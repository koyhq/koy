namespace Koy.Core.Notch;

/// <summary>
/// Açık çentikte alttaki sekmelerden seçilebilen paneller. Sekme çubuğu bu sıraya göre dizilir
/// (<see cref="NotchPanels.Order"/>). Pano yalnız pano geçmişi ayarı açıkken görünür.
/// </summary>
public enum NotchPanel
{
    /// <summary>Ana: müzik ya da selamlama, hava durumu, sistem kartları, hızlı başlat, çalışan zamanlayıcı.</summary>
    Home,
    /// <summary>Odak: Pomodoro, mola, sayaç ve kronometre; görev listesi.</summary>
    Focus,
    /// <summary>Takvim: ay, hafta ve gün görünümü, yerel etkinlikler.</summary>
    Calendar,
    Shelf,
    /// <summary>Pano geçmişi (yalnız <c>ClipboardHistoryEnabled</c> açıkken).</summary>
    Clipboard,
    /// <summary>Sistem: CPU, bellek, ağ hızı, Koy'un kendi belleği.</summary>
    System,
    ControlCenter
}

/// <summary>Panellerin sekme sırası ve ayarlardaki anahtar adları.</summary>
public static class NotchPanels
{
    /// <summary>Sekme çubuğundaki sıra. Ayarlar yalnız hangilerinin açık olduğunu söyler, sırayı değiştirmez.</summary>
    public static IReadOnlyList<NotchPanel> Order { get; } =
        [NotchPanel.Home, NotchPanel.Focus, NotchPanel.Calendar, NotchPanel.Shelf, NotchPanel.Clipboard, NotchPanel.System, NotchPanel.ControlCenter];

    /// <summary>
    /// Kullanıcının sekme sırası (<see cref="Settings.NotchSettings.PanelOrder"/>). Geçerli, tekrarsız anahtarlar
    /// verilen sırayla başa gelir; eksik paneller varsayılan sıranın sonuna eklenir. Boş ya da <c>null</c> girdi
    /// varsayılan sırayı verir. Görünürlük bu listeyi etkilemez.
    /// </summary>
    public static List<NotchPanel> NormalizeOrder(IEnumerable<string>? keys)
    {
        var result = new List<NotchPanel>();
        if (keys is not null)
        {
            foreach (var key in keys)
            {
                if (TryParse(key, out var panel) && !result.Contains(panel))
                    result.Add(panel);
            }
        }

        foreach (var panel in Order)
        {
            if (!result.Contains(panel))
                result.Add(panel);
        }

        return result;
    }

    /// <summary>
    /// <see cref="Settings.AppSettings.EnabledPanels"/> anahtarlarından açık paneller, sekme sırasıyla.
    /// Ana her zaman açıktır. Eski anahtarlar taşınır: "music" Ana, "calendar" Takvim olur (H öncesi dosyalarda Odak da açılır); "status" artık ayrı
    /// bir panel değil (saat, hava ve pil üstteki durum satırında), yok sayılır. Eski ayar dosyalarında "clipboard"
    /// ve "system" yoktur: bu sekmeler Koy penceresinden açılana kadar kapalı kalır (dosya bozulmaz).
    /// <paramref name="clipboardHistoryEnabled"/> kapalıysa Pano, anahtar listede olsa bile elenir.
    /// </summary>
    public static List<NotchPanel> ParseEnabled(IEnumerable<string>? keys, bool clipboardHistoryEnabled = true)
    {
        var enabled = new HashSet<NotchPanel> { NotchPanel.Home };
        if (keys is not null)
        {
            var list = keys.ToList();
            foreach (var key in list)
            {
                if (TryParse(key, out var panel))
                    enabled.Add(panel);
            }

            // H öncesi dosyalarda (yalnız onlarda "music"/"status" anahtarı vardır) "calendar" paneli Pomodoro ve
            // zamanlayıcıları da taşırdı; Odak o zamandan beri ayrı sekme, bu yüzden kaybolmasın.
            var legacy = list.Any(k => string.Equals(k?.Trim(), "music", StringComparison.OrdinalIgnoreCase)
                || string.Equals(k?.Trim(), "status", StringComparison.OrdinalIgnoreCase));
            if (legacy && enabled.Contains(NotchPanel.Calendar))
                enabled.Add(NotchPanel.Focus);

            // Pano ve Sistem H'den önce yoktu: H öncesi dosyada kullanıcı onları kapatmış olamaz, açık gelirler
            // (Pano yine de pano geçmişi ayarına bağlı, aşağıda).
            if (legacy)
            {
                enabled.Add(NotchPanel.Clipboard);
                enabled.Add(NotchPanel.System);
            }
        }

        if (!clipboardHistoryEnabled)
            enabled.Remove(NotchPanel.Clipboard);

        return Order.Where(enabled.Contains).ToList();
    }

    /// <summary>Sekme sırasındaki yeri (listede olmayan sona gider).</summary>
    public static int SortIndex(NotchPanel panel)
    {
        for (var i = 0; i < Order.Count; i++)
        {
            if (Order[i] == panel)
                return i;
        }

        return int.MaxValue;
    }

    /// <summary>Ayarlara yazılan anahtar.</summary>
    public static string ToKey(NotchPanel panel) => panel switch
    {
        NotchPanel.Home => "home",
        NotchPanel.Focus => "focus",
        NotchPanel.Calendar => "calendar",
        NotchPanel.Shelf => "shelf",
        NotchPanel.Clipboard => "clipboard",
        NotchPanel.System => "system",
        NotchPanel.ControlCenter => "controlcenter",
        _ => panel.ToString().ToLowerInvariant()
    };

    public static bool TryParse(string? key, out NotchPanel panel)
    {
        switch (key?.Trim().ToLowerInvariant())
        {
            case "home" or "music":
                panel = NotchPanel.Home;
                return true;
            case "focus":
                panel = NotchPanel.Focus;
                return true;
            case "calendar":
                panel = NotchPanel.Calendar;
                return true;
            case "shelf":
                panel = NotchPanel.Shelf;
                return true;
            case "clipboard":
                panel = NotchPanel.Clipboard;
                return true;
            case "system":
                panel = NotchPanel.System;
                return true;
            case "controlcenter":
                panel = NotchPanel.ControlCenter;
                return true;
            default:
                panel = default;
                return false;
        }
    }
}
