namespace Koy.Core.Settings;

/// <summary>
/// Ayarlar penceresinde sayfa başına "Varsayılana döndür" (issue #69). Yalnız verilen bölümler yeni (varsayılan)
/// kayıtla değiştirilir; diğer bölümlere dokunulmaz. Uygulamanın kendi yazdığı durumlar (ilk açılış tamamlandı,
/// son görülen sürüm) korunur, yoksa sıfırlama sihirbazı ya da "Neler Geldi" ekranını yeniden açardı.
/// Hangi sayfanın neyi sıfırladığı <see cref="TargetsFor"/> içinde; Dock sayfası görev çubuğuna dokunmamak için
/// "Dock'u kullan" ve "Windows görev çubuğunu gizle" anahtarlarını korur (açık/kapalı durumu sıfırlamayla değişmez).
/// </summary>
public static class SettingsPageReset
{
    /// <summary>Sıfırlanabilen bölüm adları (<see cref="AppSettings"/> özellik adlarıyla aynı).</summary>
    public static readonly IReadOnlySet<string> ResettableSections = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(AppSettings.General),
        nameof(AppSettings.Appearance),
        nameof(AppSettings.Notch),
        nameof(AppSettings.Music),
        nameof(AppSettings.Focus),
        nameof(AppSettings.Search),
        nameof(AppSettings.Clipboard),
        nameof(AppSettings.Shortcuts),
        nameof(AppSettings.Voice),
        nameof(AppSettings.HotCorners),
        nameof(AppSettings.Dock),
        nameof(AppSettings.Displays),
    };

    /// <summary>Bölümün tamamı yerine tek alanı sıfırlanabilen hedefler ("Bölüm.Alan"): sayfa başka sayfaların da kullandığı bölümden yalnız kendi satırlarını sıfırlar.</summary>
    public static readonly IReadOnlySet<string> ResettableFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "Appearance.BatterySaverReduceMotion",
        "Weather.RefreshInterval",
    };

    // Ayarlar penceresindeki sayfa dizinleri (KoyWindow.xaml sırası).
    private const int PageGeneral = 0, PageNotch = 1, PageDock = 2, PageDisplays = 3, PageAppearance = 4,
        PageShortcuts = 5, PageDictation = 6, PagePerformance = 7;

    /// <summary>
    /// Sayfa dizinine göre sıfırlanacak hedefler (bölüm adı ya da "Bölüm.Alan"); sıfırlaması olmayan sayfa (Hakkında,
    /// Gelişmiş, Eklentiler) için boş. Hava durumu konumu kullanıcı verisidir, hiçbir sayfa sıfırlamasında yer almaz.
    /// </summary>
    public static IReadOnlyList<string> TargetsFor(int pageIndex) => pageIndex switch
    {
        PageGeneral => [nameof(AppSettings.General)],
        PageNotch => [nameof(AppSettings.Notch), nameof(AppSettings.Music), nameof(AppSettings.Focus), nameof(AppSettings.Search), nameof(AppSettings.Clipboard)],
        PageDock => [nameof(AppSettings.Dock)],
        PageDisplays => [nameof(AppSettings.Displays)],
        PageAppearance => [nameof(AppSettings.Appearance)],
        PageShortcuts => [nameof(AppSettings.Shortcuts), nameof(AppSettings.HotCorners)],
        PageDictation => [nameof(AppSettings.Voice)],
        PagePerformance => ["Appearance.BatterySaverReduceMotion", "Weather.RefreshInterval"],
        _ => [],
    };

    /// <summary>Verilen bölümleri (ve "Bölüm.Alan" hedeflerini) varsayılana döndürür. Bilinmeyen ya da sıfırlanamaz ad hata verir.</summary>
    public static AppSettings Reset(AppSettings current, IEnumerable<string> sections)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(sections);

        var result = current;
        foreach (var section in sections.Distinct(StringComparer.Ordinal))
        {
            if (!ResettableSections.Contains(section) && !ResettableFields.Contains(section))
                throw new ArgumentException($"Sıfırlanamaz bölüm: {section}", nameof(sections));

            var defaults = new AppSettings();
            result = section switch
            {
                "Appearance.BatterySaverReduceMotion" => result with
                {
                    Appearance = result.Appearance with { BatterySaverReduceMotion = defaults.Appearance.BatterySaverReduceMotion }
                },
                "Weather.RefreshInterval" => result with
                {
                    Weather = result.Weather with { RefreshInterval = defaults.Weather.RefreshInterval }
                },
                // Dock açık/kapalı ve görev çubuğunu gizleme korunur: sıfırlama görev çubuğuna dokunmasın (AGENTS #7, #12).
                nameof(AppSettings.Dock) => result with
                {
                    Dock = new DockSettings
                    {
                        Enabled = current.Dock.Enabled,
                        HideWindowsTaskbar = current.Dock.HideWindowsTaskbar,
                    }
                },
                nameof(AppSettings.Displays) => result with { Displays = new DisplaysSettings() },
                nameof(AppSettings.HotCorners) => result with { HotCorners = new HotCornersSettings() },
                nameof(AppSettings.General) => result with
                {
                    General = new GeneralSettings
                    {
                        FirstRunCompleted = current.General.FirstRunCompleted,
                        LastSeenVersion = current.General.LastSeenVersion,
                    }
                },
                nameof(AppSettings.Appearance) => result with { Appearance = new AppearanceSettings() },
                nameof(AppSettings.Notch) => result with { Notch = new NotchSettings() },
                nameof(AppSettings.Music) => result with { Music = new MusicSettings() },
                nameof(AppSettings.Focus) => result with { Focus = new FocusSettings() },
                nameof(AppSettings.Search) => result with { Search = new SearchSettings() },
                nameof(AppSettings.Clipboard) => result with { Clipboard = new ClipboardSettings() },
                nameof(AppSettings.Shortcuts) => result with { Shortcuts = new ShortcutsSettings() },
                nameof(AppSettings.Voice) => result with { Voice = new VoiceSettings() },
                _ => throw new InvalidOperationException($"Eksik sıfırlama dalı: {section}"),
            };
        }

        return result;
    }
}
