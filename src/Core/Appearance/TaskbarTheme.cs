namespace Koy.Core.Appearance;

/// <summary>
/// Tam genişlik dock'un (Windows 11 görev çubuğu gibi) sistem temasına uyan renk anahtarları. Kapsül <c>KoyDock*</c> ile her temada
/// koyu kalır; tam genişlikte aynı rollerin <c>KoyTaskbar*</c> karşılığı kullanılır (açık temada açık cam yüzey, koyu metin).
/// Eşleme saf ve testlidir; WPF tarafı pencere kaynaklarında <c>KoyDock*</c> adlarını <c>KoyTaskbar*</c> fırçalarıyla gölgeler.
/// </summary>
public static class TaskbarTheme
{
    private const string DockPrefix = "KoyDock";
    private const string TaskbarPrefix = "KoyTaskbar";

    /// <summary>Tam genişlikte sisteme uyan roller (KoyDock sonrası ad).</summary>
    public static readonly IReadOnlyList<string> Roles =
    [
        "Surface", "Border", "Highlight", "Separator", "Divider", "Hover", "Active", "Pill", "Indicator",
        "Text", "TextSecondary", "Glyph", "ClockText", "ClockSubtext"
    ];

    /// <summary>Bütün <c>KoyTaskbar*</c> anahtarları (iki tema dosyasında bulunmak zorundadır).</summary>
    public static IEnumerable<string> TaskbarKeys => Roles.Select(r => TaskbarPrefix + r);

    /// <summary>Dock anahtarının tam genişlikteki karşılığı; rolü yoksa (ör. kapsül yüzeyi) anahtar aynen döner.</summary>
    public static string TokenFor(string dockKey)
    {
        if (!dockKey.StartsWith(DockPrefix, StringComparison.Ordinal))
            return dockKey;

        var role = dockKey[DockPrefix.Length..];
        return Roles.Contains(role) ? TaskbarPrefix + role : dockKey;
    }

    /// <summary>Pencere kaynaklarında gölgelenecek (KoyDock anahtarı, KoyTaskbar anahtarı) çiftleri.</summary>
    public static IEnumerable<(string Dock, string Taskbar)> Pairs => Roles.Select(r => (DockPrefix + r, TaskbarPrefix + r));
}
