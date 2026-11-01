using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.Services;

/// <summary>
/// Keeps the desktop in sync with the schedules. Runs entirely on the UI thread: one timer set to the
/// next change, plus re-evaluation on clock changes, resume, unlock, display changes and config saves.
/// </summary>
public sealed partial class SchedulerService : ObservableObject
{
    private static readonly TimeSpan Horizon = TimeSpan.FromDays(1);
    private static readonly TimeSpan MaxWait = TimeSpan.FromMinutes(15);

    private readonly ConfigService _config;
    private readonly WallpaperService _wallpaper;
    private readonly NotificationService _notifications;
    private readonly ContentFiles _files;
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _timer = new();
    private readonly HashSet<(Guid, DateTime)> _notified = [];
    private bool _testing;

    public SchedulerService(
        ConfigService config, WallpaperService wallpaper, NotificationService notifications,
        ContentFiles files, WallpaperPlanner planner, TimeProvider time)
    {
        _config = config;
        _wallpaper = wallpaper;
        _notifications = notifications;
        _files = files;
        Planner = planner;
        _time = time;
        _timer.Tick += (_, _) => Reevaluate();
    }

    public WallpaperPlanner Planner { get; }

    public SkipList Skipped { get; } = new();

    [ObservableProperty]
    public partial IReadOnlyList<MonitorInfo> Monitors { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<MonitorPlan> CurrentPlans { get; private set; } = [];

    [ObservableProperty]
    public partial string CurrentText { get; private set; } = "";

    [ObservableProperty]
    public partial string NextText { get; private set; } = "";

    [ObservableProperty]
    public partial string? ErrorText { get; private set; }

    public DateTime Now => _time.GetLocalNow().DateTime;

    public bool IsPaused => _config.Config.IsPaused(Now);

    public IReadOnlyList<string> MonitorIds => Monitors.Count > 0 ? Monitors.Select(m => m.Id).ToList() : [""];

    /// <summary>Raised after every evaluation so views can refresh "active now" markers.</summary>
    public event EventHandler? Evaluated;

    public void Start()
    {
        RefreshMonitors();
        _config.Changed += (_, _) =>
        {
            _files.Invalidate();
            Reevaluate();
        };
        SystemEvents.TimeChanged += OnSystemEvent;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Reevaluate(force: true);
    }

    public void Stop()
    {
        _timer.Stop();
        SystemEvents.TimeChanged -= OnSystemEvent;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    public void Reevaluate(bool force = false)
    {
        _timer.Stop();
        var now = Now;
        var config = _config.Config;
        Skipped.Prune(now);

        var plans = Planner.Evaluate(config, MonitorIds, now, Skipped);
        CurrentPlans = plans;
        if (!_testing && !config.IsPaused(now))
            ApplyPlans(plans, force);

        CheckNotifications(now);
        UpdateTexts(now);

        var wake = Planner.NextChange(config, MonitorIds, now, Horizon, Skipped);
        if (NextNotificationAt(now) is { } notifyAt && notifyAt < wake)
            wake = notifyAt;
        if (config.PausedUntil is { } until && until > now && until < wake)
            wake = until;
        if (wake - now > MaxWait)
            wake = now + MaxWait;

        // Small margin so the timer lands just after the boundary.
        _timer.Interval = wake - now + TimeSpan.FromMilliseconds(250);
        _timer.Start();
        Evaluated?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyPlans(IReadOnlyList<MonitorPlan> plans, bool force)
    {
        var assignments = plans
            .Where(p => p.ImagePath is not null)
            .Select(p => (Plan: p, Monitor: Monitors.FirstOrDefault(m => m.Id == p.MonitorId)))
            .Where(x => x.Monitor is not null)
            .Select(x => new WallpaperAssignment(x.Monitor!, x.Plan.ImagePath!, x.Plan.Fit))
            .ToList();
        try
        {
            _wallpaper.Apply(assignments, force);
            ErrorText = null;
        }
        catch (Exception e)
        {
            ErrorText = $"Falha ao aplicar o papel de parede: {e.Message}";
        }
    }

    /// <summary>Stops scheduling and shows the default wallpaper (used when the app is closed).</summary>
    public void StopAndApplyDefault()
    {
        Stop();
        var defaultsOnly = new AppConfig { Defaults = _config.Config.Defaults };
        ApplyPlans(Planner.Evaluate(defaultsOnly, MonitorIds, Now), force: true);
    }

    public void Pause(TimeSpan? duration)
    {
        _config.Config.PausedUntil = duration is { } d ? Now + d : DateTime.MaxValue;
        _config.Save();
    }

    public void PauseUntilTomorrow()
    {
        _config.Config.PausedUntil = Now.Date.AddDays(1);
        _config.Save();
    }

    public void Resume()
    {
        _config.Config.PausedUntil = null;
        _wallpaper.ForgetLastApplied();
        _config.Save();
    }

    public void Skip(Guid scheduleId, DateTime occurrenceStart)
    {
        Skipped.Add(scheduleId, occurrenceStart);
        Reevaluate();
    }

    /// <summary>Skips every schedule occurrence active right now.</summary>
    public void SkipCurrent()
    {
        foreach (var plan in CurrentPlans)
        {
            if (plan is { Schedule: { } s, Occurrence: { } o })
                Skipped.Add(s.Id, o.Start);
        }
        Reevaluate();
    }

    /// <summary>Shows an image on the desktop for a few seconds, then restores what was there.</summary>
    public async Task TestAsync(string imagePath, FitMode fit, string? monitorId, TimeSpan duration)
    {
        if (_testing)
            return;
        _testing = true;
        var snapshot = _wallpaper.Capture(Monitors);
        try
        {
            var targets = monitorId is null ? Monitors : Monitors.Where(m => m.Id == monitorId).ToList();
            _wallpaper.Apply(targets.Select(m => new WallpaperAssignment(m, imagePath, fit)).ToList(), force: true);
            await Task.Delay(duration);
        }
        finally
        {
            _wallpaper.Restore(snapshot);
            _testing = false;
            Reevaluate(force: true);
        }
    }

    public void HandleToast(ToastArguments args)
    {
        if (args.TryGetValue("action", out string action) && action == "skip"
            && args.TryGetValue("schedule", out string id) && Guid.TryParse(id, out var scheduleId)
            && args.TryGetValue("start", out string ticks) && long.TryParse(ticks, out var startTicks))
        {
            Skip(scheduleId, new DateTime(startTicks));
        }
    }

    private void RefreshMonitors()
    {
        try
        {
            Monitors = _wallpaper.GetMonitors();
        }
        catch (Exception e)
        {
            ErrorText = $"Não foi possível listar os monitores: {e.Message}";
        }
    }

    /// <summary>Schedule starts within the next day that ask for a notification.</summary>
    private IEnumerable<(Schedule Schedule, Occurrence Occurrence, WallpaperContent? Content, DateTime NotifyAt)> PendingNotifications(DateTime now)
    {
        if (!_config.Config.Settings.NotificationsEnabled || _config.Config.IsPaused(now))
            return [];
        return Planner.Upcoming(_config.Config, MonitorIds, now, now + Horizon, Skipped)
            .Where(c => c is { Schedule.NotifyMinutesBefore: > 0, Occurrence: { } o } && o.Start == c.At)
            .GroupBy(c => (c.Schedule!.Id, c.At))
            .Select(g => g.First())
            .Select(c => (c.Schedule!, c.Occurrence!.Value, c.Content, c.At.AddMinutes(-c.Schedule!.NotifyMinutesBefore)));
    }

    private void CheckNotifications(DateTime now)
    {
        foreach (var (schedule, occurrence, content, notifyAt) in PendingNotifications(now))
        {
            if (notifyAt > now || !_notified.Add((schedule.Id, occurrence.Start)))
                continue;
            var image = content is null
                ? null
                : ImagePicker.Pick(content, _files.Resolve(content), occurrence.Start, occurrence.Start, ImagePicker.SeedFor(schedule.Id));
            try
            {
                _notifications.ShowUpcoming(schedule, occurrence, image, now);
            }
            catch (Exception e)
            {
                ErrorText = $"Falha ao mostrar notificação: {e.Message}";
            }
        }
        _notified.RemoveWhere(n => n.Item2 < now.AddDays(-1));
    }

    private DateTime? NextNotificationAt(DateTime now) =>
        PendingNotifications(now).Select(p => p.NotifyAt).Where(t => t > now).Order().Cast<DateTime?>().FirstOrDefault();

    private void UpdateTexts(DateTime now)
    {
        var config = _config.Config;
        var active = CurrentPlans.Where(p => p.Schedule is not null).Select(p => p.Schedule!.Name).Distinct().ToList();
        CurrentText = config.IsPaused(now)
            ? config.PausedUntil == DateTime.MaxValue ? "Pausado" : $"Pausado até {config.PausedUntil:dd/MM HH:mm}"
            : active.Count > 0 ? $"Agora: {string.Join(", ", active)}" : "Agora: papel padrão";

        var next = Planner.Upcoming(config, MonitorIds, now, now.AddDays(7), Skipped).FirstOrDefault();
        NextText = next is null
            ? "Nenhuma troca nos próximos 7 dias"
            : $"Próxima: {FormatWhen(next.At, now)} · {next.Schedule?.Name ?? "papel padrão"}";
    }

    public static string FormatWhen(DateTime at, DateTime now)
    {
        var day = (at.Date - now.Date).Days switch
        {
            0 => "hoje",
            1 => "amanhã",
            < 7 => at.ToString("dddd"),
            _ => at.ToString("dd/MM"),
        };
        return $"{day} {at:HH:mm}";
    }

    private void OnSystemEvent(object? sender, EventArgs e) =>
        App.Current.Dispatcher.BeginInvoke(() => Reevaluate(force: true));

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            OnSystemEvent(sender, e);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.SessionLogon)
            OnSystemEvent(sender, e);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        App.Current.Dispatcher.BeginInvoke(() =>
        {
            RefreshMonitors();
            Reevaluate(force: true);
        });
}
