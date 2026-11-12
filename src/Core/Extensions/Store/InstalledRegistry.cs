using System.Text.Json;
using System.Text.Json.Serialization;
using Koy.Core.Diagnostics;

namespace Koy.Core.Extensions.Store;

/// <summary>Eklenti nereden kuruldu.</summary>
public enum ExtensionSource
{
    Store,
    File,
}

/// <summary>
/// Kayıttaki bir kurulu eklenti (<c>extensions.json</c>). Kurulum klasörü kimlikten türer
/// (<see cref="ExtensionPaths.FolderFor"/>), bu yüzden yol saklanmaz ve kayıttan yol okunmaz.
/// </summary>
/// <param name="PayloadFile">
/// Uygulamada kullanılan ana dosya, kurulum klasörüne göre: theme.json, profile.json, "icons" ya da
/// background.&lt;uzantı&gt;. Arka planın uzantısı pakete göre değiştiği için kaydedilir.
/// </param>
/// <param name="Applied">Kullanıcı "Uygula"ya bastı ve henüz geri alınmadı (profil türünde hep false).</param>
public sealed record InstalledEntry(
    string Id,
    string Version,
    ExtensionKind Kind,
    string Name,
    string Author,
    string License,
    DateTimeOffset InstalledAt,
    ExtensionSource Source,
    bool Applied,
    string PayloadFile = "");

public enum InstalledRegistryError
{
    None,
    InvalidEntry,
    TooManyExtensions,
    DuplicateId,
}

/// <summary>
/// Kurulu eklenti kaydının değişmez, saf modeli. Kimlik, ad, sürüm ve yazar doğrulaması ile en çok 64 kayıt ve
/// tekil kimlik kuralı mevcut <see cref="ExtensionManifestValidator"/> ve <see cref="InstalledExtensionList"/>
/// ile yapılır. Girdi sırası ne olursa olsun kimliğe göre sıralanır.
/// </summary>
public sealed class InstalledRegistry
{
    public static InstalledRegistry Empty { get; } = new([]);

    private InstalledRegistry(IReadOnlyList<InstalledEntry> entries) => Entries = entries;

    public IReadOnlyList<InstalledEntry> Entries { get; }

    public InstalledEntry? Find(string? id)
    {
        foreach (var entry in Entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    /// <summary>Bu türden "uygulandı" işaretli bir kayıt var mı (tema zinciri: ilk özgün yedeği korumak için).</summary>
    public bool HasApplied(ExtensionKind kind) => Entries.Any(e => e.Kind == kind && e.Applied);

    public static bool TryBuild(IEnumerable<InstalledEntry> entries, out InstalledRegistry registry, out InstalledRegistryError error)
    {
        registry = Empty;
        var wrapped = new List<InstalledExtension>();
        var byId = new Dictionary<string, InstalledEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!IsValid(entry, out var manifest))
            {
                error = InstalledRegistryError.InvalidEntry;
                return false;
            }

            // Sınır aşıldığında numaralandırma durur: dev dosyadan fazlası kopyalanmaz.
            if (wrapped.Count > InstalledExtensionList.MaxInstalled)
            {
                break;
            }

            wrapped.Add(new InstalledExtension(manifest!, true));
            byId.TryAdd(entry.Id, entry);
        }

        if (!InstalledExtensionList.TryBuild(wrapped, out var list, out var listError))
        {
            error = listError == InstalledExtensionListError.DuplicateId
                ? InstalledRegistryError.DuplicateId
                : InstalledRegistryError.TooManyExtensions;
            return false;
        }

        registry = new InstalledRegistry(list.Select(e => byId[e.Manifest.Id]).ToList().AsReadOnly());
        error = InstalledRegistryError.None;
        return true;
    }

    /// <summary>
    /// Kayıt ekler ya da aynı kimlikli olanı değiştirir (güncelleme/yeniden kurulum). Yeni kimlik için sınır
    /// (64) dolmuşsa <see cref="InstalledRegistryError.TooManyExtensions"/>.
    /// </summary>
    public bool TryUpsert(InstalledEntry entry, out InstalledRegistry result, out InstalledRegistryError error)
    {
        var others = Entries.Where(e => e.Id != entry.Id);
        return TryBuild(others.Append(entry), out result, out error);
    }

    public InstalledRegistry Remove(string id) =>
        new(Entries.Where(e => e.Id != id).ToList().AsReadOnly());

    /// <summary>
    /// Kimliği uygulanmış işaretler; aynı türdeki öbürlerinin işaretini kaldırır (tema, simge paketi ve arka plan
    /// aynı anda tek olabilir). Profil türü "uygulanmış" tutulmaz, kayıt değişmez.
    /// </summary>
    public InstalledRegistry WithApplied(string id)
    {
        var target = Find(id);
        if (target is null || target.Kind == ExtensionKind.Profile)
        {
            return this;
        }

        return new(Entries
            .Select(e => e.Kind == target.Kind ? e with { Applied = e.Id == id } : e)
            .ToList().AsReadOnly());
    }

    public InstalledRegistry WithoutApplied(string id) =>
        new(Entries.Select(e => e.Id == id ? e with { Applied = false } : e).ToList().AsReadOnly());

    private static bool IsValid(InstalledEntry entry, out ExtensionManifest? manifest)
    {
        manifest = null;
        if (entry is null || !Enum.IsDefined(entry.Kind) || !Enum.IsDefined(entry.Source)
            || !StoreFieldRules.IsValidLicense(entry.License))
        {
            return false;
        }

        return ExtensionManifestValidator.TryCreate(
            entry.Id, entry.Name, entry.Version, entry.Author, null, "data", out manifest, out _);
    }
}

/// <summary>extensions.json'ın dosya biçimi.</summary>
public sealed record InstalledRegistryFile(int Version, List<InstalledEntry> Extensions);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(InstalledRegistryFile))]
internal sealed partial class StoreJsonContext : JsonSerializerContext;

/// <summary>Kayıt dosyasının okuma sonucu: yazmaya izin verip vermediğini belirler.</summary>
internal enum RegistryLoadState
{
    /// <summary>Dosya okundu (geçerli ya da yalnız bozuk kayıtlar elendi).</summary>
    Ok,

    /// <summary>Dosya yok: boş kayıt, yazmak güvenli.</summary>
    Missing,

    /// <summary>Bozuk JSON ya da bilinmeyen eski/yanlış biçim: yazmadan önce <c>.bad</c> olarak kenara alınır.</summary>
    Corrupt,

    /// <summary>Okunamadı (G/Ç, yetki): dosya belki sağlam, üzerine yazılmaz.</summary>
    Unreadable,

    /// <summary>Daha yeni sürümün dosyası: bu sürüm asla ezmez.</summary>
    NewerVersion,
}

/// <summary>
/// <c>%AppData%\Koy\extensions.json</c> okuma/yazma. Okuma hiç fırlatmaz (hata crash.log'a yazılır); okunamayan
/// dosya <see cref="Load"/>'da boş görünür ama <see cref="Update"/> onu ezmez: dosya yoksa boş kayıttan yazılır,
/// bozuksa önce <c>.bad</c> olarak kenara alınır, okunamıyorsa ya da daha yeni sürümse hiç yazılmaz (false).
/// Geçerli dosyada bozuk tek tek kayıtlar, tekrar eden kimlikler ve 64 sınırını aşanlar atılır. Yazma geçici
/// dosya + taşıma ile yapılır (yarım dosya kalmaz). Yansımalı serileştirme yok: kaynak üreticili
/// <see cref="StoreJsonContext"/> (AGENTS #10).
/// </summary>
public sealed class InstalledRegistryStore(string? path = null, Action<string, Exception>? onError = null)
{
    public const int CurrentVersion = 1;

    // Okuma-değiştirme-yazma süreç içinde tek sıra: kurulum ve "Uygula" işaretleri birbirinin kaydını ezmesin.
    private static readonly object Gate = new();

    private readonly Action<string, Exception> _onError = onError ?? ((source, ex) => CrashLog.Write(source, ex));

    public string Path { get; } = path ?? ExtensionPaths.RegistryPath;

    public InstalledRegistry Load()
    {
        lock (Gate)
        {
            return LoadCore(out _);
        }
    }

    /// <summary>Kaydı olduğu gibi yazar; <see cref="Update"/> ile aynı koruma kuralları (okunamayan dosya ezilmez).</summary>
    public bool Save(InstalledRegistry registry) => Update(_ => registry);

    /// <summary>
    /// Kaydı okur, <paramref name="change"/> ile değiştirir ve yazar; hepsi tek kilit altında. Dönüş null ise
    /// değişiklik yapılmaz. Yazılırsa true; dosya okunamıyorsa ya da daha yeni sürümse false (dosyaya dokunulmaz).
    /// </summary>
    public bool Update(Func<InstalledRegistry, InstalledRegistry?> change)
    {
        lock (Gate)
        {
            var current = LoadCore(out var state);
            switch (state)
            {
                case RegistryLoadState.Unreadable:
                case RegistryLoadState.NewerVersion:
                    return false;
                case RegistryLoadState.Corrupt:
                    // Kullanıcı kurtarabilsin diye bozuk dosya silinmez, kenara alınır; alınamazsa yazılmaz.
                    if (!MoveAside())
                    {
                        return false;
                    }

                    break;
            }

            var next = change(current);
            return next is not null && SaveCore(next);
        }
    }

    private bool MoveAside()
    {
        try
        {
            var target = Path + ".bad";
            if (File.Exists(target))
            {
                // Önceki yedek ezilmez.
                target = Path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".bad";
            }

            File.Move(Path, target, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _onError("InstalledRegistryStore.MoveAside", ex);
            return false;
        }
    }

    private InstalledRegistry LoadCore(out RegistryLoadState state)
    {
        state = RegistryLoadState.Ok;
        try
        {
            if (!File.Exists(Path))
            {
                state = RegistryLoadState.Missing;
                return InstalledRegistry.Empty;
            }

            using var stream = File.OpenRead(Path);
            var file = JsonSerializer.Deserialize(stream, StoreJsonContext.Default.InstalledRegistryFile);
            if (file is null || file.Version > CurrentVersion)
            {
                state = file is null ? RegistryLoadState.Corrupt : RegistryLoadState.NewerVersion;
                return InstalledRegistry.Empty;
            }

            if (file.Extensions is null || file.Version != CurrentVersion)
            {
                state = RegistryLoadState.Corrupt;
                return InstalledRegistry.Empty;
            }

            // Tek tek ele: bir kaydın bozukluğu diğerlerinin kaydını silmesin.
            var good = new List<InstalledEntry>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in file.Extensions)
            {
                if (entry is null
                    || !InstalledRegistry.TryBuild([entry], out _, out _)
                    || !seen.Add(entry.Id)
                    || good.Count == InstalledExtensionList.MaxInstalled)
                {
                    continue;
                }

                good.Add(entry);
            }

            return InstalledRegistry.TryBuild(good, out var registry, out _) ? registry : InstalledRegistry.Empty;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _onError("InstalledRegistryStore.Load", ex);
            state = RegistryLoadState.Corrupt;
            return InstalledRegistry.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _onError("InstalledRegistryStore.Load", ex);
            state = RegistryLoadState.Unreadable;
            return InstalledRegistry.Empty;
        }
    }

    private bool SaveCore(InstalledRegistry registry)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            using (var stream = File.Create(temp))
            {
                JsonSerializer.Serialize(
                    stream,
                    new InstalledRegistryFile(CurrentVersion, [.. registry.Entries]),
                    StoreJsonContext.Default.InstalledRegistryFile);
            }

            File.Move(temp, Path, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _onError("InstalledRegistryStore.Save", ex);
            return false;
        }
    }
}
