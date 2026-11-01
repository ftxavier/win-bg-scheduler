using Microsoft.Win32;

namespace WinBgScheduler.App.Services;

/// <summary>Starts the app with Windows through the current user's Run key.</summary>
public static class StartupService
{
    public const string TrayArgument = "--tray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Win BG Scheduler";

    /// <param name="exe">The exe to start; defaults to the running one.</param>
    public static void SetEnabled(bool enabled, string? exe = null)
    {
        exe ??= Environment.ProcessPath;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled && exe is not null)
            key.SetValue(ValueName, $"\"{exe}\" {TrayArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
