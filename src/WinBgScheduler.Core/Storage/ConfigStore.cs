using System.Text.Json;
using System.Text.Json.Serialization;
using WinBgScheduler.Core.Models;

namespace WinBgScheduler.Core.Storage;

/// <summary>Loads and saves <see cref="AppConfig"/> as JSON, writing atomically.</summary>
public sealed class ConfigStore(string filePath)
{
    public static readonly string AppDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinBgScheduler");

    public static readonly string DefaultPath = Path.Combine(AppDataFolder, "config.json");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = filePath;

    public AppConfig Load()
    {
        if (!File.Exists(FilePath))
            return new AppConfig();
        try
        {
            using var stream = File.OpenRead(FilePath);
            return JsonSerializer.Deserialize<AppConfig>(stream, JsonOptions) ?? new AppConfig();
        }
        catch (JsonException)
        {
            // Keep the broken file for inspection instead of silently overwriting it.
            File.Copy(FilePath, $"{FilePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
