using System.IO;
using System.Runtime.InteropServices;
using WinBgScheduler.App.Interop;
using WinBgScheduler.Core.Models;

namespace WinBgScheduler.App.Services;

public sealed record MonitorInfo(string Id, int Index, int Left, int Top, int Width, int Height, bool IsPrimary)
{
    public string DisplayName => $"Monitor {Index}{(IsPrimary ? " (principal)" : "")} · {Width}×{Height}";
}

public sealed record WallpaperAssignment(MonitorInfo Monitor, string ImagePath, FitMode Fit);

/// <summary>Reads monitors and sets wallpapers through IDesktopWallpaper.</summary>
public sealed class WallpaperService(ImageRenderService renderer)
{
    private string? _lastSignature;

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var found = new List<(string Id, Rect Rect)>();
        Use(dw =>
        {
            var count = dw.GetMonitorDevicePathCount();
            for (uint i = 0; i < count; i++)
            {
                var id = dw.GetMonitorDevicePathAt(i);
                try
                {
                    var rect = dw.GetMonitorRECT(id);
                    if (rect.Right > rect.Left && rect.Bottom > rect.Top)
                        found.Add((id, rect));
                }
                catch (COMException)
                {
                    // Known but disconnected monitor.
                }
            }
        });

        return found
            .OrderBy(m => m.Rect.Left).ThenBy(m => m.Rect.Top)
            .Select((m, i) => new MonitorInfo(
                m.Id, i + 1, m.Rect.Left, m.Rect.Top, m.Rect.Right - m.Rect.Left, m.Rect.Bottom - m.Rect.Top,
                m.Rect.Left <= 0 && m.Rect.Right > 0 && m.Rect.Top <= 0 && m.Rect.Bottom > 0))
            .ToList();
    }

    /// <summary>Applies the assignments. Returns false when nothing changed since the last call (unless forced).</summary>
    public bool Apply(IReadOnlyList<WallpaperAssignment> assignments, bool force = false)
    {
        var items = assignments.Where(a => File.Exists(a.ImagePath)).ToList();
        if (items.Count == 0)
            return false;

        var signature = string.Join("|", items.Select(a =>
            $"{a.Monitor.Id}>{a.ImagePath}>{a.Fit}>{File.GetLastWriteTimeUtc(a.ImagePath).Ticks}"));
        if (!force && signature == _lastSignature)
            return false;

        Use(dw =>
        {
            var span = items.FirstOrDefault(a => a.Fit == FitMode.Span);
            if (span is not null)
            {
                dw.SetPosition(DesktopWallpaperPosition.Span);
                dw.SetWallpaper(null, span.ImagePath);
                return;
            }

            var fits = items.Select(a => a.Fit).Distinct().ToList();
            if (fits.Count == 1)
            {
                dw.SetPosition(ToPosition(fits[0]));
                foreach (var a in items)
                    dw.SetWallpaper(a.Monitor.Id, a.ImagePath);
                return;
            }

            // IDesktopWallpaper has a single, global position. Mixed fits are pre-rendered at
            // each monitor's resolution and then shown with Fill.
            var background = dw.GetBackgroundColor();
            dw.SetPosition(DesktopWallpaperPosition.Fill);
            foreach (var a in items)
            {
                var path = a.Fit == FitMode.Fill
                    ? a.ImagePath
                    : renderer.Render(a.ImagePath, a.Fit, a.Monitor.Width, a.Monitor.Height, background);
                dw.SetWallpaper(a.Monitor.Id, path);
            }
        });

        _lastSignature = signature;
        return true;
    }

    public void ForgetLastApplied() => _lastSignature = null;

    /// <summary>What Windows is showing now, per monitor, with the global fit. Monitors without a picture are left out.</summary>
    public (FitMode Fit, IReadOnlyDictionary<MonitorInfo, string> Images) ReadCurrent(IReadOnlyList<MonitorInfo> monitors)
    {
        var fit = FitMode.Fill;
        var images = new Dictionary<MonitorInfo, string>();
        Use(dw =>
        {
            fit = ToFit(dw.GetPosition());
            foreach (var m in monitors)
            {
                var path = dw.GetWallpaper(m.Id);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    images[m] = path;
            }
        });
        return (fit, images);
    }

    private static FitMode ToFit(DesktopWallpaperPosition position) => position switch
    {
        DesktopWallpaperPosition.Fit => FitMode.Fit,
        DesktopWallpaperPosition.Stretch => FitMode.Stretch,
        DesktopWallpaperPosition.Tile => FitMode.Tile,
        DesktopWallpaperPosition.Center => FitMode.Center,
        DesktopWallpaperPosition.Span => FitMode.Span,
        _ => FitMode.Fill,
    };

    internal WallpaperSnapshot Capture(IReadOnlyList<MonitorInfo> monitors)
    {
        WallpaperSnapshot? snapshot = null;
        Use(dw => snapshot = new(dw.GetPosition(), monitors.ToDictionary(m => m.Id, m => dw.GetWallpaper(m.Id))));
        return snapshot!;
    }

    internal void Restore(WallpaperSnapshot snapshot)
    {
        Use(dw =>
        {
            dw.SetPosition(snapshot.Position);
            foreach (var (id, path) in snapshot.Wallpapers)
            {
                if (!string.IsNullOrEmpty(path))
                    dw.SetWallpaper(id, path);
            }
        });
        _lastSignature = null;
    }

    private static DesktopWallpaperPosition ToPosition(FitMode fit) => fit switch
    {
        FitMode.Fit => DesktopWallpaperPosition.Fit,
        FitMode.Stretch => DesktopWallpaperPosition.Stretch,
        FitMode.Tile => DesktopWallpaperPosition.Tile,
        FitMode.Center => DesktopWallpaperPosition.Center,
        FitMode.Span => DesktopWallpaperPosition.Span,
        _ => DesktopWallpaperPosition.Fill,
    };

    private static void Use(Action<IDesktopWallpaper> action)
    {
        var dw = (IDesktopWallpaper)new DesktopWallpaperClass();
        try
        {
            action(dw);
        }
        finally
        {
            Marshal.ReleaseComObject(dw);
        }
    }
}

internal sealed record WallpaperSnapshot(DesktopWallpaperPosition Position, IReadOnlyDictionary<string, string> Wallpapers);
