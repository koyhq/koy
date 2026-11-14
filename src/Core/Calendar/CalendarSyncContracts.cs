using System.Text.Json;
using System.Text.Json.Serialization;

namespace Koy.Core.Calendar;

/// <summary>Şifreli olarak diskte saklanan OAuth belirteç kümesi.</summary>
public sealed record OAuthTokenSet(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    string AccountLabel = "")
{
    /// <summary>Belirtecin süresi dolmuşsa veya 60 sn içinde dolacaksa true döner.</summary>
    public bool IsExpired(DateTimeOffset nowUtc) => ExpiresAtUtc <= nowUtc.AddSeconds(60);
}

/// <summary>Çevrimdışı önbellekte saklanan son bilinen harici etkinlikler.</summary>
public sealed record CalendarCacheSnapshot(
    DateTimeOffset? LastSyncUtc,
    List<CalendarEvent> Events);

/// <summary>Harici takvim (Google / Outlook) OAuth ayarları. İstemci sırrı ve token burada tutulmaz.</summary>
public sealed record CalendarSettings
{
    public bool GoogleEnabled { get; set; }
    public string GoogleClientId { get; set; } = "";
    public string GoogleAccountLabel { get; set; } = "";
    public bool OutlookEnabled { get; set; }
    public string OutlookClientId { get; set; } = "";
    public string OutlookAccountLabel { get; set; } = "";
    public long LastSyncUnix { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(OAuthTokenSet))]
[JsonSerializable(typeof(CalendarCacheSnapshot))]
[JsonSerializable(typeof(CalendarSettings))]
internal sealed partial class CalendarJsonContext : JsonSerializerContext;

/// <summary>Harici takvim hesap ayarlarını <c>%AppData%\Koy\calendar-settings.json</c> dosyasında saklar.</summary>
public sealed class CalendarSettingsStore(string? path = null)
{
    public static string DefaultPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "calendar-settings.json");

    public string Path { get; } = path ?? DefaultPath;

    public CalendarSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new CalendarSettings();

            using var stream = File.OpenRead(Path);
            var settings = JsonSerializer.Deserialize(stream, CalendarJsonContext.Default.CalendarSettings);
            if (settings is null)
                return new CalendarSettings();

            settings.GoogleClientId ??= "";
            settings.GoogleAccountLabel ??= "";
            settings.OutlookClientId ??= "";
            settings.OutlookAccountLabel ??= "";
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new CalendarSettings();
        }
    }

    public void Save(CalendarSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, settings, CalendarJsonContext.Default.CalendarSettings);
        File.Move(temp, Path, overwrite: true);
    }
}

/// <summary>Platform bağımsız veri koruma sözleşmesi (Windows'ta DPAPI CurrentUser).</summary>
public interface ITokenProtector
{
    byte[] Protect(byte[] plainBytes);
    byte[] Unprotect(byte[] protectedBytes);
}

/// <summary>OAuth belirteçlerini <c>%AppData%\Koy\</c> altında DPAPI ile şifreli saklar.</summary>
public sealed class CalendarTokenStore
{
    private readonly ITokenProtector _protector;
    private readonly string _directory;

    public CalendarTokenStore(ITokenProtector protector, string? directory = null)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        _directory = directory ?? DefaultDirectory;
    }

    public static string DefaultDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy");

    public string GetTokenPath(CalendarProvider provider) =>
        Path.Combine(_directory, $"calendar-token-{provider.ToString().ToLowerInvariant()}.dat");

    public bool Exists(CalendarProvider provider) => File.Exists(GetTokenPath(provider));

    public OAuthTokenSet? Load(CalendarProvider provider)
    {
        var path = GetTokenPath(provider);
        try
        {
            if (!File.Exists(path))
                return null;

            var encrypted = File.ReadAllBytes(path);
            var plain = _protector.Unprotect(encrypted);
            return JsonSerializer.Deserialize(plain, CalendarJsonContext.Default.OAuthTokenSet);
        }
        catch
        {
            return null;
        }
    }

    public void Save(CalendarProvider provider, OAuthTokenSet tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        Directory.CreateDirectory(_directory);
        var path = GetTokenPath(provider);
        var plain = JsonSerializer.SerializeToUtf8Bytes(tokens, CalendarJsonContext.Default.OAuthTokenSet);
        var encrypted = _protector.Protect(plain);
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, encrypted);
        File.Move(temp, path, overwrite: true);
    }

    public void Delete(CalendarProvider provider)
    {
        var path = GetTokenPath(provider);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Harici takvim etkinliklerini çevrimdışı kullanım için <c>%AppData%\Koy\calendar-cache.json</c> dosyasında tutar.</summary>
public sealed class CalendarCacheStore(string? path = null)
{
    public static string DefaultPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy",
        "calendar-cache.json");

    public string Path { get; } = path ?? DefaultPath;

    public CalendarCacheSnapshot Load()
    {
        try
        {
            if (!File.Exists(Path))
                return new CalendarCacheSnapshot(null, []);

            using var stream = File.OpenRead(Path);
            var snap = JsonSerializer.Deserialize(stream, CalendarJsonContext.Default.CalendarCacheSnapshot);
            if (snap is null)
                return new CalendarCacheSnapshot(null, []);

            var valid = snap.Events?
                .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Title) && e.Source != CalendarEventSource.Local)
                .ToList() ?? [];
            return new CalendarCacheSnapshot(snap.LastSyncUtc, valid);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new CalendarCacheSnapshot(null, []);
        }
    }

    public void Save(CalendarCacheSnapshot snapshot)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, snapshot, CalendarJsonContext.Default.CalendarCacheSnapshot);
        File.Move(temp, Path, overwrite: true);
    }

    public void RemoveProvider(CalendarEventSource source)
    {
        var current = Load();
        var remaining = current.Events.Where(e => e.Source != source).ToList();
        Save(new CalendarCacheSnapshot(current.LastSyncUtc, remaining));
    }
}

public sealed record OAuthConnectResult(bool Success, string AccountLabel = "", string? Error = null);

public sealed record CalendarSyncResult(
    bool Success,
    IReadOnlyList<CalendarEvent> Events,
    bool FromCache = false,
    string? Error = null);

/// <summary>Platform katmanının uyguladığı OAuth 2.0 PKCE + salt okunur takvim eşitleme sözleşmesi.</summary>
public interface ICalendarSyncService
{
    bool HasToken(CalendarProvider provider);
    Task<OAuthConnectResult> ConnectAsync(CalendarProvider provider, string clientId, CancellationToken ct = default);
    Task DisconnectAsync(CalendarProvider provider, CancellationToken ct = default);
    Task<CalendarSyncResult> SyncAsync(
        CalendarProvider provider,
        string clientId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken ct = default);
}
