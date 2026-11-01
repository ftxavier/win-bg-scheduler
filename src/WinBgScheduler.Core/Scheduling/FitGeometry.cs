using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Scheduling;

public readonly record struct PlacedRect(double X, double Y, double Width, double Height);

/// <summary>Where an image lands on a monitor for each fit mode (used by preview and pre-rendering).</summary>
public static class FitGeometry
{
    /// <summary>For <see cref="FitMode.Tile"/> the rect is the first tile at the origin.</summary>
    public static PlacedRect Place(FitMode fit, double imageWidth, double imageHeight, double monitorWidth, double monitorHeight)
    {
        double scale;
        switch (fit)
        {
            case FitMode.Stretch:
                return new(0, 0, monitorWidth, monitorHeight);
            case FitMode.Tile:
                return new(0, 0, imageWidth, imageHeight);
            case FitMode.Center:
                scale = 1;
                break;
            case FitMode.Fit:
                scale = Math.Min(monitorWidth / imageWidth, monitorHeight / imageHeight);
                break;
            default: // Fill, Span (per monitor)
                scale = Math.Max(monitorWidth / imageWidth, monitorHeight / imageHeight);
                break;
        }
        var w = imageWidth * scale;
        var h = imageHeight * scale;
        return new((monitorWidth - w) / 2, (monitorHeight - h) / 2, w, h);
    }
}
