using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace WinBgScheduler.App.Services;

public static class ThemeHelper
{
    /// <param name="theme">"System", "Light" or "Dark".</param>
    public static void Apply(string theme)
    {
        var applied = theme switch
        {
            "Light" => ApplicationTheme.Light,
            "Dark" => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
        };
        ApplicationThemeManager.Apply(applied, WindowBackdropType.Mica, true);
    }
}
