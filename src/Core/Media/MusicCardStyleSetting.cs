namespace Koy.Core.Media;

/// <summary>
/// Müzik kartı stili: Düz (sabit nötr kart), Albüm rengi (kapaktan türeyen ton, varsayılan) ya da Albüm kapağı
/// (küçültülmüş kapak arka plan, üstünde koyu perde). Bilinmeyen değer varsayılana düşer.
/// </summary>
public static class MusicCardStyleSetting
{
    public const string Neutral = "Neutral";
    public const string Tinted = "Tinted";
    public const string Artwork = "Artwork";

    public static string Normalize(string? value)
    {
        var v = (value ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "neutral" or "düz" or "duz" => Neutral,
            "artwork" or "albüm kapağı" or "album kapagi" or "cover" => Artwork,
            _ => Tinted
        };
    }
}
