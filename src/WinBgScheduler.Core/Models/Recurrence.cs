namespace WinBgScheduler.Core.Models;

/// <summary>Which calendar days a schedule occurs on.</summary>
public sealed class Recurrence
{
    public RecurrenceKind Kind { get; set; } = RecurrenceKind.Weekly;

    /// <summary>Anchor for intervals and first day the recurrence is valid (ignored by Once and DateRange).</summary>
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Optional last day the recurrence is valid (ignored by Once and DateRange).</summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Every N days / weeks / months.</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Once: the date.</summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Weekly: days of the week.</summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];

    public MonthlyMode MonthlyMode { get; set; } = MonthlyMode.DayOfMonth;

    /// <summary>Monthly (DayOfMonth) and Yearly: day of the month. Clamped to the month length.</summary>
    public int DayOfMonth { get; set; } = 1;

    /// <summary>Monthly (NthWeekday): 1..4, or -1 for the last one.</summary>
    public int WeekOfMonth { get; set; } = 1;

    /// <summary>Monthly (NthWeekday): the weekday.</summary>
    public DayOfWeek Weekday { get; set; } = DayOfWeek.Monday;

    /// <summary>Yearly: month 1..12.</summary>
    public int Month { get; set; } = 1;

    /// <summary>DateRange: first day.</summary>
    public DateOnly RangeStart { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>DateRange: last day (inclusive).</summary>
    public DateOnly RangeEnd { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public bool Matches(DateOnly day)
    {
        switch (Kind)
        {
            case RecurrenceKind.Once:
                return day == Date;
            case RecurrenceKind.DateRange:
                return day >= RangeStart && day <= RangeEnd;
        }

        if (day < StartDate || (EndDate is { } end && day > end))
            return false;

        var interval = Math.Max(1, Interval);
        switch (Kind)
        {
            case RecurrenceKind.Daily:
                return (day.DayNumber - StartDate.DayNumber) % interval == 0;

            case RecurrenceKind.Weekly:
                if (!DaysOfWeek.Contains(day.DayOfWeek))
                    return false;
                var weeks = (WeekStart(day).DayNumber - WeekStart(StartDate).DayNumber) / 7;
                return weeks % interval == 0;

            case RecurrenceKind.Monthly:
                var months = (day.Year - StartDate.Year) * 12 + day.Month - StartDate.Month;
                if (months % interval != 0)
                    return false;
                return MonthlyMode == MonthlyMode.DayOfMonth
                    ? day.Day == ClampDay(day.Year, day.Month, DayOfMonth)
                    : IsNthWeekday(day);

            case RecurrenceKind.Yearly:
                return day.Month == Month && day.Day == ClampDay(day.Year, Month, DayOfMonth);

            default:
                return false;
        }
    }

    private bool IsNthWeekday(DateOnly day)
    {
        if (day.DayOfWeek != Weekday)
            return false;
        if (WeekOfMonth < 0)
            return day.Day + 7 > DateTime.DaysInMonth(day.Year, day.Month);
        return (day.Day - 1) / 7 + 1 == WeekOfMonth;
    }

    private static int ClampDay(int year, int month, int day) =>
        Math.Clamp(day, 1, DateTime.DaysInMonth(year, month));

    // Weeks start on Sunday (pt-BR calendar).
    private static DateOnly WeekStart(DateOnly day) => day.AddDays(-(int)day.DayOfWeek);

    public Recurrence Clone()
    {
        var copy = (Recurrence)MemberwiseClone();
        copy.DaysOfWeek = [.. DaysOfWeek];
        return copy;
    }
}
