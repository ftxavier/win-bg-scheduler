using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.Core.Tests;

public class DescriberAndGeometryTests
{
    [Fact]
    public void Describes_weekly_range()
    {
        var s = new Schedule
        {
            Recurrence = new() { Kind = RecurrenceKind.Weekly, DaysOfWeek = [DayOfWeek.Wednesday] },
            Window = new() { Kind = TimeWindowKind.Range, Start = new(19, 0), End = new(21, 0) },
        };
        Assert.Equal("Toda quarta-feira, das 19:00 às 21:00", ScheduleDescriber.Describe(s));
    }

    [Fact]
    public void Describes_common_weekly_sets()
    {
        Assert.Equal("Dias úteis", ScheduleDescriber.Describe(new Recurrence
        {
            Kind = RecurrenceKind.Weekly,
            DaysOfWeek = [DayOfWeek.Friday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday],
        }));
        Assert.Equal("Seg, qua e sex", ScheduleDescriber.Describe(new Recurrence
        {
            Kind = RecurrenceKind.Weekly, DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        }));
    }

    [Fact]
    public void Describes_monthly_and_duration()
    {
        Assert.Equal("No último sábado do mês", ScheduleDescriber.Describe(new Recurrence
        {
            Kind = RecurrenceKind.Monthly, MonthlyMode = MonthlyMode.NthWeekday, WeekOfMonth = -1, Weekday = DayOfWeek.Saturday,
        }));
        Assert.Equal("às 18:30 por 1 h 30 min", ScheduleDescriber.Describe(new TimeWindow
        {
            Kind = TimeWindowKind.Duration, Start = new(18, 30), DurationMinutes = 90,
        }));
    }

    // A 2560x1440 image on a 1920x1440 (4:3) monitor or a 1920x1080 (16:9) one.
    [Theory]
    [InlineData(FitMode.Fill, 1920, 1440, -320, 0, 2560, 1440)]
    [InlineData(FitMode.Fit, 1920, 1440, 0, 180, 1920, 1080)]
    [InlineData(FitMode.Fit, 1920, 1080, 0, 0, 1920, 1080)]
    [InlineData(FitMode.Stretch, 1920, 1440, 0, 0, 1920, 1440)]
    [InlineData(FitMode.Center, 1920, 1080, -320, -180, 2560, 1440)]
    [InlineData(FitMode.Tile, 1920, 1080, 0, 0, 2560, 1440)]
    public void Places_image_for_fit_mode(FitMode fit, double monitorW, double monitorH, double x, double y, double w, double h)
    {
        Assert.Equal(new PlacedRect(x, y, w, h), FitGeometry.Place(fit, 2560, 1440, monitorW, monitorH));
    }

    [Fact]
    public void Config_round_trips_through_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"wbs-{Guid.NewGuid()}.json");
        try
        {
            var store = new ConfigStore(path);
            var config = new AppConfig { PausedUntil = DateTime.MaxValue };
            config.Schedules.Add(new Schedule
            {
                Recurrence = new() { Kind = RecurrenceKind.Weekly, DaysOfWeek = [DayOfWeek.Wednesday] },
                Window = new() { Kind = TimeWindowKind.Range, Start = new(19, 0), End = new(21, 0) },
                Exceptions = [new(2026, 12, 25)],
                Assignments = [new() { MonitorId = "M1", Content = new() { Images = ["x.jpg"], Fit = FitMode.Tile } }],
            });
            store.Save(config);

            var loaded = store.Load();
            var s = Assert.Single(loaded.Schedules);
            Assert.Equal(config.Schedules[0].Id, s.Id);
            Assert.Equal(new TimeOnly(21, 0), s.Window.End);
            Assert.Equal(FitMode.Tile, s.Assignments[0].Content.Fit);
            Assert.Equal(new DateOnly(2026, 12, 25), s.Exceptions[0]);
            Assert.True(loaded.IsPaused(DateTime.Now));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
