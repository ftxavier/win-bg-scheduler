using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;
using WinBgScheduler.App.Services;

namespace WinBgScheduler.App.ViewModels;

public sealed record NavItem(string Title, SymbolRegular Icon, object Page);

public sealed partial class MainViewModel : ObservableObject
{
    public MainViewModel(
        Navigator navigator, SchedulerService scheduler, SchedulesViewModel schedules, UpcomingViewModel upcoming,
        DefaultsViewModel defaults, SettingsViewModel settings)
    {
        Navigator = navigator;
        Scheduler = scheduler;
        NavItems =
        [
            new("Agendamentos", SymbolRegular.CalendarClock24, schedules),
            new("Próximas trocas", SymbolRegular.Timer24, upcoming),
            new("Papel padrão", SymbolRegular.Image24, defaults),
            new("Configurações", SymbolRegular.Settings24, settings),
        ];
        SelectedNav = NavItems[0];
    }

    public Navigator Navigator { get; }

    public SchedulerService Scheduler { get; }

    public IReadOnlyList<NavItem> NavItems { get; }

    [ObservableProperty]
    public partial NavItem SelectedNav { get; set; }

    partial void OnSelectedNavChanged(NavItem value)
    {
        if (value.Page is UpcomingViewModel upcoming)
            upcoming.Refresh();
        Navigator.Reset(value.Page);
    }
}
