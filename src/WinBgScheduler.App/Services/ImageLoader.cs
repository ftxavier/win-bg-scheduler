using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WinBgScheduler.App.Services;

/// <summary>Loads frozen bitmaps without locking files, with a small cache for thumbnails.</summary>
public static class ImageLoader
{
    private const int MaxCached = 300;
    private static readonly Dictionary<(string, int), BitmapSource> Cache = [];
    private static readonly Queue<(string, int)> Order = new();

    /// <summary>Decoded to <paramref name="decodeWidth"/> pixels wide (0 = full size). Null when unreadable.</summary>
    public static BitmapSource? Load(string? path, int decodeWidth)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        var key = (path, decodeWidth);
        if (Cache.TryGetValue(key, out var hit))
            return hit;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(path);
            if (decodeWidth > 0)
                image.DecodePixelWidth = decodeWidth;
            image.EndInit();
            image.Freeze();

            // Full-size images are only used once for rendering; don't keep them around.
            if (decodeWidth > 0)
            {
                Cache[key] = image;
                Order.Enqueue(key);
                if (Order.Count > MaxCached)
                    Cache.Remove(Order.Dequeue());
            }
            return image;
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or FileFormatException)
        {
            return null;
        }
    }

    public static BitmapSource LoadFull(string path) =>
        Load(path, 0) ?? throw new IOException($"Não foi possível ler {path}");

    /// <summary>Pixel size without decoding the whole image.</summary>
    public static Size? PixelSize(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
            return new Size(frame.PixelWidth, frame.PixelHeight);
        }
        catch (Exception e) when (e is IOException or NotSupportedException or UnauthorizedAccessException or ArgumentException or FileFormatException)
        {
            return null;
        }
    }
}
