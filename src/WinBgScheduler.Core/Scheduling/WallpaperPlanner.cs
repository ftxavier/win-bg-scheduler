using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Scheduling;

/// <summary>What a monitor should show at a given moment.</summary>
public sealed record MonitorPlan(
    string MonitorId,
    string? ImagePath,
    FitMode Fit,
    Schedule? Schedule,
    Occurrence? Occurrence,
    WallpaperContent? Content,
    DateTime RotationAnchor);

/// <summary>A moment where the schedule in effect for a monitor changes (rotation ticks are not included).</summary>
public sealed record UpcomingChange(
    DateTime At,
    string MonitorId,
    Schedule? Schedule,
    Occurrence? Occurrence,
    WallpaperContent? Content);

/// <summary>Decides which schedule wins for each monitor and when the next change happens.</summary>
public sealed class WallpaperPlanner(Func<WallpaperContent, IReadOnlyList<string>> resolveFiles)
{
    /// <summary>"Most specific wins": lower is more specific.</summary>
    public static int Specificity(RecurrenceKind kind) => kind switch
    {
        RecurrenceKind.Once => 0,
        RecurrenceKind.Yearly => 1,
        RecurrenceKind.Monthly => 2,
        RecurrenceKind.Weekly => 3,
        RecurrenceKind.Daily => 4,
        _ => 5,
    };

    public (Schedule Schedule, Occurrence Occurrence, WallpaperContent Content)? ActiveFor(
        AppConfig config, string monitorId, DateTime t, SkipList? skip = null)
    {
        (Schedule Schedule, Occurrence Occurrence, WallpaperContent Content)? best = null;
        foreach (var schedule in config.Schedules)
        {
            if (!schedule.Enabled || schedule.ContentFor(monitorId) is not { } content)
                continue;
            if (schedule.OccurrenceAt(t) is not { } occurrence || skip?.Contains(schedule.Id, occurrence.Start) == true)
                continue;
            if (best is null || Beats(schedule, occurrence, best.Value.Schedule, best.Value.Occurrence))
                best = (schedule, occurrence, content);
        }
        return best;
    }

    private static bool Beats(Schedule a, Occurrence aOcc, Schedule b, Occurrence bOcc)
    {
        var bySpecificity = Specificity(a.Recurrence.Kind).CompareTo(Specificity(b.Recurrence.Kind));
        if (bySpecificity != 0)
            return bySpecificity < 0;
        var byLength = aOcc.Length.CompareTo(bOcc.Length);
        if (byLength != 0)
            return byLength < 0;
        return a.CreatedAt > b.CreatedAt;
    }

    public MonitorPlan Resolve(AppConfig config, string monitorId, DateTime t, SkipList? skip = null)
    {
        var active = ActiveFor(config, monitorId, t, skip);
        var content = active?.Content ?? config.DefaultFor(monitorId);
        if (content is null)
            return new(monitorId, null, FitMode.Fill, null, null, null, ImagePicker.DefaultAnchor);

        var anchor = active?.Occurrence.Start ?? ImagePicker.DefaultAnchor;
        var seed = active is { } a ? ImagePicker.SeedFor(a.Schedule.Id) : 0;
        var image = ImagePicker.Pick(content, resolveFiles(content), anchor, t, seed);
        return new(monitorId, image, content.Fit, active?.Schedule, active?.Occurrence, content, anchor);
    }

    public IReadOnlyList<MonitorPlan> Evaluate(AppConfig config, IReadOnlyList<string> monitorIds, DateTime t, SkipList? skip = null) =>
        monitorIds.Select(m => Resolve(config, m, t, skip)).ToList();

    /// <summary>Next instant after <paramref name="t"/> when any plan may change (capped at t + horizon).</summary>
    public DateTime NextChange(AppConfig config, IReadOnlyList<string> monitorIds, DateTime t, TimeSpan horizon, SkipList? skip = null)
    {
        var next = t + horizon;
        foreach (var boundary in Boundaries(config, t, next))
        {
            next = boundary;
            break;
        }

        foreach (var plan in Evaluate(config, monitorIds, t, skip))
        {
            if (plan.Content is null)
                continue;
            var rotation = ImagePicker.NextRotation(plan.Content, resolveFiles(plan.Content).Count, plan.RotationAnchor, t);
            if (rotation is { } r && r < next)
                next = r;
        }
        return next;
    }

    /// <summary>Schedule changes per monitor in (from, to].</summary>
    public IReadOnlyList<UpcomingChange> Upcoming(
        AppConfig config, IReadOnlyList<string> monitorIds, DateTime from, DateTime to, SkipList? skip = null)
    {
        var changes = new List<UpcomingChange>();
        var current = monitorIds.ToDictionary(m => m, m => Key(ActiveFor(config, m, from, skip)));
        foreach (var at in Boundaries(config, from, to))
        {
            foreach (var monitorId in monitorIds)
            {
                var active = ActiveFor(config, monitorId, at, skip);
                var key = Key(active);
                if (key == current[monitorId])
                    continue;
                current[monitorId] = key;
                changes.Add(new(at, monitorId, active?.Schedule, active?.Occurrence,
                    active?.Content ?? config.DefaultFor(monitorId)));
            }
        }
        return changes;
    }

    private static (Guid?, DateTime?) Key((Schedule Schedule, Occurrence Occurrence, WallpaperContent Content)? active) =>
        (active?.Schedule.Id, active?.Occurrence.Start);

    /// <summary>Sorted, distinct occurrence starts/ends in (from, to].</summary>
    private static IEnumerable<DateTime> Boundaries(AppConfig config, DateTime from, DateTime to)
    {
        var set = new SortedSet<DateTime>();
        foreach (var schedule in config.Schedules.Where(s => s.Enabled))
        {
            foreach (var occurrence in schedule.OccurrencesBetween(from, to.AddTicks(1)))
            {
                if (occurrence.Start > from && occurrence.Start <= to)
                    set.Add(occurrence.Start);
                if (occurrence.End > from && occurrence.End <= to)
                    set.Add(occurrence.End);
            }
        }
        return set;
    }
}
