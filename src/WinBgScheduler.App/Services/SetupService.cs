using System.IO;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.Services;

/// <summary>First run: adopt the current wallpaper as the default, and install the app for the user.</summary>
public static class SetupService
{
    public static readonly string InstallFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinBgScheduler");

    public static readonly string InstalledExe = Path.Combine(InstallFolder, "WinBgScheduler.exe");

    private static readonly string ShortcutPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Win BG Scheduler.lnk");

    /// <summary>Published as a single exe (copyable). A dev build has its DLLs next to it and can't be copied alone.</summary>
    public static bool IsSingleFile => !File.Exists(Path.Combine(AppContext.BaseDirectory, "WinBgScheduler.dll"));

    public static bool IsInstalled =>
        string.Equals(Environment.ProcessPath, InstalledExe, StringComparison.OrdinalIgnoreCase);

    /// <summary>Default wallpaper assignments built from what Windows shows right now.</summary>
    public static List<MonitorAssignment> CaptureCurrentWallpaper(WallpaperService wallpaper, IReadOnlyList<MonitorInfo> monitors)
    {
        var (fit, images) = wallpaper.ReadCurrent(monitors);
        if (images.Count == 0)
            return [];

        var kept = images.ToDictionary(i => i.Key, i => Preserve(i.Value));
        if (kept.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            return [new() { Content = new() { Images = [kept.Values.First()], Fit = fit } }];

        // Different pictures per monitor; Span only makes sense for a single picture.
        var perMonitorFit = fit == FitMode.Span ? FitMode.Fill : fit;
        return kept.Select(i => new MonitorAssignment
        {
            MonitorId = i.Key.Id,
            Content = new() { Images = [i.Value], Fit = perMonitorFit },
        }).ToList();
    }

    /// <summary>
    /// Windows-managed copies (Themes\TranscodedWallpaper, Spotlight, etc.) get replaced over time,
    /// so keep our own copy of those. Regular user files are referenced where they are.
    /// </summary>
    private static string Preserve(string path)
    {
        var windowsManaged =
            path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase);
        if (!windowsManaged)
            return path;

        var folder = Path.Combine(ConfigStore.AppDataFolder, "wallpapers");
        Directory.CreateDirectory(folder);
        var extension = Path.GetExtension(path) is { Length: > 0 } ext ? ext : ".jpg";
        var target = Path.Combine(folder, $"original-{DateTime.Now:yyyyMMddHHmmss}-{Math.Abs(path.GetHashCode()):x}{extension}");
        File.Copy(path, target, overwrite: true);
        return target;
    }

    /// <summary>
    /// Copies the exe to %LocalAppData%\Programs (when published as a single file), adds a Start menu
    /// shortcut and registers it to start with Windows. Returns the exe that should keep running.
    /// </summary>
    public static string Install()
    {
        var exe = Environment.ProcessPath!;
        if (IsSingleFile && !IsInstalled)
        {
            Directory.CreateDirectory(InstallFolder);
            File.Copy(exe, InstalledExe, overwrite: true);
            exe = InstalledExe;
        }
        CreateShortcut(exe);
        StartupService.SetEnabled(true, exe);
        return exe;
    }

    private static void CreateShortcut(string exe)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell indisponível");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exe;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exe);
        shortcut.Description = "Agendador de papéis de parede";
        shortcut.IconLocation = exe + ",0";
        shortcut.Save();
    }
}
