using CommunityToolkit.Mvvm.ComponentModel;
using WinBgScheduler.Core.Models;

namespace WinBgScheduler.App.ViewModels;

public sealed record Option<T>(T Value, string Label);

public static class Options
{
    public static readonly IReadOnlyList<Option<FitMode>> Fits =
    [
        new(FitMode.Fill, "Preencher"),
        new(FitMode.Fit, "Ajustar"),
        new(FitMode.Stretch, "Ampliar"),
        new(FitMode.Tile, "Lado a lado"),
        new(FitMode.Center, "Centralizar"),
        new(FitMode.Span, "Estender (todos os monitores)"),
    ];

    public static readonly IReadOnlyList<Option<RotationOrder>> Orders =
    [
        new(RotationOrder.Sequential, "Em sequência"),
        new(RotationOrder.Random, "Aleatória"),
    ];

    public static readonly IReadOnlyList<Option<RecurrenceKind>> Recurrences =
    [
        new(RecurrenceKind.Once, "Uma vez (data específica)"),
        new(RecurrenceKind.Daily, "Diariamente"),
        new(RecurrenceKind.Weekly, "Semanalmente"),
        new(RecurrenceKind.Monthly, "Mensalmente"),
        new(RecurrenceKind.Yearly, "Anualmente"),
        new(RecurrenceKind.DateRange, "Período (de/até)"),
    ];

    public static readonly IReadOnlyList<Option<int>> WeeksOfMonth =
    [
        new(1, "primeira"),
        new(2, "segunda"),
        new(3, "terceira"),
        new(4, "quarta"),
        new(-1, "última"),
    ];

    public static readonly IReadOnlyList<Option<DayOfWeek>> Weekdays =
    [
        new(DayOfWeek.Sunday, "domingo"),
        new(DayOfWeek.Monday, "segunda-feira"),
        new(DayOfWeek.Tuesday, "terça-feira"),
        new(DayOfWeek.Wednesday, "quarta-feira"),
        new(DayOfWeek.Thursday, "quinta-feira"),
        new(DayOfWeek.Friday, "sexta-feira"),
        new(DayOfWeek.Saturday, "sábado"),
    ];

    public static readonly IReadOnlyList<Option<int>> Months =
        Enumerable.Range(1, 12)
            .Select(m => new Option<int>(m, new DateTime(2000, m, 1).ToString("MMMM")))
            .ToList();

    public static readonly IReadOnlyList<Option<string>> Themes =
    [
        new("System", "Igual ao Windows"),
        new("Light", "Claro"),
        new("Dark", "Escuro"),
    ];
}

/// <summary>Swaps the page shown in the main window.</summary>
public sealed partial class Navigator : ObservableObject
{
    private readonly Stack<object> _history = new();

    [ObservableProperty]
    public partial object? Current { get; private set; }

    public void Reset(object page)
    {
        _history.Clear();
        Current = page;
    }

    public void Push(object page)
    {
        if (Current is not null)
            _history.Push(Current);
        Current = page;
    }

    public void Back()
    {
        if (_history.Count > 0)
            Current = _history.Pop();
    }
}
