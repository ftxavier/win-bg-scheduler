namespace WinBgScheduler.Core.Models;

/// <summary>How the image is fitted to the monitor (mirrors DESKTOP_WALLPAPER_POSITION).</summary>
public enum FitMode
{
    Fill,
    Fit,
    Stretch,
    Tile,
    Center,
    Span,
}

public enum RecurrenceKind
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    DateRange,
}

public enum MonthlyMode
{
    DayOfMonth,
    NthWeekday,
}

public enum TimeWindowKind
{
    AllDay,
    Range,
    Duration,
}

public enum RotationOrder
{
    Sequential,
    Random,
}
