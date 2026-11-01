using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

public sealed partial class ScheduleItemViewModel(Schedule schedule, ConfigService config) : ObservableObject
{
    public Schedule Schedule { get; } = schedule;

    public string Name => Schedule.Name;

    public string Summary => ScheduleDescriber.Describe(Schedule);

    [ObservableProperty]
    public partial string? Thumbnail { get; set; }

    [ObservableProperty]
    public partial bool IsActiveNow { get; set; }

    [ObservableProperty]
    public partial string NextText { get; set; } = "";

    public bool IsEnabled
    {
        get => Schedule.Enabled;
        set
        {
            if (Schedule.Enabled == value)
                return;
            Schedule.Enabled = value;
            OnPropertyChanged();
            config.Save();
        }
    }
}

public sealed partial class SchedulesViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly SchedulerService _scheduler;
    private readonly ContentFiles _files;
    private readonly Navigator _navigator;

    public SchedulesViewModel(ConfigService config, SchedulerService scheduler, ContentFiles files, Navigator navigator)
    {
        _config = config;
        _scheduler = scheduler;
        _files = files;
        _navigator = navigator;
        _config.Changed += (_, _) => Refresh();
        _scheduler.Evaluated += (_, _) => RefreshStatus();
        Refresh();
    }

    public ObservableCollection<ScheduleItemViewModel> Items { get; } = [];

    public bool IsEmpty => Items.Count == 0;

    public void Refresh()
    {
        Items.Clear();
        foreach (var s in _config.Config.Schedules
                     .OrderBy(s => WallpaperPlanner.Specificity(s.Recurrence.Kind))
                     .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var content = s.Assignments.FirstOrDefault(a => !a.Content.IsEmpty)?.Content;
            Items.Add(new ScheduleItemViewModel(s, _config)
            {
                Thumbnail = content is null ? null : _files.Resolve(content).FirstOrDefault(),
            });
        }
        OnPropertyChanged(nameof(IsEmpty));
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var now = _scheduler.Now;
        var active = _scheduler.CurrentPlans.Select(p => p.Schedule?.Id).ToHashSet();
        foreach (var item in Items)
        {
            item.IsActiveNow = active.Contains(item.Schedule.Id);
            var next = item.Schedule.OccurrencesBetween(now, now.AddYears(2)).FirstOrDefault(o => o.Start > now);
            item.NextText = !item.Schedule.Enabled ? "Desativado"
                : item.IsActiveNow ? "Ativo agora"
                : next.Start > now ? $"Próxima: {SchedulerService.FormatWhen(next.Start, now)}"
                : "Sem próximas ocorrências";
        }
    }

    [RelayCommand]
    private void New()
    {
        var schedule = new Schedule
        {
            NotifyMinutesBefore = _config.Config.Settings.DefaultNotifyMinutes,
            Recurrence = new() { Kind = RecurrenceKind.Weekly, DaysOfWeek = [DateTime.Today.DayOfWeek] },
            Window = new() { Kind = TimeWindowKind.Range, Start = new(19, 0), End = new(21, 0) },
        };
        _navigator.Push(new ScheduleEditorViewModel(schedule, true, _config, _scheduler, _files, _navigator));
    }

    [RelayCommand]
    private void Edit(ScheduleItemViewModel item) =>
        _navigator.Push(new ScheduleEditorViewModel(item.Schedule.Clone(), false, _config, _scheduler, _files, _navigator));

    [RelayCommand]
    private void Duplicate(ScheduleItemViewModel item)
    {
        var copy = item.Schedule.Clone();
        copy.Id = Guid.NewGuid();
        copy.CreatedAt = DateTime.Now;
        copy.Name += " (cópia)";
        _navigator.Push(new ScheduleEditorViewModel(copy, true, _config, _scheduler, _files, _navigator));
    }

    [RelayCommand]
    private async Task DeleteAsync(ScheduleItemViewModel item)
    {
        var box = new Wpf.Ui.Controls.MessageBox
        {
            Title = "Excluir agendamento",
            Content = $"Excluir \"{item.Name}\"? Isso não pode ser desfeito.",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
        };
        if (await box.ShowDialogAsync() != Wpf.Ui.Controls.MessageBoxResult.Primary)
            return;
        _config.Config.Schedules.RemoveAll(s => s.Id == item.Schedule.Id);
        _config.Save();
    }
}
