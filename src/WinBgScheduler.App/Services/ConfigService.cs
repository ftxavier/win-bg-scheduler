using System.IO;
using WinBgScheduler.Core.Models;
using WinBgScheduler.Core.Storage;

namespace WinBgScheduler.App.Services;

/// <summary>The single in-memory config. Only touched from the UI thread.</summary>
public sealed class ConfigService
{
    private readonly ConfigStore _store = new(ConfigStore.DefaultPath);

    public ConfigService()
    {
        IsFirstRun = !File.Exists(_store.FilePath);
        Config = _store.Load();
    }

    public AppConfig Config { get; }

    /// <summary>No config existed when the app started.</summary>
    public bool IsFirstRun { get; }

    public event EventHandler? Changed;

    public void Save()
    {
        _store.Save(Config);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
