namespace WinBgScheduler.Core.Scheduling;

/// <summary>Occurrences the user chose to skip ("Pular desta vez").</summary>
public sealed class SkipList
{
    private readonly HashSet<(Guid ScheduleId, DateTime Start)> _skipped = [];

    public void Add(Guid scheduleId, DateTime occurrenceStart) => _skipped.Add((scheduleId, occurrenceStart));

    public bool Contains(Guid scheduleId, DateTime occurrenceStart) => _skipped.Contains((scheduleId, occurrenceStart));

    /// <summary>Drops entries whose occurrence started before <paramref name="before"/> minus a week.</summary>
    public void Prune(DateTime before) => _skipped.RemoveWhere(s => s.Start < before.AddDays(-7));
}
