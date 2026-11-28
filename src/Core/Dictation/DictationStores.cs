using System.Text.Json;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;

namespace Koy.Core.Dictation;

/// <summary>Ayar klasöründe tek nesnelik JSON dosyası. Okuma hatasında varsayılan, yazma hatasında false; çökmez.</summary>
public sealed class DictationConfigStore(string? path = null)
{
    public string Path { get; } = path ?? JsonListFile.InSettingsFolder("dictation.json");

    public DictationConfig Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new DictationConfig();

            using var stream = File.OpenRead(Path);
            return Normalize(JsonSerializer.Deserialize(stream, DataJsonContext.Default.DictationConfig) ?? new DictationConfig());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            CrashLog.Write("DictationConfigStore.Load", ex);
            return new DictationConfig();
        }
    }

    public bool Save(DictationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, Normalize(config), DataJsonContext.Default.DictationConfig);
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write("DictationConfigStore.Save", ex);
            return false;
        }
    }

    /// <summary>Elle bozulmuş dosyada boş ya da saçma değerleri varsayılana çeker.</summary>
    public static DictationConfig Normalize(DictationConfig config)
    {
        var defaults = new DictationConfig();
        config.BaseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? defaults.BaseUrl : config.BaseUrl.Trim();
        config.TranscribeModel = string.IsNullOrWhiteSpace(config.TranscribeModel) ? defaults.TranscribeModel : config.TranscribeModel.Trim();
        config.CleanupModel = string.IsNullOrWhiteSpace(config.CleanupModel) ? defaults.CleanupModel : config.CleanupModel.Trim();
        config.Language = string.IsNullOrWhiteSpace(config.Language) ? "auto" : config.Language.Trim();
        config.MaxSeconds = Math.Clamp(config.MaxSeconds, 5, 3600);
        config.HistoryLimit = Math.Clamp(config.HistoryLimit, 0, 5000);
        config.SilenceDb = Math.Clamp(config.SilenceDb, -100, -20);
        config.SpeechMarginDb = Math.Clamp(config.SpeechMarginDb, 3, 40);
        config.MinVoicedSeconds = Math.Clamp(config.MinVoicedSeconds, 0.05, 3);
        config.DefaultProfile ??= new WritingProfile();
        config.Profiles ??= [];
        return config;
    }
}

/// <summary>
/// Sözlük dosyası (<c>dictation-dictionary.json</c>). Ayrı dosya ve ayrı kilit: öğrenme arka plan iş parçacığından yazar,
/// ayarlar penceresi ise bütün ayar dosyasını bir seferde yazar; tek dosyada biri ötekini ezerdi.
/// </summary>
public sealed class DictionaryStore(string? path = null)
{
    private readonly Lock _gate = new();

    public string Path { get; } = path ?? JsonListFile.InSettingsFolder("dictation-dictionary.json");

    public DictionaryFile Load()
    {
        lock (_gate)
            return ReadUnlocked();
    }

    public bool Save(DictionaryFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        lock (_gate)
            return WriteUnlocked(file);
    }

    /// <summary>Oku-değiştir-yaz tek kilit altında (öğrenme).</summary>
    public bool Update(Action<DictionaryFile> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            var file = ReadUnlocked();
            change(file);
            return WriteUnlocked(file);
        }
    }

    private DictionaryFile ReadUnlocked()
    {
        try
        {
            if (!File.Exists(Path))
                return new DictionaryFile();

            using var stream = File.OpenRead(Path);
            var file = JsonSerializer.Deserialize(stream, DataJsonContext.Default.DictionaryFile) ?? new DictionaryFile();
            file.Entries ??= [];
            file.Pending ??= [];
            return file;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            CrashLog.Write("DictionaryStore.Load", ex);
            return new DictionaryFile();
        }
    }

    private bool WriteUnlocked(DictionaryFile file)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, file, DataJsonContext.Default.DictionaryFile);
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CrashLog.Write("DictionaryStore.Save", ex);
            return false;
        }
    }
}
