using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly SchedulerService _scheduler;
    private readonly ImageRenderService _renderer;
    private readonly WallpaperService _wallpaper;

    public SettingsViewModel(ConfigService config, SchedulerService scheduler, ImageRenderService renderer, WallpaperService wallpaper)
    {
        _config = config;
        _scheduler = scheduler;
        _renderer = renderer;
        _wallpaper = wallpaper;
        _scheduler.Evaluated += (_, _) => OnPropertyChanged(nameof(PauseText));
    }

    private AppSettings S => _config.Config.Settings;

    public IReadOnlyList<Option<string>> ThemeOptions => Options.Themes;

    public string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    public string DataFolder => ConfigStore.AppDataFolder;

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set
        {
            S.StartWithWindows = value;
            StartupService.SetEnabled(value);
            Save();
        }
    }

    public bool StartMinimized
    {
        get => S.StartMinimized;
        set { S.StartMinimized = value; Save(); }
    }

    public bool NotificationsEnabled
    {
        get => S.NotificationsEnabled;
        set { S.NotificationsEnabled = value; Save(); }
    }

    public int DefaultNotifyMinutes
    {
        get => S.DefaultNotifyMinutes;
        set { S.DefaultNotifyMinutes = Math.Clamp(value, 0, 1440); Save(); }
    }

    public string Theme
    {
        get => S.Theme;
        set
        {
            S.Theme = value;
            ThemeHelper.Apply(value);
            Save();
        }
    }

    public string PauseText => _scheduler.IsPaused
        ? _config.Config.PausedUntil == DateTime.MaxValue
            ? "Agendamentos pausados até você retomar."
            : $"Agendamentos pausados até {_config.Config.PausedUntil:dd/MM/yyyy HH:mm}."
        : "Agendamentos em execução.";

    public bool IsPaused => _scheduler.IsPaused;

    [RelayCommand]
    private void Pause(string? minutes)
    {
        if (minutes == "tomorrow")
            _scheduler.PauseUntilTomorrow();
        else
            _scheduler.Pause(int.TryParse(minutes, out var m) ? TimeSpan.FromMinutes(m) : null);
        OnPropertyChanged(nameof(IsPaused));
    }

    [RelayCommand]
    private void Resume()
    {
        _scheduler.Resume();
        OnPropertyChanged(nameof(IsPaused));
    }

    [RelayCommand]
    private void Reapply()
    {
        _wallpaper.ForgetLastApplied();
        _scheduler.Reevaluate(force: true);
    }

    /// <summary>Offered while running a published exe from somewhere other than the install folder.</summary>
    public bool CanInstall => SetupService.IsSingleFile && !SetupService.IsInstalled;

    [RelayCommand]
    private void Install()
    {
        var exe = SetupService.Install();
        S.StartWithWindows = true;
        Save();
        if (!string.Equals(exe, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            ((App)App.Current).HandOverTo(exe);
    }

    [RelayCommand]
    private void OpenDataFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{ConfigStore.AppDataFolder}\"") { UseShellExecute = true });

    [RelayCommand]
    private void ClearCache()
    {
        _renderer.ClearCache();
        _wallpaper.ForgetLastApplied();
        _scheduler.Reevaluate(force: true);
    }

    private void Save()
    {
        OnPropertyChanged(string.Empty);
        _config.Save();
    }
}
