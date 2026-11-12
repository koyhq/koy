namespace Koy.Core.Settings;

/// <summary>
/// Ayarlar penceresinde sayfa başına "Varsayılana döndür" (issue #69). Yalnız verilen bölümler yeni (varsayılan)
/// kayıtla değiştirilir; diğer bölümlere dokunulmaz. Uygulamanın kendi yazdığı durumlar (ilk açılış tamamlandı,
/// son görülen sürüm) korunur, yoksa sıfırlama sihirbazı ya da "Neler Geldi" ekranını yeniden açardı.
/// Ekranlar ve Dock sayfalarında sıfırlama yoktur: görev çubuğu / AppBar etkisi uygula-geri al ile birlikte ele alınır.
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
    };

    /// <summary>Verilen bölümleri varsayılana döndürür. Bilinmeyen ya da sıfırlanamaz bölüm adı hata verir.</summary>
    public static AppSettings Reset(AppSettings current, IEnumerable<string> sections)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(sections);

        var result = current;
        foreach (var section in sections.Distinct(StringComparer.Ordinal))
        {
            if (!ResettableSections.Contains(section))
                throw new ArgumentException($"Sıfırlanamaz bölüm: {section}", nameof(sections));

            result = section switch
            {
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
