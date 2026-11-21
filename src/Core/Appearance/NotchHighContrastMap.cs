namespace Koy.Core.Appearance;

/// <summary>Yüksek Karşıtlık temasında bir çentik renginin üstleneceği sistem rengi rolü.</summary>
public enum HighContrastRole
{
    /// <summary>Yazı, simge, vurgu (WindowText).</summary>
    Text,

    /// <summary>Çentik yüzeyi ve kartlar (Window).</summary>
    Background,

    /// <summary>İnce çizgi ve kenarlıklar (WindowText).</summary>
    Border,

    /// <summary>Seçili, üstüne gelinen ve basılı dolgu (Highlight).</summary>
    Highlight,

    /// <summary>Highlight dolgusunun üstündeki yazı (HighlightText).</summary>
    HighlightText,

    /// <summary>Boş iz (ilerleme, kaydırıcı): dolgudan ayrışsın diye GrayText.</summary>
    Track
}

/// <summary>
/// Windows Yüksek Karşıtlık açıkken (SystemParameters.HighContrast) çentik token'larının hangi sistem rengine
/// bağlanacağı. Çentik normalde sabit koyu paletlidir; bu tablo yazının, yüzeyin ve kenarın en azından sistem
/// renklerinde okunmasını sağlar. Karar burada (testli), SystemColors eşlemesi App'te (<c>NotchHighContrast</c>).
/// </summary>
public static class NotchHighContrastMap
{
    private static readonly string[] Text =
    [
        "KoyNotchText", "KoyNotchTextSecondary", "KoyNotchTextMuted", "KoyNotchMuted", "KoyNotchDim", "KoyNotchDoneText",
        "KoyNotchFocusRing", "KoyNotchOk", "KoyNotchBatteryOk", "KoyNotchBatteryLow", "KoyNotchRowError",
        "KoyNotchAccentHome", "KoyNotchAccentFocus", "KoyNotchAccentShelf", "KoyNotchAccentClipboard", "KoyNotchAccentSystem",
        "KoyNotchAccentControl", "KoyNotchAccentCalendar",
        "KoyNotchMusicSub", "KoyNotchMusicSpectrum", "KoyNotchWeatherSoft", "KoyNotchFocusSoft", "KoyNotchShelfSoft",
        "KoyNotchShelfChipText", "KoyNotchControlSoft", "KoyNotchControlBattery", "KoyNotchBrightness", "KoyNotchCalendarSoft",
        "KoyNotchClipSoft", "KoyNotchClipChipText", "KoyNotchSystemSoft", "KoyNotchNetSoft",
    ];

    private static readonly string[] Background =
    [
        "KoyNotchSurface", "KoyNotchCard", "KoyNotchChip", "KoyNotchMusicCard", "KoyNotchMusicCover", "KoyNotchWeatherCard",
        "KoyNotchWeatherIconBg", "KoyNotchFocusCard", "KoyNotchFocusButton", "KoyNotchShelfCard", "KoyNotchShelfChip",
        "KoyNotchControlCard", "KoyNotchControlTile", "KoyNotchControlIconBg", "KoyNotchCalendarCard", "KoyNotchCalendarChip",
        "KoyNotchClipCard", "KoyNotchClipChip", "KoyNotchSystemCard", "KoyNotchNetCard", "KoyNotchIconBg",
    ];

    private static readonly string[] Border =
        ["KoyNotchCardBorder", "KoyNotchDivider", "KoyNotchDash", "KoyNotchCheckBorder", "KoyNotchShelfDash"];

    private static readonly string[] Highlight = ["KoyNotchHover", "KoyNotchPressed", "KoyNotchActive", "KoyNotchRowHover"];

    private static readonly string[] HighlightText = ["KoyNotchTextOnLight"];

    private static readonly string[] Track = ["KoyNotchTrack", "KoyNotchFocusTrack", "KoyNotchControlTrack", "KoyNotchSystemTrack"];

    /// <summary>Her token adı ve rolü (her ad bir kez).</summary>
    public static IReadOnlyList<(string Key, HighContrastRole Role)> Entries { get; } =
    [
        .. Text.Select(k => (k, HighContrastRole.Text)),
        .. Background.Select(k => (k, HighContrastRole.Background)),
        .. Border.Select(k => (k, HighContrastRole.Border)),
        .. Highlight.Select(k => (k, HighContrastRole.Highlight)),
        .. HighlightText.Select(k => (k, HighContrastRole.HighlightText)),
        .. Track.Select(k => (k, HighContrastRole.Track)),
    ];
}
