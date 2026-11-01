namespace WinBgScheduler.Core.Models;

public sealed class AppConfig
{
    public int Version { get; set; } = 1;

    public List<Schedule> Schedules { get; set; } = [];

    /// <summary>Wallpaper used when no schedule is active.</summary>
    public List<MonitorAssignment> Defaults { get; set; } = [];

    public AppSettings Settings { get; set; } = new();

    /// <summary>While set and in the future, schedules are not applied. <see cref="DateTime.MaxValue"/> = indefinitely.</summary>
    public DateTime? PausedUntil { get; set; }

    public bool IsPaused(DateTime now) => PausedUntil is { } until && until > now;

    public WallpaperContent? DefaultFor(string monitorId) =>
        Defaults.FirstOrDefault(a => a.MonitorId == monitorId && !a.Content.IsEmpty)?.Content
        ?? Defaults.FirstOrDefault(a => a.MonitorId is null && !a.Content.IsEmpty)?.Content;
}

public sealed class AppSettings
{
    /// <summary>Set by the first-run install or the settings toggle.</summary>
    public bool StartWithWindows { get; set; }

    public bool StartMinimized { get; set; } = true;

    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>Notification lead time suggested for new schedules.</summary>
    public int DefaultNotifyMinutes { get; set; } = 5;

    /// <summary>"System", "Light" or "Dark".</summary>
    public string Theme { get; set; } = "System";
}
