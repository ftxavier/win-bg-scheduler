namespace WinBgScheduler.Core.Models;

public sealed class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "Novo agendamento";

    public bool Enabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Recurrence Recurrence { get; set; } = new();

    public TimeWindow Window { get; set; } = new();

    /// <summary>Days on which the schedule does not apply.</summary>
    public List<DateOnly> Exceptions { get; set; } = [];

    public List<MonitorAssignment> Assignments { get; set; } = [];

    /// <summary>Minutes before the start to show a notification. 0 = no notification.</summary>
    public int NotifyMinutesBefore { get; set; } = 5;

    public bool OccursOn(DateOnly day) => !Exceptions.Contains(day) && Recurrence.Matches(day);

    /// <summary>Occurrences that overlap [from, to).</summary>
    public IEnumerable<Occurrence> OccurrencesBetween(DateTime from, DateTime to)
    {
        var day = DateOnly.FromDateTime(from).AddDays(-Window.SpanDays);
        var last = DateOnly.FromDateTime(to);
        for (; day <= last; day = day.AddDays(1))
        {
            if (!OccursOn(day))
                continue;
            var occurrence = Window.ToOccurrence(day);
            if (occurrence.End > from && occurrence.Start < to)
                yield return occurrence;
        }
    }

    /// <summary>The occurrence active at <paramref name="t"/>, if any. The latest-starting one wins when they overlap.</summary>
    public Occurrence? OccurrenceAt(DateTime t)
    {
        Occurrence? found = null;
        foreach (var occurrence in OccurrencesBetween(t, t.AddTicks(1)))
            found = occurrence;
        return found;
    }

    /// <summary>Content for a monitor: its own assignment first, then the "all monitors" one.</summary>
    public WallpaperContent? ContentFor(string monitorId) =>
        Assignments.FirstOrDefault(a => a.MonitorId == monitorId && !a.Content.IsEmpty)?.Content
        ?? Assignments.FirstOrDefault(a => a.MonitorId is null && !a.Content.IsEmpty)?.Content;

    public Schedule Clone() => new()
    {
        Id = Id,
        Name = Name,
        Enabled = Enabled,
        CreatedAt = CreatedAt,
        Recurrence = Recurrence.Clone(),
        Window = Window.Clone(),
        Exceptions = [.. Exceptions],
        Assignments = Assignments.Select(a => a.Clone()).ToList(),
        NotifyMinutesBefore = NotifyMinutesBefore,
    };
}
