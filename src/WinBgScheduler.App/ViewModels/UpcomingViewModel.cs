using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

public sealed record UpcomingItem(
    DateTime At, string Time, string Title, string Detail, string Monitors, string? Image, Guid? ScheduleId, DateTime? OccurrenceStart)
{
    public bool CanSkip => ScheduleId is not null;
}

public sealed record UpcomingDay(string Header, IReadOnlyList<UpcomingItem> Items);

/// <summary>Timeline of the next 7 days: which wallpaper takes over when.</summary>
public sealed partial class UpcomingViewModel : ObservableObject
{
    private readonly ConfigService _config;
    private readonly SchedulerService _scheduler;
    private readonly ContentFiles _files;

    public UpcomingViewModel(ConfigService config, SchedulerService scheduler, ContentFiles files)
    {
        _config = config;
        _scheduler = scheduler;
        _files = files;
        _scheduler.Evaluated += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<UpcomingItem> Now { get; } = [];

    public ObservableCollection<UpcomingDay> Days { get; } = [];

    public bool IsEmpty => Days.Count == 0;

    [RelayCommand]
    public void Refresh()
    {
        var now = _scheduler.Now;
        var monitorName = _scheduler.Monitors.ToDictionary(m => m.Id, m => $"Monitor {m.Index}");
        string MonitorsText(IEnumerable<string> ids)
        {
            var list = ids.ToList();
            return list.Count == _scheduler.MonitorIds.Count ? "Todos os monitores"
                : string.Join(", ", list.Select(id => monitorName.GetValueOrDefault(id, "?")));
        }

        Now.Clear();
        foreach (var group in _scheduler.CurrentPlans.GroupBy(p => (p.Schedule?.Id, p.ImagePath)))
        {
            var p = group.First();
            Now.Add(new UpcomingItem(now, "Agora", p.Schedule?.Name ?? "Papel padrão",
                p.Schedule is null ? (p.ImagePath is null ? "Nenhum papel padrão definido" : "Nenhum agendamento ativo") : ScheduleDescriber.Describe(p.Schedule),
                MonitorsText(group.Select(g => g.MonitorId)), p.ImagePath, p.Schedule?.Id, p.Occurrence?.Start));
        }

        var changes = _scheduler.Planner.Upcoming(_config.Config, _scheduler.MonitorIds, now, now.AddDays(7), _scheduler.Skipped);
        var items = changes
            .GroupBy(c => (c.At, c.Schedule?.Id))
            .Select(g =>
            {
                var c = g.First();
                var image = c.Content is null ? null : ImagePicker.Pick(
                    c.Content, _files.Resolve(c.Content), c.Occurrence?.Start ?? ImagePicker.DefaultAnchor, c.At,
                    c.Schedule is null ? 0 : ImagePicker.SeedFor(c.Schedule.Id));
                return new UpcomingItem(
                    c.At, c.At.ToString("HH:mm"),
                    c.Schedule?.Name ?? "Volta ao papel padrão",
                    c.Schedule is null ? "" : $"até {c.Occurrence!.Value.End:HH:mm}{(c.Occurrence.Value.End.Date != c.At.Date ? $" de {c.Occurrence.Value.End:dd/MM}" : "")}",
                    MonitorsText(g.Select(x => x.MonitorId)), image, c.Schedule?.Id, c.Occurrence?.Start);
            });

        Days.Clear();
        foreach (var day in items.GroupBy(i => i.At.Date))
            Days.Add(new UpcomingDay(DayHeader(day.Key, now), day.ToList()));
        OnPropertyChanged(nameof(IsEmpty));
    }

    [RelayCommand]
    private void Skip(UpcomingItem item)
    {
        if (item is { ScheduleId: { } id, OccurrenceStart: { } start })
            _scheduler.Skip(id, start);
    }

    private static string DayHeader(DateTime day, DateTime now) => (day - now.Date).Days switch
    {
        0 => $"Hoje · {day:dd/MM}",
        1 => $"Amanhã · {day:dd/MM}",
        _ => $"{day:dddd · dd/MM}",
    };
}
