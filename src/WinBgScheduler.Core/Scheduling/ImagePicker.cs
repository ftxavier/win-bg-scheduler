using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Scheduling;

/// <summary>Picks which image of a rotating content is shown at a given time. Deterministic: same inputs, same image.</summary>
public static class ImagePicker
{
    /// <summary>Rotation anchor for the default wallpaper, which has no occurrence start.</summary>
    public static readonly DateTime DefaultAnchor = new(2000, 1, 1);

    public static string? Pick(WallpaperContent content, IReadOnlyList<string> files, DateTime anchor, DateTime t, int seed)
    {
        if (files.Count == 0)
            return null;
        var slot = SlotAt(content, files.Count, anchor, t);
        if (content.Order == RotationOrder.Sequential)
            return files[(int)(slot % files.Count)];

        var cycle = slot / files.Count;
        var order = Shuffle(files.Count, unchecked(seed * 31 + (int)cycle));
        return files[order[(int)(slot % files.Count)]];
    }

    /// <summary>Next time the rotation advances after <paramref name="t"/>, or null when it doesn't rotate.</summary>
    public static DateTime? NextRotation(WallpaperContent content, int fileCount, DateTime anchor, DateTime t)
    {
        if (content.RotationMinutes <= 0 || fileCount < 2)
            return null;
        var slot = SlotAt(content, fileCount, anchor, t);
        return anchor.AddMinutes((slot + 1) * (double)content.RotationMinutes);
    }

    private static long SlotAt(WallpaperContent content, int fileCount, DateTime anchor, DateTime t)
    {
        if (content.RotationMinutes <= 0 || fileCount < 2 || t <= anchor)
            return 0;
        return (long)Math.Floor((t - anchor).TotalMinutes / content.RotationMinutes);
    }

    private static int[] Shuffle(int count, int seed)
    {
        var order = Enumerable.Range(0, count).ToArray();
        new Random(seed).Shuffle(order);
        return order;
    }

    public static int SeedFor(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0);
}
