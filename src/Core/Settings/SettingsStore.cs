using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koy.Core.Settings;

public sealed class SettingsStore(string path)
{
    public static string DefaultPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "settings.json");

    public string Path { get; } = path;

    /// <summary>
    /// v3 → v4 göçünün ilk çalışmasında eski dosyanın kopyası. v4 dosyasını eski sürüm (v0.2.6) okursa bütün düz alanları
    /// eksik görüp varsayılana döner ve ilk kayıtta ayarlar kalıcı gider; geri dönüşte bu dosya geri konur.
    /// </summary>
    public string BackupPath { get; } = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", "settings.v3.json");

    /// <summary>Dosya yoksa ya da JSON'u bozuksa varsayılan ayarları döner. Okuma sırasındaki G/Ç hataları (kilitli dosya vb.) yakalanmaz.</summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new AppSettings();

            JsonNode? node;
            using (var stream = File.OpenRead(Path))
                node = JsonNode.Parse(stream);

            if (node is not JsonObject root)
                return new AppSettings();

            // Göç tipli okumadan önce JSON ağacında yapılır: şema ya da değer biçimi değişse de dosya okunabilir kalır.
            var originalVersion = SettingsMigration.Migrate(root);
            if (originalVersion < AppSettings.CurrentVersion)
                BackupOriginalFile();

            return DeserializeTolerant(root).FillMissingSections();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }

    // Tanınmayan bir değer (örn. "Style": "Bogus") yalnız kendi alanını varsayılana döndürür; geri kalan ayarlar korunur.
    // Hata yolu JsonException.Path'ten okunur; yol çözülemezse o üst düzey bölüm düşer.
    private const int MaxRepairs = 200;

    private static AppSettings DeserializeTolerant(JsonObject root)
    {
        for (var attempt = 0; attempt < MaxRepairs; attempt++)
        {
            try
            {
                return root.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
            catch (JsonException ex) when (ex.Path is not null && DropAt(root, ex.Path))
            {
            }
        }
        return new AppSettings();
    }

    private static bool DropAt(JsonObject root, string path)
    {
        // "$.Displays.Monitors.DISPLAY1.x.NotchEnabled": sözlük anahtarları noktalı olabilir; her adımda en uzun
        // eşleşen anahtar seçilir. Yol çözülemezse üst düzey bölüm düşer.
        var parts = path.TrimStart('$').TrimStart('.').Split('.');
        if (parts.Any(p => p.Contains('[')))
            return root.Remove(parts[0].Split('[')[0]);

        var parent = root;
        var i = 0;
        while (i < parts.Length)
        {
            string? key = null;
            var end = -1;
            for (var j = parts.Length; j > i; j--)
            {
                var candidate = string.Join('.', parts[i..j]);
                if (parent.ContainsKey(candidate))
                {
                    key = candidate;
                    end = j;
                    break;
                }
            }

            if (key is null)
                return root.Remove(parts[0]);
            if (end == parts.Length)
                return parent.Remove(key);
            if (parent[key] is not JsonObject next)
                return root.Remove(parts[0]);

            parent = next;
            i = end;
        }
        return false;
    }

    // Yedek yazılamazsa (kilit, yetki) açılış durmasın; yedek bir kolaylıktır, ayarların kendisi değil.
    private void BackupOriginalFile()
    {
        try
        {
            if (!File.Exists(BackupPath))
                File.Copy(Path, BackupPath, overwrite: false);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Önce geçici dosyaya yazar, sonra yerine taşır; yazma yarıda kesilirse eski dosya bozulmaz.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        File.Move(temp, Path, overwrite: true);
    }
}
