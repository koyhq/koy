using System.Text.Json;
using System.Text.Json.Nodes;
using Koy.Core.Diagnostics;

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

    // Antivirüs/senkron aracı dosyayı bir an tutabilir: kısa, sınırlı yeniden deneme (UI iş parçacığını uzun bloklamaz).
    private const int IoAttempts = 3;
    private const int IoRetryDelayMs = 75;

    // Yol başına son bilinen ayarlar (başarılı Load/Save). Kilitli dosyada varsayılana düşüp ayarları sıfırlamamak için.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, AppSettings> LastKnown =
        new(StringComparer.OrdinalIgnoreCase);

    private static string KeyOf(string path) => System.IO.Path.GetFullPath(path);

    /// <summary>
    /// Dosya yoksa ya da JSON'u bozuksa varsayılan ayarları döner. Kilitli/okunamayan dosyada (G/Ç, yetki) kısa süre
    /// yeniden dener, olmazsa hatayı crash.log'a yazar ve son bilinen ayarlarla (yoksa varsayılanla) devam eder;
    /// varsayılan diske yazılmaz, dosya bozulmaz.
    /// </summary>
    public AppSettings Load()
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var loaded = LoadOnce();
                LastKnown[KeyOf(Path)] = loaded;
                return loaded;
            }
            catch (JsonException)
            {
                return new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < IoAttempts)
                {
                    Thread.Sleep(IoRetryDelayMs);
                    continue;
                }
                CrashLog.Write("SettingsStore.Load " + System.IO.Path.GetFileName(Path), ex);
                return LastKnown.TryGetValue(KeyOf(Path), out var known) ? known : new AppSettings();
            }
        }
    }

    private AppSettings LoadOnce()
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

        return ParseTolerant(root, out _).FillMissingSections();
    }

    // Tanınmayan bir değer (örn. "Style": "Bogus") yalnız kendi alanını varsayılana döndürür; geri kalan ayarlar korunur.
    // Hata yolu JsonException.Path'ten okunur; yol çözülemezse o üst düzey bölüm düşer.
    private const int MaxRepairs = 200;

    /// <summary>
    /// Toleranslı okuma. <paramref name="dropped"/> kaç alanın varsayılana döndüğünü verir; içe aktarma bunu
    /// kullanarak geçersiz değer içeren dosyayı reddeder (dosyadan yalnız bu sayı 0 ise kabul edilir).
    /// </summary>
    internal static AppSettings ParseTolerant(JsonObject root, out int dropped)
    {
        dropped = 0;
        for (var attempt = 0; attempt < MaxRepairs; attempt++)
        {
            try
            {
                return root.Deserialize(SettingsJsonContext.Default.AppSettings) ?? new AppSettings();
            }
            catch (JsonException ex) when (ex.Path is not null && DropAt(root, ex.Path))
            {
                dropped++;
            }
        }
        dropped = MaxRepairs;
        return new AppSettings();
    }

    private static bool DropAt(JsonObject root, string path)
    {
        // "$.Displays.Monitors.DISPLAY1.x.NotchEnabled": sözlük anahtarları noktalı olabilir; her adımda en uzun
        // eşleşen anahtar seçilir. "$.DockItems.Folders[1].Id": yol bir listeden geçiyorsa en içteki liste öğesi düşer;
        // bozuk tek kayıt listenin geri kalanını ve bölümdeki diğer alanları götürmesin. Yol çözülemezse üst düzey bölüm düşer.
        var parts = path.TrimStart('$').TrimStart('.').Split('.');
        var top = parts[0].Split('[')[0];
        if (path.Contains("['", StringComparison.Ordinal))
            return root.Remove(top);

        JsonObject parent = root;
        (JsonArray Array, int Index)? innermost = null;
        var i = 0;
        while (i < parts.Length)
        {
            string? key = null;
            int[] indices = [];
            var end = -1;
            for (var j = parts.Length; j > i; j--)
            {
                var (name, idx) = SplitIndices(string.Join('.', parts[i..j]));
                if (name is not null && parent.ContainsKey(name))
                {
                    key = name;
                    indices = idx;
                    end = j;
                    break;
                }
            }

            if (key is null)
                return root.Remove(top);

            var node = parent[key];
            foreach (var index in indices)
            {
                if (node is not JsonArray array || index >= array.Count)
                    return root.Remove(top);
                innermost = (array, index);
                node = array[index];
            }

            if (end == parts.Length)
            {
                if (innermost is { } hit)
                {
                    hit.Array.RemoveAt(hit.Index);
                    return true;
                }
                return parent.Remove(key);
            }

            if (node is not JsonObject next)
                return innermost is { } at ? RemoveElement(at) : root.Remove(top);

            parent = next;
            i = end;
        }
        return false;

        static bool RemoveElement((JsonArray Array, int Index) at)
        {
            at.Array.RemoveAt(at.Index);
            return true;
        }
    }

    // "Folders[1][0]" → ("Folders", [1, 0]); indissiz ad olduğu gibi döner. Biçim bozuksa ad null olur.
    private static (string? Name, int[] Indices) SplitIndices(string segment)
    {
        var open = segment.IndexOf('[');
        if (open < 0)
            return (segment, []);

        var indices = new List<int>();
        var rest = segment.AsSpan(open);
        while (rest.Length > 0)
        {
            var close = rest.IndexOf(']');
            if (rest[0] != '[' || close < 2 || !int.TryParse(rest[1..close], System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var index))
                return (null, []);
            indices.Add(index);
            rest = rest[(close + 1)..];
        }
        return (segment[..open], indices.ToArray());
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

    // Art arda hatalar tek bildirime iner (kaydırıcı sürüklemesi her değişimde yazar).
    private static readonly SaveFailureStreak Streak = new();

    /// <summary>Son kayıt başarısız mı.</summary>
    public static bool IsSaveFailing => Streak.IsFailing;

    /// <summary>Kayıt başarısız olmaya başladı (art arda ilk hata). Arayüz iş parçacığında olmayabilir.</summary>
    public static event Action<Exception>? SaveFailed;

    /// <summary>Başarısızlık serisi bitti (kayıt yine çalışıyor).</summary>
    public static event Action? SaveRecovered;

    /// <summary>
    /// Kaydeder; G/Ç hatasında (kilit, salt okunur, disk dolu) fırlatmaz: hata crash.log'a yazılır ve
    /// <see cref="SaveFailed"/> tetiklenir. Sonucu bilmek isteyen <see cref="TrySave"/> kullanır.
    /// </summary>
    public void Save(AppSettings settings) => TrySave(settings, out _);

    /// <summary>
    /// Önce geçici dosyaya yazar, sonra yerine taşır; yazma yarıda kesilirse eski dosya bozulmaz. G/Ç ve yetki hatasında
    /// kısa yeniden dener; olmazsa false döner, .tmp kalıntısı silinir ve eski dosya olduğu gibi kalır.
    /// </summary>
    public bool TrySave(AppSettings settings, out Exception? error)
    {
        error = null;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                SaveOnce(settings);
                LastKnown[KeyOf(Path)] = settings;
                if (Streak.Report(success: true) == SaveFailureStreak.Change.Ended)
                    SaveRecovered?.Invoke();
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                DeleteTempQuietly();
                if (attempt < IoAttempts)
                {
                    Thread.Sleep(IoRetryDelayMs);
                    continue;
                }

                error = ex;
                if (Streak.Report(success: false) == SaveFailureStreak.Change.Started)
                {
                    CrashLog.Write("SettingsStore.Save " + System.IO.Path.GetFileName(Path), ex);
                    SaveFailed?.Invoke(ex);
                }
                return false;
            }
        }
    }

    private void SaveOnce(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
        var temp = Path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        File.Move(temp, Path, overwrite: true);
        RememberOwnWrite(Path);
    }

    private void DeleteTempQuietly()
    {
        try
        {
            File.Delete(Path + ".tmp");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // Koy'un kendi yazdığı dosyanın son yazma zamanı (yol başına; aynı dosyaya birden çok SettingsStore örneği yazar).
    // Canlı yeniden yükleme bununla kendi kaydını dışarıdan düzenlemeden ayırır (issue #69).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> OwnWrites =
        new(StringComparer.OrdinalIgnoreCase);

    private static void RememberOwnWrite(string path)
    {
        try
        {
            OwnWrites[System.IO.Path.GetFullPath(path)] = File.GetLastWriteTimeUtc(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Dosyanın şu anki hali Koy'un bu süreçte yaptığı son kayıt mı (dışarıdan düzenlenmemiş).</summary>
    public bool IsOwnWrite()
    {
        try
        {
            return File.Exists(Path)
                && OwnWrites.TryGetValue(System.IO.Path.GetFullPath(Path), out var own)
                && File.GetLastWriteTimeUtc(Path) == own;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Katı okuma (canlı yeniden yükleme için): dosya yoksa, JSON bozuksa ya da tek bir alan bile tanınmayıp varsayılana
    /// dönecekse false; o durumda son geçerli ayarlar korunur. Göç gerektiren (eski sürüm) dosya da reddedilir: göç ve
    /// yedek açılıştaki <see cref="Load"/>'un işidir. Kilitli dosya gibi G/Ç hataları false döner (yazma sürerken okunabilir).
    /// </summary>
    public bool TryReadStrict(out AppSettings settings)
    {
        settings = new AppSettings();
        try
        {
            if (!File.Exists(Path))
                return false;

            JsonNode? node;
            using (var stream = File.OpenRead(Path))
                node = JsonNode.Parse(stream);

            if (node is not JsonObject root)
                return false;

            var version = root["Version"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : 0;
            if (version != AppSettings.CurrentVersion)
                return false;

            var parsed = ParseTolerant(root, out var dropped);
            if (dropped > 0)
                return false;

            settings = parsed.FillMissingSections();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
