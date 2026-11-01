using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;

namespace WinBgScheduler.Core.Tests;

public class PlannerTests
{
    private const string Monitor = "M1";
    private static readonly string[] Monitors = [Monitor];

    // Files are just the listed image names, no disk access.
    private readonly WallpaperPlanner _planner = new(c => c.Images);

    private static Schedule Wednesday19To21(string image = "noite.jpg") => new()
    {
        Name = "Quarta à noite",
        Recurrence = new() { Kind = RecurrenceKind.Weekly, StartDate = new(2026, 1, 1), DaysOfWeek = [DayOfWeek.Wednesday] },
        Window = new() { Kind = TimeWindowKind.Range, Start = new(19, 0), End = new(21, 0) },
        Assignments = [new() { Content = new() { Images = [image] } }],
    };

    private static AppConfig ConfigWith(params Schedule[] schedules) => new()
    {
        Schedules = [.. schedules],
        Defaults = [new() { Content = new() { Images = ["padrao.jpg"] } }],
    };

    [Fact]
    public void Weekly_window_starts_and_ends_on_time()
    {
        var config = ConfigWith(Wednesday19To21());
        var wed = new DateTime(2026, 11, 4);

        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, wed.AddHours(18).AddMinutes(59)).ImagePath);
        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, wed.AddHours(19)).ImagePath);
        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, wed.AddHours(20).AddMinutes(59)).ImagePath);
        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, wed.AddHours(21)).ImagePath);
    }

    [Fact]
    public void Next_change_is_window_start_then_end()
    {
        var config = ConfigWith(Wednesday19To21());
        var wed = new DateTime(2026, 11, 4);

        Assert.Equal(wed.AddHours(19), _planner.NextChange(config, Monitors, wed.AddHours(10), TimeSpan.FromDays(1)));
        Assert.Equal(wed.AddHours(21), _planner.NextChange(config, Monitors, wed.AddHours(19), TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Window_crossing_midnight_belongs_to_start_day()
    {
        var s = Wednesday19To21();
        s.Window = new() { Kind = TimeWindowKind.Range, Start = new(22, 0), End = new(2, 0) };
        var config = ConfigWith(s);

        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 5, 1, 30, 0)).ImagePath);
        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 5, 2, 0, 0)).ImagePath);
        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 1, 0, 0)).ImagePath);
    }

    [Fact]
    public void Duration_window_lasts_given_minutes()
    {
        var s = Wednesday19To21();
        s.Window = new() { Kind = TimeWindowKind.Duration, Start = new(18, 30), DurationMinutes = 15 };
        var config = ConfigWith(s);
        var wed = new DateTime(2026, 11, 4);

        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, wed.AddHours(18).AddMinutes(44)).ImagePath);
        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, wed.AddHours(18).AddMinutes(45)).ImagePath);
    }

    [Fact]
    public void Once_beats_weekly_when_overlapping()
    {
        var once = new Schedule
        {
            Recurrence = new() { Kind = RecurrenceKind.Once, Date = new(2026, 11, 4) },
            Window = new() { Kind = TimeWindowKind.AllDay },
            Assignments = [new() { Content = new() { Images = ["especial.jpg"] } }],
        };
        var config = ConfigWith(Wednesday19To21(), once);

        Assert.Equal("especial.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 20, 0, 0)).ImagePath);
    }

    [Fact]
    public void Same_kind_shorter_window_wins()
    {
        var allDay = Wednesday19To21("dia.jpg");
        allDay.Window = new() { Kind = TimeWindowKind.AllDay };
        var config = ConfigWith(allDay, Wednesday19To21());

        Assert.Equal("dia.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 10, 0, 0)).ImagePath);
        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 20, 0, 0)).ImagePath);
    }

    [Fact]
    public void Exception_day_is_skipped()
    {
        var s = Wednesday19To21();
        s.Exceptions = [new(2026, 11, 4)];
        var config = ConfigWith(s);

        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 20, 0, 0)).ImagePath);
        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 11, 20, 0, 0)).ImagePath);
    }

    [Fact]
    public void Disabled_schedule_is_ignored()
    {
        var s = Wednesday19To21();
        s.Enabled = false;
        Assert.Equal("padrao.jpg", _planner.Resolve(ConfigWith(s), Monitor, new DateTime(2026, 11, 4, 20, 0, 0)).ImagePath);
    }

    [Fact]
    public void Skipped_occurrence_falls_back_to_default()
    {
        var s = Wednesday19To21();
        var config = ConfigWith(s);
        var skip = new SkipList();
        skip.Add(s.Id, new DateTime(2026, 11, 4, 19, 0, 0));

        Assert.Equal("padrao.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 4, 20, 0, 0), skip).ImagePath);
        Assert.Equal("noite.jpg", _planner.Resolve(config, Monitor, new DateTime(2026, 11, 11, 20, 0, 0), skip).ImagePath);
    }

    [Fact]
    public void Monitor_specific_content_overrides_all_monitors()
    {
        var s = Wednesday19To21();
        s.Assignments.Add(new() { MonitorId = "M2", Content = new() { Images = ["m2.jpg"], Fit = FitMode.Fit } });
        var config = ConfigWith(s);
        var t = new DateTime(2026, 11, 4, 20, 0, 0);

        var plans = _planner.Evaluate(config, ["M1", "M2"], t);
        Assert.Equal("noite.jpg", plans[0].ImagePath);
        Assert.Equal("m2.jpg", plans[1].ImagePath);
        Assert.Equal(FitMode.Fit, plans[1].Fit);
    }

    [Fact]
    public void Sequential_rotation_advances_and_wraps()
    {
        var s = Wednesday19To21();
        s.Assignments[0].Content = new() { Images = ["a", "b", "c"], RotationMinutes = 10 };
        var config = ConfigWith(s);
        var start = new DateTime(2026, 11, 4, 19, 0, 0);

        Assert.Equal("a", _planner.Resolve(config, Monitor, start.AddMinutes(9)).ImagePath);
        Assert.Equal("b", _planner.Resolve(config, Monitor, start.AddMinutes(10)).ImagePath);
        Assert.Equal("c", _planner.Resolve(config, Monitor, start.AddMinutes(25)).ImagePath);
        Assert.Equal("a", _planner.Resolve(config, Monitor, start.AddMinutes(30)).ImagePath);
        Assert.Equal(start.AddMinutes(30), _planner.NextChange(config, Monitors, start.AddMinutes(25), TimeSpan.FromDays(1)));
    }

    [Fact]
    public void Random_rotation_shows_every_image_once_per_cycle()
    {
        var s = Wednesday19To21();
        s.Assignments[0].Content = new() { Images = ["a", "b", "c", "d"], RotationMinutes = 5, Order = RotationOrder.Random };
        var config = ConfigWith(s);
        var start = new DateTime(2026, 11, 4, 19, 0, 0);

        var cycle = Enumerable.Range(0, 4).Select(i => _planner.Resolve(config, Monitor, start.AddMinutes(i * 5)).ImagePath).ToList();
        Assert.Equal(["a", "b", "c", "d"], cycle.Order());
    }

    [Fact]
    public void Upcoming_lists_start_and_return_to_default()
    {
        var config = ConfigWith(Wednesday19To21());
        var changes = _planner.Upcoming(config, Monitors, new DateTime(2026, 11, 4), new DateTime(2026, 11, 5));

        Assert.Equal(2, changes.Count);
        Assert.Equal(new DateTime(2026, 11, 4, 19, 0, 0), changes[0].At);
        Assert.NotNull(changes[0].Schedule);
        Assert.Equal(new DateTime(2026, 11, 4, 21, 0, 0), changes[1].At);
        Assert.Null(changes[1].Schedule);
    }

    [Fact]
    public void Wall_clock_times_hold_across_dst_dates()
    {
        // Schedules use local wall-clock time, so 19:00 stays 19:00 on any date.
        var s = Wednesday19To21();
        s.Recurrence = new() { Kind = RecurrenceKind.Daily, StartDate = new(2026, 1, 1) };
        var config = ConfigWith(s);
        foreach (var day in new[] { new DateTime(2026, 3, 8), new DateTime(2026, 3, 29), new DateTime(2026, 11, 1) })
            Assert.Equal(day.AddHours(19), _planner.NextChange(config, Monitors, day.AddHours(12), TimeSpan.FromDays(1)));
    }

    [Fact]
    public void No_content_leaves_wallpaper_untouched()
    {
        var plan = _planner.Resolve(new AppConfig(), Monitor, DateTime.Now);
        Assert.Null(plan.ImagePath);
    }
}
