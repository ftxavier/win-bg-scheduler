using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Storage;

/// <summary>Expands a content's images and folders into existing image files, with a short-lived cache.</summary>
public sealed class ContentFiles(TimeProvider time)
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp",
    };

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(1);

    private readonly Dictionary<string, (DateTimeOffset At, IReadOnlyList<string> Files)> _cache = [];

    public IReadOnlyList<string> Resolve(WallpaperContent content)
    {
        var key = string.Join('|', content.Images) + "#" + string.Join('|', content.Folders) + content.IncludeSubfolders;
        var now = time.GetUtcNow();
        if (_cache.TryGetValue(key, out var hit) && now - hit.At < CacheLifetime)
            return hit.Files;

        var files = Enumerate(content).ToList();
        _cache[key] = (now, files);
        return files;
    }

    public void Invalidate() => _cache.Clear();

    public static bool IsImage(string path) => Extensions.Contains(Path.GetExtension(path));

    private static IEnumerable<string> Enumerate(WallpaperContent content)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in content.Images)
        {
            if (File.Exists(image) && seen.Add(image))
                yield return image;
        }

        var option = content.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        foreach (var folder in content.Folders.Where(Directory.Exists))
        {
            IEnumerable<string> found;
            try
            {
                found = Directory.EnumerateFiles(folder, "*", option).Where(IsImage).Order(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in found)
            {
                if (seen.Add(file))
                    yield return file;
            }
        }
    }
}
