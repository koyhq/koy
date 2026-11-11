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
}
