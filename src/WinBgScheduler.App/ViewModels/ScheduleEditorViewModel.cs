using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

public sealed partial class DayToggle(DayOfWeek day, string label, bool isChecked) : ObservableObject
{
    public DayOfWeek Day { get; } = day;

    public string Label { get; } = label;

    public string FullName => Options.Weekdays[(int)Day].Label;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}

/// <summary>Edits a copy of a schedule; nothing is stored until Save.</summary>
public sealed partial class ScheduleEditorViewModel : ObservableObject
{
    private static readonly string[] TimeFormats = ["H:mm", "HH:mm", "H", "HHmm", "H\\hmm", "H\\h"];
    private static readonly string[] DayLabels = ["D", "S", "T", "Q", "Q", "S", "S"];

    private readonly Schedule _s;
    private readonly bool _isNew;
    private readonly ConfigService _config;
    private readonly SchedulerService _scheduler;
    private readonly Navigator _navigator;
    private string _startText;
    private string _endText;

    public ScheduleEditorViewModel(
        Schedule schedule, bool isNew, ConfigService config, SchedulerService scheduler, ContentFiles files, Navigator navigator)
    {
        _s = schedule;
        _isNew = isNew;
        _config = config;
        _scheduler = scheduler;
        _navigator = navigator;
        _startText = _s.Window.Start.ToString("HH:mm");
        _endText = _s.Window.End.ToString("HH:mm");

        Content = new ContentEditorViewModel(_s.Assignments, scheduler.Monitors, files);
        Content.ContentChanged += (_, _) => Refresh();

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var toggle = new DayToggle(day, DayLabels[(int)day], _s.Recurrence.DaysOfWeek.Contains(day));
            toggle.PropertyChanged += (_, _) =>
            {
                _s.Recurrence.DaysOfWeek = Days.Where(d => d.IsChecked).Select(d => d.Day).ToList();
                Refresh();
            };
            Days.Add(toggle);
        }
        foreach (var d in _s.Exceptions.Order())
            Exceptions.Add(d.ToDateTime(TimeOnly.MinValue));

        Refresh();
    }

    public string Title => _isNew ? "Novo agendamento" : "Editar agendamento";

    public ContentEditorViewModel Content { get; }

    public ObservableCollection<DayToggle> Days { get; } = [];

    public ObservableCollection<DateTime> Exceptions { get; } = [];

    public ObservableCollection<string> NextOccurrences { get; } = [];

    public IReadOnlyList<Option<RecurrenceKind>> RecurrenceOptions => Options.Recurrences;

    public IReadOnlyList<Option<int>> WeekOfMonthOptions => Options.WeeksOfMonth;

    public IReadOnlyList<Option<DayOfWeek>> WeekdayOptions => Options.Weekdays;

    public IReadOnlyList<Option<int>> MonthOptions => Options.Months;

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    [ObservableProperty]
    public partial string? Error { get; private set; }

    [ObservableProperty]
    public partial DateTime? NewException { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    public partial bool IsTesting { get; private set; }

    public string Name
    {
        get => _s.Name;
        set => Set(() => _s.Name = value);
    }

    public int NotifyMinutesBefore
    {
        get => _s.NotifyMinutesBefore;
        set => Set(() => _s.NotifyMinutesBefore = Math.Clamp(value, 0, 1440));
    }

    // ---- Recurrence -------------------------------------------------------------------------

    public RecurrenceKind Kind
    {
        get => _s.Recurrence.Kind;
        set
        {
            Set(() => _s.Recurrence.Kind = value);
            OnPropertyChanged(nameof(IsOnce));
            OnPropertyChanged(nameof(IsDaily));
            OnPropertyChanged(nameof(IsWeekly));
            OnPropertyChanged(nameof(IsMonthly));
            OnPropertyChanged(nameof(IsYearly));
            OnPropertyChanged(nameof(IsDateRange));
            OnPropertyChanged(nameof(HasInterval));
            OnPropertyChanged(nameof(IntervalUnit));
            OnPropertyChanged(nameof(HasValidity));
        }
    }

    public bool IsOnce => Kind == RecurrenceKind.Once;
    public bool IsDaily => Kind == RecurrenceKind.Daily;
    public bool IsWeekly => Kind == RecurrenceKind.Weekly;
    public bool IsMonthly => Kind == RecurrenceKind.Monthly;
    public bool IsYearly => Kind == RecurrenceKind.Yearly;
    public bool IsDateRange => Kind == RecurrenceKind.DateRange;
    public bool HasInterval => Kind is RecurrenceKind.Daily or RecurrenceKind.Weekly or RecurrenceKind.Monthly;
    public bool HasValidity => Kind is not (RecurrenceKind.Once or RecurrenceKind.DateRange);

    public string IntervalUnit => Kind switch
    {
        RecurrenceKind.Daily => "dia(s)",
        RecurrenceKind.Weekly => "semana(s)",
        _ => "mês(es)",
    };

    public int Interval
    {
        get => _s.Recurrence.Interval;
        set => Set(() => _s.Recurrence.Interval = Math.Max(1, value));
    }

    public DateTime? OnceDate
    {
        get => ToDateTime(_s.Recurrence.Date);
        set => Set(() => _s.Recurrence.Date = ToDateOnly(value) ?? _s.Recurrence.Date);
    }

    public DateTime? StartDate
    {
        get => ToDateTime(_s.Recurrence.StartDate);
        set => Set(() => _s.Recurrence.StartDate = ToDateOnly(value) ?? _s.Recurrence.StartDate);
    }

    public bool HasEndDate
    {
        get => _s.Recurrence.EndDate is not null;
        set
        {
            Set(() => _s.Recurrence.EndDate = value ? _s.Recurrence.EndDate ?? _s.Recurrence.StartDate.AddMonths(1) : null);
            OnPropertyChanged(nameof(EndDate));
        }
    }

    public DateTime? EndDate
    {
        get => ToDateTime(_s.Recurrence.EndDate);
        set => Set(() => _s.Recurrence.EndDate = ToDateOnly(value));
    }

    public bool MonthlyByDay
    {
        get => _s.Recurrence.MonthlyMode == MonthlyMode.DayOfMonth;
        set
        {
            Set(() => _s.Recurrence.MonthlyMode = value ? MonthlyMode.DayOfMonth : MonthlyMode.NthWeekday);
            OnPropertyChanged(nameof(MonthlyByWeekday));
        }
    }

    public bool MonthlyByWeekday
    {
        get => !MonthlyByDay;
        set => MonthlyByDay = !value;
    }

    public int DayOfMonth
    {
        get => _s.Recurrence.DayOfMonth;
        set => Set(() => _s.Recurrence.DayOfMonth = Math.Clamp(value, 1, 31));
    }

    public int WeekOfMonth
    {
        get => _s.Recurrence.WeekOfMonth;
        set => Set(() => _s.Recurrence.WeekOfMonth = value);
    }

    public DayOfWeek Weekday
    {
        get => _s.Recurrence.Weekday;
        set => Set(() => _s.Recurrence.Weekday = value);
    }

    public int Month
    {
        get => _s.Recurrence.Month;
        set => Set(() => _s.Recurrence.Month = value);
    }

    public DateTime? RangeStart
    {
        get => ToDateTime(_s.Recurrence.RangeStart);
        set => Set(() => _s.Recurrence.RangeStart = ToDateOnly(value) ?? _s.Recurrence.RangeStart);
    }

    public DateTime? RangeEnd
    {
        get => ToDateTime(_s.Recurrence.RangeEnd);
        set => Set(() => _s.Recurrence.RangeEnd = ToDateOnly(value) ?? _s.Recurrence.RangeEnd);
    }

    // ---- Time window ------------------------------------------------------------------------

    public bool IsAllDay
    {
        get => _s.Window.Kind == TimeWindowKind.AllDay;
        set { if (value) SetWindowKind(TimeWindowKind.AllDay); }
    }

    public bool IsRange
    {
        get => _s.Window.Kind == TimeWindowKind.Range;
        set { if (value) SetWindowKind(TimeWindowKind.Range); }
    }

    public bool IsDuration
    {
        get => _s.Window.Kind == TimeWindowKind.Duration;
        set { if (value) SetWindowKind(TimeWindowKind.Duration); }
    }

    public string StartTimeText
    {
        get => _startText;
        set
        {
            _startText = value;
            if (ParseTime(value) is { } t)
                _s.Window.Start = t;
            Set(() => { });
        }
    }

    public string EndTimeText
    {
        get => _endText;
        set
        {
            _endText = value;
            if (ParseTime(value) is { } t)
                _s.Window.End = t;
            Set(() => { });
        }
    }

    public int DurationMinutes
    {
        get => _s.Window.DurationMinutes;
        set => Set(() => _s.Window.DurationMinutes = Math.Clamp(value, 1, TimeWindow.MaxDurationMinutes));
    }

    private void SetWindowKind(TimeWindowKind kind)
    {
        _s.Window.Kind = kind;
        OnPropertyChanged(nameof(IsAllDay));
        OnPropertyChanged(nameof(IsRange));
        OnPropertyChanged(nameof(IsDuration));
        Refresh();
    }

    // ---- Commands ---------------------------------------------------------------------------

    [RelayCommand]
    private void AddException()
    {
        if (NewException is not { } date || Exceptions.Contains(date.Date))
            return;
        _s.Exceptions.Add(DateOnly.FromDateTime(date));
        var index = Exceptions.TakeWhile(d => d < date.Date).Count();
        Exceptions.Insert(index, date.Date);
        NewException = null;
        Refresh();
    }

    [RelayCommand]
    private void RemoveException(DateTime date)
    {
        _s.Exceptions.Remove(DateOnly.FromDateTime(date));
        Exceptions.Remove(date);
        Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        if (Content.PreviewPath is not { } path)
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

    private bool CanTest() => !IsTesting;

    [RelayCommand]
    private void Save()
    {
        if (Validate() is { } error)
        {
            Error = error;
            return;
        }

        _s.Assignments.RemoveAll(a => a.Content.IsEmpty);
        var schedules = _config.Config.Schedules;
        var index = schedules.FindIndex(x => x.Id == _s.Id);
        if (index >= 0)
            schedules[index] = _s;
        else
            schedules.Add(_s);
        _config.Save();
        _navigator.Back();
    }

    [RelayCommand]
    private void Cancel() => _navigator.Back();

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(_s.Name))
            return "Dê um nome ao agendamento.";
        if (_s.Assignments.All(a => a.Content.IsEmpty))
            return "Escolha pelo menos uma imagem ou pasta.";
        if (Kind == RecurrenceKind.Weekly && _s.Recurrence.DaysOfWeek.Count == 0)
            return "Marque pelo menos um dia da semana.";
        if (Kind == RecurrenceKind.DateRange && _s.Recurrence.RangeEnd < _s.Recurrence.RangeStart)
            return "A data final do período é anterior à inicial.";
        if (HasValidity && _s.Recurrence.EndDate < _s.Recurrence.StartDate)
            return "A data de término é anterior à de início.";
        if (_s.Window.Kind != TimeWindowKind.AllDay && ParseTime(_startText) is null)
            return "Horário de início inválido (use HH:mm).";
        if (_s.Window.Kind == TimeWindowKind.Range && ParseTime(_endText) is null)
            return "Horário de término inválido (use HH:mm).";
        if (_s.Window.Kind == TimeWindowKind.Range && _s.Window.Start == _s.Window.End)
            return "O horário de início e de término são iguais.";
        return null;
    }

    // ---- Helpers ----------------------------------------------------------------------------

    private void Set(Action apply, [CallerMemberName] string? property = null)
    {
        apply();
        OnPropertyChanged(property);
        Refresh();
    }

    private void Refresh()
    {
        Summary = ScheduleDescriber.Describe(_s);
        Error = null;

        NextOccurrences.Clear();
        var now = _scheduler.Now;
        foreach (var o in _s.OccurrencesBetween(now, now.AddYears(2)).Take(5))
        {
            var span = o.End.Date == o.Start.Date || o.End == o.Start.Date.AddDays(1)
                ? $"{o.Start:HH:mm}–{(o.End == o.Start.Date.AddDays(1) ? "24:00" : o.End.ToString("HH:mm"))}"
                : $"{o.Start:HH:mm} até {o.End:ddd dd/MM HH:mm}";
            NextOccurrences.Add($"{o.Start:ddd, dd/MM/yyyy} · {span}");
        }
        if (NextOccurrences.Count == 0)
            NextOccurrences.Add("Nenhuma ocorrência futura");
    }

    private static TimeOnly? ParseTime(string text) =>
        TimeOnly.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

    private static DateTime? ToDateTime(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? ToDateOnly(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : null;
}
