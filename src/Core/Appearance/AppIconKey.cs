namespace Koy.Core.Appearance;

/// <summary>
/// Uygulama başına simge değişiminin anahtarı: varsa AppUserModelId (paketli uygulamalarda kısayol değişse de aynı kalır),
/// yoksa dock öğesinin kısayol/yürütülebilir dosya adı. Geçerli anahtar çıkmazsa null.
/// </summary>
public static class AppIconKey
{
    /// <param name="appUserModelId">Dock öğesinin AUMID'i; yoksa null.</param>
    /// <param name="itemId">Dock öğesi kimliği (kısayol dosya adı ya da yolu).</param>
    public static string? For(string? appUserModelId, string? itemId) => All(appUserModelId, itemId) is [var first, ..] ? first : null;

    /// <summary>
    /// Öğenin bütün geçerli anahtarları, tercih sırasıyla (AUMID, kısayol adı). Sabitli bir uygulamanın AUMID'i yalnız
    /// çalışırken bilinebilir; bu yüzden kaydederken ve silerken hepsi, okurken ilk eşleşen kullanılır ve değişim
    /// uygulama kapalıyken de kaybolmaz.
    /// </summary>
    public static IReadOnlyList<string> All(string? appUserModelId, string? itemId)
    {
        var keys = new List<string>(2);
        if (AppIconOverrides.IsValidKey(appUserModelId))
            keys.Add(appUserModelId!.Trim());

        if (ShortcutName(itemId) is { } name && !keys.Contains(name, StringComparer.OrdinalIgnoreCase))
            keys.Add(name);

        return keys;
    }

    private static string? ShortcutName(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        // Yol gelirse yalnız dosya adı alınır (PinnedOrder.IdOf ile aynı kural, her iki ayırıcıyla).
        var trimmed = itemId.Trim();
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        var name = cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
        return AppIconOverrides.IsValidKey(name) ? name : null;
    }
}
