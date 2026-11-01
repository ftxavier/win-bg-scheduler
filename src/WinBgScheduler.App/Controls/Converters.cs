using System.Globalization;
using System.Windows;
using System.Windows.Data;
using WinBgScheduler.App.Services;

namespace WinBgScheduler.App.Controls;

/// <summary>File path → thumbnail. ConverterParameter = decode width in pixels (default 160).</summary>
public sealed class PathToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        ImageLoader.Load(value as string, int.TryParse(parameter as string, out var w) ? w : 160);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the value is truthy (non-null, non-empty, true, non-zero). Parameter "invert" flips it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var truthy = value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            _ => true,
        };
        if (parameter as string == "invert")
            truthy = !truthy;
        return truthy ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>NumberBox works with double?; the view models use int.</summary>
public sealed class IntToDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? (double)i : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? (int)Math.Round(d) : 0;
}
