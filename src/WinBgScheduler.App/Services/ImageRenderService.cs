using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Scheduling;

namespace WinBgScheduler.App.Services;

/// <summary>Renders images to disk: fitted wallpapers for mixed-fit setups and small thumbnails for toasts.</summary>
public sealed class ImageRenderService
{
    public static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinBgScheduler", "cache");

    /// <param name="background">COLORREF (0x00BBGGRR) used where the image doesn't cover the monitor.</param>
    public string Render(string imagePath, FitMode fit, int width, int height, uint background)
    {
        var target = CachePath(imagePath, $"{fit}-{width}x{height}-{background:X}");
        if (File.Exists(target))
            return target;

        var source = ImageLoader.LoadFull(imagePath);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var color = Color.FromRgb((byte)(background & 0xFF), (byte)((background >> 8) & 0xFF), (byte)((background >> 16) & 0xFF));
            dc.DrawRectangle(new SolidColorBrush(color), null, new Rect(0, 0, width, height));
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            var placed = FitGeometry.Place(fit, source.PixelWidth, source.PixelHeight, width, height);
            if (fit == FitMode.Tile)
            {
                var brush = new ImageBrush(source)
                {
                    TileMode = TileMode.Tile,
                    ViewportUnits = BrushMappingMode.Absolute,
                    Viewport = new Rect(0, 0, placed.Width, placed.Height),
                    Stretch = Stretch.Fill,
                };
                dc.DrawRectangle(brush, null, new Rect(0, 0, width, height));
            }
            else
            {
                dc.DrawImage(source, new Rect(placed.X, placed.Y, placed.Width, placed.Height));
            }
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        Save(bitmap, target);
        return target;
    }

    /// <summary>A small JPEG (for toast notifications, which reject large images).</summary>
    public string Thumbnail(string imagePath, int width = 364)
    {
        var target = CachePath(imagePath, $"thumb-{width}");
        if (!File.Exists(target))
            Save(ImageLoader.Load(imagePath, width) ?? throw new IOException($"Não foi possível ler {imagePath}"), target);
        return target;
    }

    public void ClearCache()
    {
        if (!Directory.Exists(CacheFolder))
            return;
        foreach (var file in Directory.EnumerateFiles(CacheFolder))
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // In use as the current wallpaper; it will be cleaned next time.
            }
        }
    }

    private static string CachePath(string imagePath, string variant)
    {
        var stamp = File.GetLastWriteTimeUtc(imagePath).Ticks;
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{imagePath}|{stamp}|{variant}")));
        Directory.CreateDirectory(CacheFolder);
        return Path.Combine(CacheFolder, hash[..20] + ".jpg");
    }

    private static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var temp = path + ".tmp";
        using (var stream = File.Create(temp))
            encoder.Save(stream);
        File.Move(temp, path, overwrite: true);
    }
}
