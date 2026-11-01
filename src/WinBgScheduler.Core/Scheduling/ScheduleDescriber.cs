using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Scheduling;

/// <summary>Human-readable (pt-BR) summaries such as "Toda quarta-feira, das 19:00 às 21:00".</summary>
public static class ScheduleDescriber
{
    private static readonly string[] DayNames =
        ["domingo", "segunda-feira", "terça-feira", "quarta-feira", "quinta-feira", "sexta-feira", "sábado"];

    private static readonly string[] ShortDayNames = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    private static readonly string[] Ordinals = ["", "primeira", "segunda", "terceira", "quarta"];

    public static string Describe(Schedule schedule) =>
        $"{Describe(schedule.Recurrence)}, {Describe(schedule.Window)}";

    public static string Describe(Recurrence r)
    {
        var every = Math.Max(1, r.Interval);
        return r.Kind switch
        {
            RecurrenceKind.Once => $"Em {r.Date:dd/MM/yyyy}",
            RecurrenceKind.DateRange => $"De {r.RangeStart:dd/MM/yyyy} a {r.RangeEnd:dd/MM/yyyy}",
            RecurrenceKind.Daily => every == 1 ? "Todos os dias" : $"A cada {every} dias",
            RecurrenceKind.Weekly => DescribeWeekly(r.DaysOfWeek, every),
            RecurrenceKind.Monthly => DescribeMonthly(r, every),
            RecurrenceKind.Yearly => $"Todo ano em {r.DayOfMonth:00}/{r.Month:00}",
            _ => "",
        };
    }

    public static string Describe(TimeWindow w) => w.Kind switch
    {
        TimeWindowKind.AllDay => "o dia todo",
        TimeWindowKind.Range => $"das {w.Start:HH\\:mm} às {w.End:HH\\:mm}",
        _ => $"às {w.Start:HH\\:mm} por {FormatMinutes(w.DurationMinutes)}",
    };

    public static string FormatMinutes(int minutes)
    {
        if (minutes < 60)
            return $"{minutes} min";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h} h" : $"{h} h {m} min";
    }

    private static string DescribeWeekly(List<DayOfWeek> days, int every)
    {
        var sorted = days.Distinct().Order().ToList();
        string text;
        if (sorted.Count == 0)
            text = "Nenhum dia selecionado";
        else if (sorted.Count == 7)
            text = "Todos os dias";
        else if (sorted.SequenceEqual([DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]))
            text = "Dias úteis";
        else if (sorted.SequenceEqual([DayOfWeek.Sunday, DayOfWeek.Saturday]))
            text = "Fins de semana";
        else if (sorted.Count == 1)
            text = $"{(sorted[0] is DayOfWeek.Saturday or DayOfWeek.Sunday ? "Todo" : "Toda")} {DayNames[(int)sorted[0]]}";
        else
            text = Capitalize(JoinPt(sorted.Select(d => ShortDayNames[(int)d]).ToList()));

        return every == 1 ? text : $"A cada {every} semanas: {Uncapitalize(text)}";
    }

    private static string DescribeMonthly(Recurrence r, int every)
    {
        var period = every == 1 ? "do mês" : $"a cada {every} meses";
        if (r.MonthlyMode == MonthlyMode.DayOfMonth)
            return $"Todo dia {r.DayOfMonth} {period}";
        var ordinal = r.WeekOfMonth < 0 ? "última" : Ordinals[Math.Clamp(r.WeekOfMonth, 1, 4)];
        // "sábado"/"domingo" are masculine
        if (r.Weekday is DayOfWeek.Saturday or DayOfWeek.Sunday)
            ordinal = r.WeekOfMonth < 0 ? "último" : ordinal[..^1] + "o";
        var article = r.Weekday is DayOfWeek.Saturday or DayOfWeek.Sunday ? "No" : "Na";
        return $"{article} {ordinal} {DayNames[(int)r.Weekday]} {period}";
    }

    private static string JoinPt(List<string> items) =>
        items.Count == 1 ? items[0] : $"{string.Join(", ", items.Take(items.Count - 1))} e {items[^1]}";

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private static string Uncapitalize(string s) => s.Length == 0 ? s : char.ToLower(s[0]) + s[1..];
}
