using System.Text.Json.Serialization;

namespace WinBgScheduler.Core.Models;

/// <summary>What to show: one or more images (and/or folders), how to fit them and how to rotate.</summary>
public sealed class WallpaperContent
{
    public List<string> Images { get; set; } = [];

    public List<string> Folders { get; set; } = [];

    public bool IncludeSubfolders { get; set; }

    public FitMode Fit { get; set; } = FitMode.Fill;

    /// <summary>Minutes between images when there is more than one. 0 = no rotation (first image only).</summary>
    public int RotationMinutes { get; set; }

    public RotationOrder Order { get; set; } = RotationOrder.Sequential;

    [JsonIgnore]
    public bool IsEmpty => Images.Count == 0 && Folders.Count == 0;

    public WallpaperContent Clone()
    {
        var copy = (WallpaperContent)MemberwiseClone();
        copy.Images = [.. Images];
        copy.Folders = [.. Folders];
        return copy;
    }
}

/// <summary>Content targeted at one monitor, or at all monitors when <see cref="MonitorId"/> is null.</summary>
public sealed class MonitorAssignment
{
    public string? MonitorId { get; set; }

    public WallpaperContent Content { get; set; } = new();

    public MonitorAssignment Clone() => new() { MonitorId = MonitorId, Content = Content.Clone() };
}
