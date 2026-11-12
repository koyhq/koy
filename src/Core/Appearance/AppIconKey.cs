namespace Koy.Core.Appearance;

/// <summary>
/// Uygulama başına simge değişiminin anahtarı: varsa AppUserModelId (paketli uygulamalarda kısayol değişse de aynı kalır),
/// yoksa dock öğesinin kısayol/yürütülebilir dosya adı. Geçerli anahtar çıkmazsa null.
/// </summary>
public static class AppIconKey
{
    /// <param name="appUserModelId">Dock öğesinin AUMID'i; yoksa null.</param>
    /// <param name="itemId">Dock öğesi kimliği (kısayol dosya adı ya da yolu).</param>
    public static string? For(string? appUserModelId, string? itemId)
    {
        if (AppIconOverrides.IsValidKey(appUserModelId))
            return appUserModelId!.Trim();

        if (string.IsNullOrWhiteSpace(itemId))
            return null;

        // Yol gelirse yalnız dosya adı alınır (PinnedOrder.IdOf ile aynı kural, her iki ayırıcıyla).
        var trimmed = itemId.Trim();
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        var name = cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
        return AppIconOverrides.IsValidKey(name) ? name : null;
    }
}
