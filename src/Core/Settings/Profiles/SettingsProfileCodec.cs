using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Koy.Core.Shortcuts;
using Koy.Core.Timers;

namespace Koy.Core.Settings.Profiles;

/// <summary>
/// Ayar profili ve paketi dosyası (JSON metni): dışa aktarma, doğrulamalı okuma, seçili bölümleri uygulama.
/// Saf mantık: dosya G/Ç yok, çağıran metni okur ve yazar. Serileştirme kaynak üreticili
/// <see cref="SettingsJsonContext"/> ile yapılır, yansıma yok.
/// </summary>
public static class SettingsProfileCodec
{
    public const string FormatName = "koy-profile";
    public const int CurrentFormatVersion = 1;

    /// <summary>Bu uzunluktan büyük metin ayrıştırılmadan reddedilir (karakter sayısı).</summary>
    public const int MaxLength = 1_000_000;

    public const int MaxNameLength = 60;

    /// <summary>
    /// Ayar listelerinin (açık paneller, Kontrol Merkezi sırası) içe aktarmada kabul edilen en çok öğe sayısı. Gerçek
    /// listeler 7 öğeyi geçmez; 1 MB'lık bir dosya yüzbinlerce öğeyi settings.json'a kalıcı yazdırabilirdi.
    /// </summary>
    public const int MaxListItems = 32;

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>
    /// Ad boş olmamalı, en fazla 60 karakter olmalı; denetim karakteri, satır/paragraf ayırıcı (U+2028/2029) ve
    /// yön geçersiz kılma karakteri (U+202A-202E, U+2066-2069) içermemeli: sonuncusu önizlemede adı yanıltıcı gösterir.
    /// ZWJ gibi diğer biçim karakterleri (emoji dizileri) serbesttir.
    /// </summary>
    public static bool IsValidName(string? name)
    {
        if (name is null)
            return false;
        var trimmed = name.Trim();
        return trimmed.Length is > 0 and <= MaxNameLength && !trimmed.Any(IsForbiddenNameChar);
    }

    private static bool IsForbiddenNameChar(char c) =>
        char.IsControl(c)
        || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        || (int)c is (>= 0x202A and <= 0x202E) or (>= 0x2066 and <= 0x2069);

    /// <summary>
    /// Seçili bölümleri içeren profil metnini üretir. Seçilmeyen bölümler dosyaya hiç yazılmaz. Makineye özgü
    /// bölümler <see cref="ProfileSection"/> içinde olmadığından dışa aktarılamaz.
    /// </summary>
    public static string Export(AppSettings current, string name, IEnumerable<ProfileSection> sections)
    {
        if (!IsValidName(name))
            throw new ArgumentException("Profil adı boş, çok uzun ya da denetim karakteri içeriyor.", nameof(name));

        var chosen = sections.Distinct().OrderBy(s => (int)s).ToList();
        if (chosen.Count == 0)
            throw new ArgumentException("En az bir bölüm seçilmeli.", nameof(sections));
        // Tanımsız sayı ("99") dosyaya "99": null olarak yazılır ve dosya geri okunamazdı.
        if (!chosen.All(Enum.IsDefined))
            throw new ArgumentException("Bilinmeyen bölüm.", nameof(sections));

        // Dock başlat simgesinin yolu bu makineye özgü: paylaşılan dosyaya yazılmaz (içe aktarmada yerel değer korunur).
        var shareable = current with
        {
            Appearance = current.Appearance with { AppIconOverrides = new(), IconPackFolder = "" },
            Dock = current.Dock with { StartIconPath = "" },
            // Canlı arka plan dosyasının yolu da bu makineye özgü.
            Notch = current.Notch with { LiveBackgroundFilePath = "" },
        };
        var full = (JsonObject)JsonSerializer.SerializeToNode(shareable, SettingsJsonContext.Default.AppSettings)!;
        var settings = new JsonObject { ["Version"] = AppSettings.CurrentVersion };
        foreach (var section in chosen)
        {
            var key = section.ToString();
            settings[key] = full[key]?.DeepClone();
        }

        var sectionNames = new JsonArray(chosen.Select(s => (JsonNode?)JsonValue.Create(s.ToString())).ToArray());
        var root = new JsonObject
        {
            ["Format"] = FormatName,
            ["FormatVersion"] = CurrentFormatVersion,
            ["Name"] = name.Trim(),
            ["Sections"] = sectionNames,
            ["Settings"] = settings,
        };
        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Profil metnini doğrular. Bilinmeyen biçim, daha yeni biçim sürümü, geçersiz ad, bilinmeyen ya da makineye özgü
    /// bölüm, boş bölüm listesi ve tolerantlı okumada düzeltilmesi gereken (geçersiz değerli) alan reddedilir.
    /// Tolerantlı okumanın kaçırdığı değerler de reddedilir (<see cref="HasInvalidValues"/>): tanımsız enum sayısı,
    /// sonsuz sayı, aralık dışı Pomodoro süresi, çözülemeyen ya da çakışan kısayol, aşırı uzun liste.
    /// Bölüm İÇİNDEKİ bilinmeyen anahtarlar sessizce yok sayılır: ileri sürümün eklediği alan bu sürüme taşınmaz.
    /// </summary>
    public static ProfileImportResult Read(string json)
    {
        if (json is null)
            return Fail(ProfileImportError.NotAProfile);
        if (json.Length > MaxLength)
            return Fail(ProfileImportError.TooLarge);

        try
        {
            return ReadCore(json);
        }
        catch (ArgumentException)
        {
            // JsonNode aynı anahtarı iki kez içeren nesnede JsonException değil ArgumentException atar (ağaç tembel
            // kurulur, atış ilk erişimde olur): kötü niyetli ya da bozuk dosya uygulamayı düşürmesin.
            return Fail(ProfileImportError.MalformedJson);
        }
    }

    private static ProfileImportResult ReadCore(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return Fail(ProfileImportError.MalformedJson);
        }

        if (node is not JsonObject root)
            return Fail(ProfileImportError.NotAProfile);
        if (StringOf(root, "Format") != FormatName)
            return Fail(ProfileImportError.NotAProfile);
        if (!TryIntOf(root, "FormatVersion", out var version) || version < 1)
            return Fail(ProfileImportError.NotAProfile);
        if (version > CurrentFormatVersion)
            return Fail(ProfileImportError.UnsupportedFormatVersion);

        var name = StringOf(root, "Name");
        if (!IsValidName(name))
            return Fail(ProfileImportError.InvalidName);

        if (root["Sections"] is not JsonArray declared)
            return Fail(ProfileImportError.NotAProfile);

        var sections = new List<ProfileSection>();
        foreach (var item in declared)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var sectionName) || !TryParseSection(sectionName, out var section))
                return Fail(ProfileImportError.UnknownSection);
            if (!sections.Contains(section))
                sections.Add(section);
        }
        if (sections.Count == 0)
            return Fail(ProfileImportError.NoSections);

        if (root["Settings"] is not JsonObject settingsObject)
            return Fail(ProfileImportError.NotAProfile);

        // Yalnız bildirilen bölümler tolerantlı okumaya girer: bildirilmeyen bir bölümdeki bozukluk profili etkilemez.
        var scoped = new JsonObject { ["Version"] = AppSettings.CurrentVersion };
        foreach (var section in sections)
        {
            var key = section.ToString();
            if (settingsObject[key] is { } sectionNode)
                scoped[key] = sectionNode.DeepClone();
        }

        AppSettings settings;
        int dropped;
        bool badDockLayout;
        try
        {
            var parsed = SettingsStore.ParseTolerant(scoped, out dropped);
            // FillMissingSections dock yerleşimini (DockLayoutLimits.Sanitize) sessizce onarır; profil bozuk değeri reddeder.
            badDockLayout = sections.Contains(ProfileSection.Dock) && parsed.Dock is { } rawDock && HasInvalidDockLayout(rawDock);
            settings = parsed.FillMissingSections();
        }
        catch (JsonException)
        {
            // Hata yolu çözülemezse ParseTolerant onarmaz ve istisna çıkar: dosya temiz biçimde reddedilir, hiçbir şey yazılmaz.
            return Fail(ProfileImportError.InvalidValues);
        }
        if (dropped > 0 || badDockLayout || HasInvalidValues(settings, sections))
            return Fail(ProfileImportError.InvalidValues);

        return new ProfileImportResult(ProfileImportError.None, new ImportedProfile(name!.Trim(), sections, settings));
    }

    /// <summary>
    /// Tolerantlı okumanın hata saymadığı ama uygulanırsa ayarları bozacak değerler. Yalnız bildirilen bölümlere bakılır.
    /// Enum sayıları (<c>"Style": 99</c>) tanımsız değerle okunur; JSON sonsuz sayıyı (1e400) kabul eder; kısayol
    /// metni olduğu gibi saklanır ve geçersizse mevcut çalışan kısayolun üzerine yazılıp onu sessizce öldürürdü.
    /// Yeni enum / double / liste alanı eklenince buraya da eklenir (test bunu reflection ile zorlar).
    /// </summary>
    private static bool HasInvalidValues(AppSettings s, IReadOnlyList<ProfileSection> sections)
    {
        foreach (var section in sections)
        {
            var bad = section switch
            {
                ProfileSection.Appearance =>
                    !Enum.IsDefined(s.Appearance.Theme) || !Enum.IsDefined(s.Appearance.AnimationSpeed)
                    || !Enum.IsDefined(s.Appearance.Material) || !double.IsFinite(s.Appearance.CornerRadius)
                    || !Enum.IsDefined(s.Appearance.IconPack) || !double.IsFinite(s.Appearance.BaseFontSize),
                ProfileSection.Dock =>
                    !Enum.IsDefined(s.Dock.Visibility) || !Enum.IsDefined(s.Dock.Style) || !Enum.IsDefined(s.Dock.IconAlignment)
                    || !Enum.IsDefined(s.Dock.Proximity) || !Enum.IsDefined(s.Dock.StartIcon) || !Enum.IsDefined(s.Dock.IndicatorStyle)
                    || !Enum.IsDefined(s.Dock.MiddleClickAction) || !double.IsFinite(s.Dock.IconSize)
                    || !Enum.IsDefined(s.Dock.IndicatorColor) || !Enum.IsDefined(s.Dock.BadgeColor)
                    || !Enum.IsDefined(s.Dock.BadgeCorner) || !Enum.IsDefined(s.Dock.BadgeStyle) || !Enum.IsDefined(s.Dock.RevealTrigger)
                    || !Enum.IsDefined(s.Dock.GroupingMode) || !Enum.IsDefined(s.Dock.WindowMonitorScope)
                    || !Enum.IsDefined(s.Dock.LeftClickAction) || !Enum.IsDefined(s.Dock.LabelMode)
                    || !Enum.IsDefined(s.Dock.NewTrayIconVisibility),
                ProfileSection.Notch =>
                    !Enum.IsDefined(s.Notch.Visibility) || !Enum.IsDefined(s.Notch.Style) || !Enum.IsDefined(s.Notch.OpenBehavior)
                    || !Enum.IsDefined(s.Notch.IdleDisplay) || !Enum.IsDefined(s.Notch.WheelAction)
                    || !IsSaneList(s.Notch.EnabledPanels) || !IsSaneList(s.Notch.ControlCenterLayout)
                    || !Enum.IsDefined(s.Notch.TabBarStyle) || !IsSaneList(s.Notch.PanelOrder)
                    || s.Notch.HomeCards is null || s.Notch.HomeCards.Count > MaxListItems || !s.Notch.HomeCards.All(Enum.IsDefined)
                    || !Enum.IsDefined(s.Notch.ClockHourFormat) || !Enum.IsDefined(s.Notch.ClockFirstDayOfWeek)
                    || !Enum.IsDefined(s.Notch.LiveBackgroundTarget) || !Enum.IsDefined(s.Notch.LiveBackgroundSource),
                ProfileSection.Music =>
                    !Enum.IsDefined(s.Music.CardStyle) || !Enum.IsDefined(s.Music.VisualizerStyle)
                    || !Enum.IsDefined(s.Music.VisualizerColorSource),
                ProfileSection.Shortcuts => HasInvalidShortcut(s),
                ProfileSection.Focus =>
                    !IsPomodoroMinutes(s.Focus.PomodoroWorkMinutes) || !IsPomodoroMinutes(s.Focus.PomodoroShortBreakMinutes)
                    || !IsPomodoroMinutes(s.Focus.PomodoroLongBreakMinutes),
                _ => true,
            };
            if (bad)
                return true;
        }
        return false;
    }

    private static bool HasInvalidDockLayout(DockSettings dock) =>
        !Enum.IsDefined(dock.Edge) || !double.IsFinite(dock.EdgeOffsetDip)
        || !double.IsFinite(dock.InnerPaddingDip) || !double.IsFinite(dock.IconSpacingDip);

    private static bool IsSaneList(ValueList<string> list) => list.Count <= MaxListItems && !list.Contains(null!);

    // Çalışma zamanı bu aralığa sıkıştırır ama Ayarlar sayfası ham değeri gösterir ve kaydederken geri yazar.
    private static bool IsPomodoroMinutes(int minutes) => minutes is >= PomodoroConfig.MinMinutes and <= PomodoroConfig.MaxMinutes;

    // Ayarlar sayfasının kaydetme denetimiyle aynı kural (ShortcutConflicts.Check): boş = atanmamış, geri kalanı çözülmeli,
    // sistemce ayrılmış olmamalı ve profilin kendi içinde çakışmamalı.
    private static bool HasInvalidShortcut(AppSettings s)
    {
        var all = ShortcutBindings.ReadAll(s);
        foreach (var (action, text) in all)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            if (!Hotkey.TryParse(text, out var hotkey) || ShortcutConflicts.Check(hotkey, action, all) is not null)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Profilde bildirilen ve uygulandığında değişecek bölümler (içe aktarma önizlemesi). Karşılaştırma
    /// <see cref="Apply"/> sonucuna göre yapılır; böylece yerel dosya yolu gibi korunan değerler fark sayılmaz.
    /// </summary>
    public static IReadOnlyList<ProfileSection> ChangedSections(AppSettings current, ImportedProfile profile) =>
        profile.Sections
            .Where(section => !Equals(SectionOf(current, section), SectionOf(Apply(current, profile, [section]), section)))
            .ToList();

    /// <summary>
    /// Seçili ve profilde bildirilmiş bölümleri mevcut ayarların üzerine uygular. Dosyada olmayan bölüm değişmez.
    /// Dock başlat simgesinin dosya yolu makineye özgüdür: içe aktarmada yerel değer korunur.
    /// Girdi değiştirilmez; sonuç yeni bir kayıttır ve profille nesne paylaşmaz: ayar bölümlerinin alanları
    /// yerinde değiştirilebilir (<c>settings.Notch.ControlCenterLayout = ...</c>), paylaşılsa uygulanmış ayardaki
    /// değişiklik profili (ve ikinci kez uygulamayı) bozardı.
    /// </summary>
    public static AppSettings Apply(AppSettings current, ImportedProfile profile, IEnumerable<ProfileSection> chosen)
    {
        var result = current;
        foreach (var section in chosen.Distinct())
        {
            if (!profile.Sections.Contains(section))
                continue;

            result = section switch
            {
                ProfileSection.Appearance => result with
                {
                    Appearance = profile.Settings.Appearance with
                    {
                        AppIconOverrides = current.Appearance.AppIconOverrides,
                        IconPackFolder = current.Appearance.IconPackFolder,
                    },
                },
                ProfileSection.Dock => result with
                {
                    Dock = profile.Settings.Dock with { StartIconPath = current.Dock.StartIconPath },
                },
                ProfileSection.Notch => result with
                {
                    Notch = profile.Settings.Notch with
                    {
                        EnabledPanels = new ValueList<string>(profile.Settings.Notch.EnabledPanels),
                        ControlCenterLayout = new ValueList<string>(profile.Settings.Notch.ControlCenterLayout),
                        LiveBackgroundFilePath = current.Notch.LiveBackgroundFilePath,
                    },
                },
                ProfileSection.Music => result with { Music = profile.Settings.Music with { } },
                ProfileSection.Shortcuts => result with { Shortcuts = profile.Settings.Shortcuts with { } },
                ProfileSection.Focus => result with { Focus = profile.Settings.Focus with { } },
                _ => result,
            };
        }
        return result;
    }

    private static object SectionOf(AppSettings settings, ProfileSection section) => section switch
    {
        ProfileSection.Appearance => settings.Appearance,
        ProfileSection.Dock => settings.Dock,
        ProfileSection.Notch => settings.Notch,
        ProfileSection.Music => settings.Music,
        ProfileSection.Shortcuts => settings.Shortcuts,
        ProfileSection.Focus => settings.Focus,
        _ => throw new ArgumentOutOfRangeException(nameof(section)),
    };

    private static bool TryParseSection(string name, out ProfileSection section)
    {
        switch (name)
        {
            case "Appearance": section = ProfileSection.Appearance; return true;
            case "Dock": section = ProfileSection.Dock; return true;
            case "Notch": section = ProfileSection.Notch; return true;
            case "Music": section = ProfileSection.Music; return true;
            case "Shortcuts": section = ProfileSection.Shortcuts; return true;
            case "Focus": section = ProfileSection.Focus; return true;
            default: section = default; return false;
        }
    }

    private static string? StringOf(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static bool TryIntOf(JsonObject obj, string key, out int number)
    {
        number = 0;
        return obj[key] is JsonValue value && value.TryGetValue<int>(out number);
    }

    private static ProfileImportResult Fail(ProfileImportError error) => new(error, null);
}
