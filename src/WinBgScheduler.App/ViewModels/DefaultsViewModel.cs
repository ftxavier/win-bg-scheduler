using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

/// <summary>The wallpaper shown when no schedule is active.</summary>
public sealed partial class DefaultsViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly SchedulerService _scheduler;
    private readonly ContentFiles _files;
    private List<MonitorAssignment> _draft = [];

    public DefaultsViewModel(ConfigService config, SchedulerService scheduler, ContentFiles files)
    {
        _config = config;
        _scheduler = scheduler;
        _files = files;
        Reload();
    }

    [ObservableProperty]
    public partial ContentEditorViewModel Content { get; private set; }

    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    [ObservableProperty]
    public partial bool IsTesting { get; private set; }

    [RelayCommand]
    private void Reload()
    {
        _draft = _config.Config.Defaults.Select(a => a.Clone()).ToList();
        Content = new ContentEditorViewModel(_draft, _scheduler.Monitors, _files);
        Content.ContentChanged += (_, _) => IsDirty = true;
        IsDirty = false;
    }

    [RelayCommand]
    private void Save()
    {
        _config.Config.Defaults = _draft.Where(a => !a.Content.IsEmpty).Select(a => a.Clone()).ToList();
        _config.Save();
        IsDirty = false;
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        if (Content.PreviewPath is not { } path || IsTesting)
            return;
        IsTesting = true;
        try
        {
            await _scheduler.TestAsync(path, Content.Fit, Content.SelectedTarget.MonitorId, TimeSpan.FromSeconds(10));
        }
        finally
        {
            IsTesting = false;
        }
    }
}
