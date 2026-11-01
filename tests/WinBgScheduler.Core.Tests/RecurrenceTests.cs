using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Tests;

public class RecurrenceTests
{
    private static readonly DateOnly Start = new(2026, 1, 1); // Thursday

    [Fact]
    public void Once_matches_only_its_date()
    {
        var r = new Recurrence { Kind = RecurrenceKind.Once, Date = new(2026, 10, 31) };
        Assert.True(r.Matches(new(2026, 10, 31)));
        Assert.False(r.Matches(new(2026, 11, 1)));
    }

    [Fact]
    public void Daily_respects_interval_and_start()
    {
        var r = new Recurrence { Kind = RecurrenceKind.Daily, StartDate = Start, Interval = 3 };
        Assert.True(r.Matches(Start));
        Assert.False(r.Matches(Start.AddDays(1)));
        Assert.True(r.Matches(Start.AddDays(3)));
        Assert.False(r.Matches(Start.AddDays(-3)));
    }

    [Fact]
    public void Weekly_every_wednesday()
    {
        var r = new Recurrence { Kind = RecurrenceKind.Weekly, StartDate = Start, DaysOfWeek = [DayOfWeek.Wednesday] };
        Assert.True(r.Matches(new(2026, 11, 4)));
        Assert.False(r.Matches(new(2026, 11, 5)));
    }

    [Fact]
    public void Weekly_every_other_week()
    {
        // Week of 2026-01-04 (Sun) is week 1 after the start week.
        var r = new Recurrence { Kind = RecurrenceKind.Weekly, StartDate = Start, Interval = 2, DaysOfWeek = [DayOfWeek.Monday] };
        Assert.False(r.Matches(new(2026, 1, 5)));
        Assert.True(r.Matches(new(2026, 1, 12)));
    }

    [Fact]
    public void Weekly_end_date_is_inclusive()
    {
        var r = new Recurrence
        {
            Kind = RecurrenceKind.Weekly, StartDate = Start, EndDate = new(2026, 11, 4), DaysOfWeek = [DayOfWeek.Wednesday],
        };
        Assert.True(r.Matches(new(2026, 11, 4)));
        Assert.False(r.Matches(new(2026, 11, 11)));
    }

    [Fact]
    public void Monthly_day_31_clamps_to_last_day()
    {
        var r = new Recurrence { Kind = RecurrenceKind.Monthly, StartDate = Start, DayOfMonth = 31 };
        Assert.True(r.Matches(new(2026, 2, 28)));
        Assert.True(r.Matches(new(2026, 4, 30)));
        Assert.True(r.Matches(new(2026, 5, 31)));
        Assert.False(r.Matches(new(2026, 5, 30)));
    }

    [Fact]
    public void Monthly_last_friday()
    {
        var r = new Recurrence
        {
            Kind = RecurrenceKind.Monthly, StartDate = Start, MonthlyMode = MonthlyMode.NthWeekday,
            WeekOfMonth = -1, Weekday = DayOfWeek.Friday,
        };
        Assert.True(r.Matches(new(2026, 10, 30)));
        Assert.False(r.Matches(new(2026, 10, 23)));
    }

    [Fact]
    public void Monthly_first_monday()
    {
        var r = new Recurrence
        {
            Kind = RecurrenceKind.Monthly, StartDate = Start, MonthlyMode = MonthlyMode.NthWeekday,
            WeekOfMonth = 1, Weekday = DayOfWeek.Monday,
        };
        Assert.True(r.Matches(new(2026, 11, 2)));
        Assert.False(r.Matches(new(2026, 11, 9)));
    }

    [Fact]
    public void Yearly_feb_29_falls_back_to_28()
    {
        var r = new Recurrence { Kind = RecurrenceKind.Yearly, StartDate = Start, Month = 2, DayOfMonth = 29 };
        Assert.True(r.Matches(new(2027, 2, 28)));
        Assert.True(r.Matches(new(2028, 2, 29)));
        Assert.False(r.Matches(new(2028, 2, 28)));
    }

    [Fact]
    public void DateRange_is_inclusive()
    {
        var r = new Recurrence { Kind = RecurrenceKind.DateRange, RangeStart = new(2026, 12, 1), RangeEnd = new(2026, 12, 31) };
        Assert.True(r.Matches(new(2026, 12, 1)));
        Assert.True(r.Matches(new(2026, 12, 31)));
        Assert.False(r.Matches(new(2027, 1, 1)));
    }
}
