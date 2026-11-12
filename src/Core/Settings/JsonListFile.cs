using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Koy.Core.Diagnostics;

namespace Koy.Core.Settings;

/// <summary>
/// Ayar klasöründeki küçük bir JSON liste dosyası (sabit pano öğeleri, görevler). Okuma hatasında çökmez:
/// boş liste döner ve hata crash.log'a yazılır. Yazma geçici dosya + taşıma ile yapılır (yarım dosya kalmaz).
/// Kaynak üreticili tip bilgisi gerekir; yansımalı serileştirme yok (AGENTS.md #10).
/// </summary>
public sealed class JsonListFile<T>(string path, JsonTypeInfo<List<T>> typeInfo, Action<string, Exception>? onError = null)
{
    private readonly Action<string, Exception> _onError = onError ?? ((source, ex) => CrashLog.Write(source, ex));

    public string Path { get; } = path;

    /// <summary>Dosya yoksa, bozuksa ya da okunamazsa boş liste.</summary>
    public List<T> Load()
    {
        try
        {
            if (!File.Exists(Path))
                return [];

            using var stream = File.OpenRead(Path);
            return JsonSerializer.Deserialize(stream, typeInfo) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _onError("JsonListFile.Load " + System.IO.Path.GetFileName(Path), ex);
            return [];
        }
    }

    /// <summary>Başarılıysa true. Yazılamazsa (disk dolu, kilit) hata loglanır, çağıran çökmez.</summary>
    public bool Save(List<T> items)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, items, typeInfo);
            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _onError("JsonListFile.Save " + System.IO.Path.GetFileName(Path), ex);
            return false;
        }
    }
}

/// <summary>JsonListFile için tipten bağımsız yardımcılar.</summary>
public static class JsonListFile
{
    /// <summary>Ayar klasöründe (settings.json'ın yanı) dosya yolu.</summary>
    public static string InSettingsFolder(string fileName) => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(SettingsStore.DefaultPath)!, fileName);
}
