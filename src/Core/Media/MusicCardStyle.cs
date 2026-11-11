namespace Koy.Core.Media;

// Enum adları settings.json'a yazılır: bir kez yayımlandıktan sonra ADLARI DEĞİŞTİRİLMEZ (yalnız yeni değer eklenir).

/// <summary>
/// Müzik kartı stili: Düz (sabit nötr kart), Albüm rengi (kapaktan türeyen ton, varsayılan) ya da Albüm kapağı
/// (küçültülmüş kapak arka plan, üstünde koyu perde).
/// </summary>
public enum MusicCardStyle
{
    Neutral,
    Tinted,
    Artwork
}
