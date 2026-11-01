using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinBgScheduler.App.Services;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.ViewModels;

public sealed partial class TargetOption(string? monitorId, string label) : ObservableObject
{
    public string? MonitorId { get; } = monitorId;

    public string Label { get; } = label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    public partial bool HasContent { get; set; }

    public string DisplayLabel => HasContent ? $"{Label}  •" : Label;
}

public sealed record ContentEntry(string Path, bool IsFolder)
{
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\')) is { Length: > 0 } n ? n : Path;
}

/// <summary>Edits the images, fit and rotation of a list of per-monitor assignments.</summary>
public sealed partial class ContentEditorViewModel : ObservableObject
{
    private readonly List<MonitorAssignment> _assignments;
    private readonly IReadOnlyList<MonitorInfo> _monitors;
    private readonly ContentFiles _files;
    private MonitorAssignment _current = new();

    public ContentEditorViewModel(List<MonitorAssignment> assignments, IReadOnlyList<MonitorInfo> monitors, ContentFiles files)
    {
        _assignments = assignments;
        _monitors = monitors;
        _files = files;

        Targets.Add(new TargetOption(null, "Todos os monitores"));
        if (monitors.Count > 1)
        {
            foreach (var m in monitors)
                Targets.Add(new TargetOption(m.Id, m.DisplayName));
        }
        foreach (var a in assignments.Where(a => a.MonitorId is not null && monitors.All(m => m.Id != a.MonitorId)))
            Targets.Add(new TargetOption(a.MonitorId, "Monitor desconectado"));
        foreach (var t in Targets)
            t.HasContent = Find(t.MonitorId) is { Content.IsEmpty: false };

        SelectedTarget = Targets[0];
    }

    public event EventHandler? ContentChanged;

    public ObservableCollection<TargetOption> Targets { get; } = [];

    public ObservableCollection<ContentEntry> Entries { get; } = [];

    public IReadOnlyList<Option<FitMode>> FitOptions => Options.Fits;

    public IReadOnlyList<Option<RotationOrder>> OrderOptions => Options.Orders;

    public bool ShowTargets => Targets.Count > 1;

    [ObservableProperty]
    public partial TargetOption SelectedTarget { get; set; }

    [ObservableProperty]
    public partial ContentEntry? SelectedEntry { get; set; }

    [ObservableProperty]
    public partial string? PreviewPath { get; private set; }

    public WallpaperContent Content => _current.Content;

    public FitMode Fit
    {
        get => Content.Fit;
        set => Update(() => Content.Fit = value);
    }

    public int RotationMinutes
    {
        get => Content.RotationMinutes;
        set => Update(() => Content.RotationMinutes = Math.Max(0, value));
    }

    public RotationOrder Order
    {
        get => Content.Order;
        set => Update(() => Content.Order = value);
    }

    public bool IncludeSubfolders
    {
        get => Content.IncludeSubfolders;
        set => Update(() => Content.IncludeSubfolders = value);
    }

    public int FileCount => _files.Resolve(Content).Count;

    public bool CanRotate => FileCount > 1;

    public bool IsEmpty => Content.IsEmpty;

    public string FilesSummary => FileCount switch
    {
        0 when !Content.IsEmpty => "Nenhuma imagem encontrada",
        0 => SelectedTarget.MonitorId is null ? "Nenhuma imagem escolhida" : "Usa a configuração de \"Todos os monitores\"",
        1 => "1 imagem",
        var n => $"{n} imagens",
    };

    /// <summary>Monitor shape for the preview: the selected monitor, else the primary one, else 16:9.</summary>
    public double PreviewMonitorWidth => PreviewMonitor?.Width ?? 1920;

    public double PreviewMonitorHeight => PreviewMonitor?.Height ?? 1080;

    private MonitorInfo? PreviewMonitor =>
        _monitors.FirstOrDefault(m => m.Id == SelectedTarget.MonitorId)
        ?? _monitors.FirstOrDefault(m => m.IsPrimary)
        ?? _monitors.FirstOrDefault();

    partial void OnSelectedTargetChanged(TargetOption value)
    {
        _current = Find(value.MonitorId) ?? AddAssignment(value.MonitorId);
        Entries.Clear();
        foreach (var image in Content.Images)
            Entries.Add(new ContentEntry(image, false));
        foreach (var folder in Content.Folders)
            Entries.Add(new ContentEntry(folder, true));
        SelectedEntry = Entries.FirstOrDefault();
        RefreshAll();
    }

    partial void OnSelectedEntryChanged(ContentEntry? value) => UpdatePreview();

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Escolher imagens",
            Multiselect = true,
            Filter = "Imagens|" + string.Join(";", ContentFiles.Extensions.Select(e => "*" + e)) + "|Todos os arquivos|*.*",
        };
        if (dialog.ShowDialog() == true)
            AddPaths(dialog.FileNames);
    }

    [RelayCommand]
    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Escolher pasta de imagens", Multiselect = true };
        if (dialog.ShowDialog() == true)
            AddPaths(dialog.FolderNames);
    }

    [RelayCommand]
    private void RemoveEntry(ContentEntry? entry)
    {
        if (entry is null)
            return;
        Content.Images.Remove(entry.Path);
        Content.Folders.Remove(entry.Path);
        Entries.Remove(entry);
        if (SelectedEntry is null)
            SelectedEntry = Entries.FirstOrDefault();
        Changed();
    }

    [RelayCommand]
    private void ClearEntries()
    {
        Content.Images.Clear();
        Content.Folders.Clear();
        Entries.Clear();
        Changed();
    }

    /// <summary>Adds files and folders (from dialogs or drag and drop).</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        ContentEntry? last = null;
        foreach (var path in paths)
        {
            if (Directory.Exists(path) && !Content.Folders.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                Content.Folders.Add(path);
                Entries.Add(last = new ContentEntry(path, true));
            }
            else if (File.Exists(path) && ContentFiles.IsImage(path) && !Content.Images.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                Content.Images.Add(path);
                Entries.Insert(Content.Images.Count - 1, last = new ContentEntry(path, false));
            }
        }
        if (last is not null)
            SelectedEntry = last;
        Changed();
    }

    private MonitorAssignment? Find(string? monitorId) => _assignments.FirstOrDefault(a => a.MonitorId == monitorId);

    private MonitorAssignment AddAssignment(string? monitorId)
    {
        var assignment = new MonitorAssignment { MonitorId = monitorId };
        _assignments.Add(assignment);
        return assignment;
    }

    private void Update(Action apply)
    {
        apply();
        Changed();
    }

    private void Changed()
    {
        _files.Invalidate();
        SelectedTarget.HasContent = !Content.IsEmpty;
        RefreshAll();
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshAll()
    {
        OnPropertyChanged(nameof(Fit));
        OnPropertyChanged(nameof(RotationMinutes));
        OnPropertyChanged(nameof(Order));
        OnPropertyChanged(nameof(IncludeSubfolders));
        OnPropertyChanged(nameof(FileCount));
        OnPropertyChanged(nameof(CanRotate));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(FilesSummary));
        OnPropertyChanged(nameof(PreviewMonitorWidth));
        OnPropertyChanged(nameof(PreviewMonitorHeight));
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        PreviewPath = SelectedEntry switch
        {
            { IsFolder: false } e => e.Path,
            { IsFolder: true } e => _files.Resolve(new WallpaperContent { Folders = [e.Path], IncludeSubfolders = Content.IncludeSubfolders }).FirstOrDefault(),
            null => _files.Resolve(Content).FirstOrDefault(),
        };
    }
}
