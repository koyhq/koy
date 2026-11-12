namespace Koy.Core.Tray;

/// <summary>
/// Tepsi simgesi görünürlüğü: uygulama başına kayıt varsa o, yoksa yeni simge varsayılanı. Anahtar küçük harfli exe adıdır.
/// </summary>
public static class TrayIconVisibilityPolicy
{
    /// <summary>Yeni tepsi simgeleri varsayılan olarak taşma menüsüne girer.</summary>
    public const TrayIconVisibility DefaultNewIcon = TrayIconVisibility.Overflow;

    /// <summary>Exe adından kayıt anahtarı: boşluk kırpılır, küçük harfe çevrilir (kültürden bağımsız).</summary>
    public static string AppKey(string? exeName) => (exeName ?? "").Trim().ToLowerInvariant();

    /// <summary>Simgenin etkin görünürlüğü.</summary>
    public static TrayIconVisibility Resolve(string? exeName, IReadOnlyDictionary<string, TrayIconVisibility> perApp, TrayIconVisibility newIconDefault)
    {
        var key = AppKey(exeName);
        if (key.Length > 0 && perApp.TryGetValue(key, out var visibility))
            return visibility;

        return newIconDefault;
    }

    /// <summary>
    /// Ayarlar listesindeki uygulamalar: bu oturumda tepside görülen exe adları ile kaydı olanların birleşimi,
    /// anahtar biçiminde (küçük harf), tekrarsız ve sıralı. Boş adlar atlanır.
    /// </summary>
    public static IReadOnlyList<string> SettingsApps(IEnumerable<string?> seenExeNames, IEnumerable<string> savedKeys) =>
        seenExeNames.Select(AppKey).Concat(savedKeys.Select(AppKey))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
}
