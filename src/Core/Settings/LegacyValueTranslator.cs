using System.Text.Json.Nodes;

namespace Koy.Core.Settings;

/// <summary>
/// v3 → v4 değer çevirisi: sayı olarak (<c>IdleDisplayMode</c>…) ya da dile bağlı metin olarak ("Katı", "Kararlı"…) yazılmış
/// ayarlar enum adına çevrilir. Eski <c>Normalize</c> yardımcılarının kabul ettiği her değer burada karşılığını bulur.
/// Tanınmayan değer için <c>null</c> döner: göç o anahtarı düşürür, alan varsayılanını alır (dosyanın kalanı etkilenmez).
/// </summary>
internal static class LegacyValueTranslator
{
    // Anahtarlar küçük harfle aranır (eski kod da ToLowerInvariant kullanırdı).
    private static readonly Dictionary<string, string> Theme = new()
    {
        ["system"] = "System", ["sistem"] = "System",
        ["dark"] = "Dark", ["koyu"] = "Dark",
        ["light"] = "Light", ["açık"] = "Light", ["acik"] = "Light",
    };

    private static readonly Dictionary<string, string> AnimationSpeed = new()
    {
        ["sakin"] = "Calm", ["calm"] = "Calm", ["slow"] = "Calm",
        ["normal"] = "Normal",
        ["hızlı"] = "Fast", ["hizli"] = "Fast", ["fast"] = "Fast",
        ["kapalı"] = "Off", ["kapali"] = "Off", ["off"] = "Off", ["none"] = "Off",
    };

    private static readonly Dictionary<string, string> UpdateChannel = new()
    {
        ["kararlı"] = "Stable", ["kararli"] = "Stable", ["stable"] = "Stable",
        ["önizleme"] = "Preview", ["onizleme"] = "Preview", ["preview"] = "Preview",
    };

    private static readonly Dictionary<string, string> WeatherRefresh = new()
    {
        ["30m"] = "Every30Minutes",
        ["1h"] = "Hourly",
        ["startup"] = "OnStartup",
    };

    private static readonly Dictionary<string, string> Material = new()
    {
        ["katı"] = "Solid", ["kati"] = "Solid", ["solid"] = "Solid",
        ["buzlu"] = "Frosted", ["frosted"] = "Frosted", ["yarı saydam"] = "Frosted", ["yari saydam"] = "Frosted",
        ["translucent"] = "Frosted", ["acrylic"] = "Frosted", ["mica"] = "Frosted",
        ["cam"] = "Glass", ["glass"] = "Glass",
    };

    private static readonly Dictionary<string, string> NotchStyle = new()
    {
        ["yapışık"] = "Pinned", ["yapisik"] = "Pinned", ["pinned"] = "Pinned",
        ["yüzen"] = "Floating", ["yuzen"] = "Floating", ["yüzen ada"] = "Floating", ["floating"] = "Floating", ["island"] = "Floating",
    };

    private static readonly Dictionary<string, string> MusicCard = new()
    {
        ["neutral"] = "Neutral", ["düz"] = "Neutral", ["duz"] = "Neutral",
        ["tinted"] = "Tinted",
        ["artwork"] = "Artwork", ["albüm kapağı"] = "Artwork", ["album kapagi"] = "Artwork", ["cover"] = "Artwork",
    };

    /// <summary>Düz (v3) anahtar → değer çevirisi. Tabloda olmayan anahtarın değeri olduğu gibi taşınır.</summary>
    public static IReadOnlyDictionary<string, Func<JsonNode, string?>> ByKey { get; } = new Dictionary<string, Func<JsonNode, string?>>
    {
        ["Theme"] = node => Text(node, Theme),
        ["AnimationSpeed"] = node => Text(node, AnimationSpeed),
        ["UpdateChannel"] = node => Text(node, UpdateChannel),
        ["WeatherRefreshInterval"] = node => Text(node, WeatherRefresh),
        ["Material"] = node => Text(node, Material),
        ["NotchStyle"] = node => Text(node, NotchStyle),
        ["MusicCardStyle"] = node => Text(node, MusicCard),
        ["IdleDisplayMode"] = node => Number(node, "ClockAndBattery", "ClockOnly", "Nothing", "RingClockDay"),
        ["NotchOpenBehavior"] = node => Number(node, "Hover", "Click"),
        ["WheelAction"] = node => Number(node, "SwitchPanel", "Volume"),
        ["MiddleClickAction"] = node => Number(node, "NewWindow", "Close"),
        ["DockIndicatorStyle"] = node => Number(node, "Dot", "Line"),
    };

    private static string? Text(JsonNode node, Dictionary<string, string> map)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text))
            return null;
        return map.GetValueOrDefault(text.Trim().ToLowerInvariant());
    }

    /// <summary>Sayı → sıradaki enum adı (eski sabitler 0'dan başlıyordu). Aralık dışı ya da sayı olmayan değer null.</summary>
    private static string? Number(JsonNode node, params string[] names)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number) && number >= 0 && number < names.Length)
            return names[number];
        return null;
    }
}
