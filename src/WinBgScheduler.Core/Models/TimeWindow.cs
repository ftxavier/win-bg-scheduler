using System.Text.Json.Serialization;

namespace WinBgScheduler.Core.Models;

/// <summary>A concrete period during which a schedule is active.</summary>
public readonly record struct Occurrence(DateTime Start, DateTime End)
{
    public TimeSpan Length => End - Start;

    public bool Contains(DateTime t) => t >= Start && t < End;
}

/// <summary>The part of the day a schedule is active on each matching day.</summary>
public sealed class TimeWindow
{
    public const int MaxDurationMinutes = 7 * 24 * 60;

    public TimeWindowKind Kind { get; set; } = TimeWindowKind.AllDay;

    public TimeOnly Start { get; set; } = new(19, 0);

    /// <summary>Range: end time. When it is not after <see cref="Start"/> the window ends on the next day.</summary>
    public TimeOnly End { get; set; } = new(21, 0);

    /// <summary>Duration: minutes from <see cref="Start"/>.</summary>
    public int DurationMinutes { get; set; } = 30;

    /// <summary>How many days before an occurrence's day it can still be active (for look-back).</summary>
    [JsonIgnore]
    public int SpanDays => Kind == TimeWindowKind.Duration
        ? (int)Math.Ceiling((Start.ToTimeSpan().TotalMinutes + ClampedDuration) / 1440.0)
        : 1;

    private int ClampedDuration => Math.Clamp(DurationMinutes, 1, MaxDurationMinutes);

    public Occurrence ToOccurrence(DateOnly day)
    {
        var midnight = day.ToDateTime(TimeOnly.MinValue);
        return Kind switch
        {
            TimeWindowKind.AllDay => new(midnight, midnight.AddDays(1)),
            TimeWindowKind.Range => new(
                midnight + Start.ToTimeSpan(),
                (End > Start ? midnight : midnight.AddDays(1)) + End.ToTimeSpan()),
            _ => new(
                midnight + Start.ToTimeSpan(),
                midnight + Start.ToTimeSpan() + TimeSpan.FromMinutes(ClampedDuration)),
        };
    }

    public TimeWindow Clone() => (TimeWindow)MemberwiseClone();
}
