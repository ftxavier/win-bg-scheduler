using System.Runtime.InteropServices;

namespace WinBgScheduler.App.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct Rect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

internal enum DesktopWallpaperPosition
{
    Center = 0,
    Tile = 1,
    Stretch = 2,
    Fit = 3,
    Fill = 4,
    Span = 5,
}

/// <summary>shobjidl_core.h IDesktopWallpaper (Windows 8+).</summary>
[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);

    uint GetMonitorDevicePathCount();

    Rect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId);

    void SetBackgroundColor(uint color);

    uint GetBackgroundColor();

    void SetPosition(DesktopWallpaperPosition position);

    DesktopWallpaperPosition GetPosition();

    void SetSlideshow(IntPtr items);

    IntPtr GetSlideshow();

    void SetSlideshowOptions(uint options, uint slideshowTick);

    void GetSlideshowOptions(out uint options, out uint slideshowTick);

    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, uint direction);

    uint GetStatus();

    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

[ComImport]
[Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
internal class DesktopWallpaperClass
{
}
