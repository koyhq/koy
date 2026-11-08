using Koy.Core.Settings;

namespace Koy.Core.Shortcuts;

/// <summary>Kısayolla tetiklenebilen eylemler. Sıra, sayfadaki satır sırasıdır.</summary>
public enum ShortcutAction
{
    ToggleNotch,
    OpenShelf,
    ToggleDock,
    OpenMenu,
    PlayPause,
    OpenSearch,
    OpenSearchClipboard
}

/// <summary>Eylem ile ayar alanı ve varsayılan tuş eşlemesi.</summary>
public static class ShortcutBindings
{
    public static IReadOnlyList<ShortcutAction> All { get; } = Enum.GetValues<ShortcutAction>();

    /// <summary>
    /// Varsayılanlar Ctrl+Alt+harf: Windows kendi kısayollarında Win+harf kullanır (Win+D, Win+K, Win+Alt+R Game Bar...),
    /// Ctrl+Alt+harf ise sistemde ayrılmış değildir. Arama paneli sık açıldığı için tek elle basılan Win+Shift+Q'dur.
    /// </summary>
    public static string DefaultText(ShortcutAction action) => action switch
    {
        ShortcutAction.ToggleNotch => "Ctrl+Alt+N",
        ShortcutAction.OpenShelf => "Ctrl+Alt+R",
        ShortcutAction.ToggleDock => "Ctrl+Alt+D",
        ShortcutAction.OpenMenu => "Ctrl+Alt+K",
        ShortcutAction.PlayPause => "Ctrl+Alt+Space",
        ShortcutAction.OpenSearch => "Win+Shift+Q",
        ShortcutAction.OpenSearchClipboard => "Alt+Shift+F2",
        _ => ""
    };

    public static string GetText(AppSettings settings, ShortcutAction action) => action switch
    {
        ShortcutAction.ToggleNotch => settings.ShortcutToggleNotch,
        ShortcutAction.OpenShelf => settings.ShortcutOpenShelf,
        ShortcutAction.ToggleDock => settings.ShortcutToggleDock,
        ShortcutAction.OpenMenu => settings.ShortcutOpenMenu,
        ShortcutAction.PlayPause => settings.ShortcutPlayPause,
        ShortcutAction.OpenSearch => settings.ShortcutOpenSearch,
        ShortcutAction.OpenSearchClipboard => settings.ShortcutOpenSearchClipboard,
        _ => ""
    };

    public static AppSettings WithText(AppSettings settings, ShortcutAction action, string text) => action switch
    {
        ShortcutAction.ToggleNotch => settings with { ShortcutToggleNotch = text },
        ShortcutAction.OpenShelf => settings with { ShortcutOpenShelf = text },
        ShortcutAction.ToggleDock => settings with { ShortcutToggleDock = text },
        ShortcutAction.OpenMenu => settings with { ShortcutOpenMenu = text },
        ShortcutAction.PlayPause => settings with { ShortcutPlayPause = text },
        ShortcutAction.OpenSearch => settings with { ShortcutOpenSearch = text },
        ShortcutAction.OpenSearchClipboard => settings with { ShortcutOpenSearchClipboard = text },
        _ => settings
    };

    /// <summary>Metin, eylemin varsayılan kombinasyonu mu? Yazım sırası önemsizdir (Win+Shift+Q = Shift+Win+Q).</summary>
    public static bool IsDefault(ShortcutAction action, string text) =>
        Hotkey.TryParse(text, out var current) && Hotkey.TryParse(DefaultText(action), out var fallback) && current == fallback;

    /// <summary>Bütün eylemlerin ayardaki metni (boş metin = atanmamış).</summary>
    public static Dictionary<ShortcutAction, string> ReadAll(AppSettings settings) =>
        All.ToDictionary(a => a, a => GetText(settings, a));
}
