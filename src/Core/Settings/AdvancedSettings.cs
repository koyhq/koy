using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Koy.Core.Settings;

/// <summary>Gelişmiş sayfasındaki düzenleyici türü.</summary>
public enum AdvancedSettingKind
{
    Bool,
    Integer,
    Number,
    Text,
    Choice,
}

/// <summary>Gelişmiş sayfasında bir satır: anahtar ("Notch.ClockShowSeconds"), tür, şu anki ve varsayılan değer (değişmez kültürde metin).</summary>
public sealed record AdvancedSetting
{
    public string Key { get; init; }
    public AdvancedSettingKind Kind { get; init; }
    public string Value { get; init; }
    public string Default { get; init; }
    public IReadOnlyList<string> Choices { get; init; }
    public string Section { get; init; }
    public string PropertyName { get; init; }

    public AdvancedSetting(
        string key,
        AdvancedSettingKind kind,
        string value,
        string @default,
        IReadOnlyList<string> choices,
        string section = "",
        string propertyName = "")
    {
        Key = key ?? "";
        Kind = kind;
        Value = value ?? "";
        Default = @default ?? "";
        Choices = choices ?? [];
        Section = !string.IsNullOrEmpty(section)
            ? section
            : (key?.IndexOf('.') >= 0 ? key[..key.IndexOf('.')] : "");
        PropertyName = !string.IsNullOrEmpty(propertyName)
            ? propertyName
            : (key?.IndexOf('.') >= 0 ? key[(key.IndexOf('.') + 1)..] : (key ?? ""));
    }

    public bool IsChanged => !string.Equals(Value, Default, StringComparison.Ordinal);

    /// <summary>Geçerli arayüz kültürüne göre kullanıcı dostu başlık (Türkçe/İngilizce).</summary>
    public string Title => GetFriendlyTitle();

    /// <summary>Kullanıcı dostu başlık için takma ad (Title ile aynı).</summary>
    public string FriendlyTitle => Title;

    /// <summary>Geçerli arayüz kültürüne göre bölümün kullanıcı dostu başlığı.</summary>
    public string SectionTitle => AdvancedSettings.GetSectionTitle(Section);

    /// <summary>Belirtilen kültüre veya dile göre kullanıcı dostu başlık döner.</summary>
    public string GetFriendlyTitle(string? languageOrCulture = null) =>
        AdvancedSettings.GetFriendlyTitle(PropertyName, languageOrCulture);

    /// <summary>Belirtilen kültüre veya dile göre bölüm başlığı döner.</summary>
    public string GetSectionTitle(string? languageOrCulture = null) =>
        AdvancedSettings.GetSectionTitle(Section, languageOrCulture);

    public void Deconstruct(
        out string key,
        out AdvancedSettingKind kind,
        out string value,
        out string @default,
        out IReadOnlyList<string> choices)
    {
        key = Key;
        kind = Kind;
        value = Value;
        @default = Default;
        choices = Choices;
    }

    public void Deconstruct(
        out string key,
        out AdvancedSettingKind kind,
        out string value,
        out string @default,
        out IReadOnlyList<string> choices,
        out string section,
        out string propertyName)
    {
        key = Key;
        kind = Kind;
        value = Value;
        @default = Default;
        choices = Choices;
        section = Section;
        propertyName = PropertyName;
    }
}

/// <summary>Gelişmiş sayfasında bölüm bazında gruplanmış ayarlar.</summary>
public sealed record AdvancedSettingGroup(
    string Section,
    string Title,
    IReadOnlyList<AdvancedSetting> Items)
{
    public string SectionTitle => Title;
    public bool HasChanged => Items.Any(i => i.IsChanged);
}

/// <summary>Bir değerin neden yazılmadığı (App metni çevirir).</summary>
public enum AdvancedSettingError
{
    None,
    UnknownKey,
    InvalidValue,
}

/// <summary>
/// Gelişmiş sayfası (issue #69): kendi arayüzü olmayan her basit ayar, ayar modelinden kendiliğinden listelenir
/// (elle tablo yok). Değer yazma JSON ağacı üzerinden yapılır ve kaynak üreticili bağlamla katı okunur: tanınmayan,
/// türüne sığmayan ya da biçimi bozuk değer hiçbir zaman ayara geçmez (tek alan bile düşerse reddedilir). Alan başına
/// sayı aralığı modelde tanımlı değil; aralıklar değeri kullanan politikada kenetlenir (ör. FontScalePolicy). Yansıma yalnız
/// listeleme ve okuma içindir; JSON yansımalı değildir (AGENTS #10).
/// </summary>
public static class AdvancedSettings
{
    // Uygulamanın kendi yazdığı durumlar ve sürüm: kullanıcıya düzenletilmez.
    private static readonly HashSet<string> Internal = new(StringComparer.Ordinal)
    {
        "General.FirstRunCompleted",
        "General.LastSeenVersion",
        "Weather.LocationUpdatedUnix",
    };

    // Koy'un kendi verisi (sıra, klasörler) ve ekran başına ayarlar: kendi akışları var.
    private static readonly HashSet<string> SkippedSections = new(StringComparer.Ordinal)
    {
        nameof(AppSettings.DockItems),
        nameof(AppSettings.Displays),
        nameof(AppSettings.QuickLaunch),
    };

    private static readonly Dictionary<string, (string Tr, string En)> SectionTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["General"] = ("Genel", "General"),
        ["Appearance"] = ("Görünüm", "Appearance"),
        ["Dock"] = ("Dock", "Dock"),
        ["DockItems"] = ("Dock Öğeleri", "Dock Items"),
        ["Notch"] = ("Çentik", "Notch"),
        ["Music"] = ("Müzik", "Music"),
        ["Displays"] = ("Ekranlar", "Displays"),
        ["Shortcuts"] = ("Kısayollar", "Shortcuts"),
        ["HotCorners"] = ("Sıcak Köşeler", "Hot Corners"),
        ["Voice"] = ("Sesli Komut", "Voice Command"),
        ["Search"] = ("Arama", "Search"),
        ["Focus"] = ("Odak", "Focus"),
        ["Weather"] = ("Hava Durumu", "Weather"),
        ["Clipboard"] = ("Pano", "Clipboard"),
        ["QuickLaunch"] = ("Hızlı Başlat", "Quick Launch"),
    };

    private static readonly Dictionary<string, (string Tr, string En)> PropertyTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        // Appearance
        ["BaseFontSize"] = ("Taban Yazı Boyutu", "Base Font Size"),
        ["ClockFontFamily"] = ("Saat Yazı Tipi", "Clock Font"),
        ["FontFamily"] = ("Yazı Tipi Ailesi", "Font Family"),
        ["FontWeight"] = ("Yazı Kalınlığı", "Font Weight"),
        ["IconPack"] = ("Simge Paketi", "Icon Pack"),
        ["IconPackFolder"] = ("Simge Paketi Klasörü", "Icon Pack Folder"),
        ["UiScalePercent"] = ("Arayüz Ölçeği (%)", "UI Scale (%)"),
        ["UserStyleOverridesEnabled"] = ("Kullanıcı Stil Dosyası", "User Style Overrides"),

        // General
        ["DeveloperMode"] = ("Geliştirici Modu", "Developer Mode"),
        ["Language"] = ("Dil", "Language"),
        ["AutoCheckUpdates"] = ("Otomatik Güncelleme Denetimi", "Auto Check Updates"),
        ["UpdateChannel"] = ("Güncelleme Kanalı", "Update Channel"),
        ["TemperatureUnit"] = ("Sıcaklık Birimi", "Temperature Unit"),
        ["WindSpeedUnit"] = ("Rüzgâr Hızı Birimi", "Wind Speed Unit"),
        ["NetworkSpeedUnit"] = ("Ağ Hızı Birimi", "Network Speed Unit"),

        // Dock
        ["LengthPercent"] = ("Uzunluk (%)", "Length (%)"),
        ["EdgeOffsetDip"] = ("Kenar Uzaklığı (DIP)", "Edge Offset (DIP)"),
        ["InnerPaddingDip"] = ("İç Dolgu (DIP)", "Inner Padding (DIP)"),
        ["IconSpacingDip"] = ("Simge Boşluğu (DIP)", "Icon Spacing (DIP)"),
        ["IndicatorColor"] = ("Gösterge Rengi", "Indicator Color"),
        ["IndicatorCustomColor"] = ("Özel Gösterge Rengi", "Custom Indicator Color"),
        ["IndicatorOnePerWindow"] = ("Pencere Başına Gösterge", "Indicator Per Window"),
        ["BadgeColor"] = ("Rozet Rengi", "Badge Color"),
        ["BadgeCustomColor"] = ("Özel Rozet Rengi", "Custom Badge Color"),
        ["BadgeCorner"] = ("Rozet Köşesi", "Badge Corner"),
        ["BadgeStyle"] = ("Rozet Stili", "Badge Style"),
        ["RevealTrigger"] = ("Belirme Tetikleyicisi", "Reveal Trigger"),
        ["RevealDelayMs"] = ("Belirme Gecikmesi (ms)", "Reveal Delay (ms)"),
        ["HiddenEdgeLine"] = ("Gizli Kenar Çizgisi", "Hidden Edge Line"),
        ["GroupingMode"] = ("Gruplama Kipi", "Grouping Mode"),
        ["WindowMonitorScope"] = ("Pencere Monitör Kapsamı", "Window Monitor Scope"),
        ["LeftClickAction"] = ("Sol Tık Eylemi", "Left Click Action"),
        ["LabelMode"] = ("Etiket Kipi", "Label Mode"),
        ["NewTrayIconVisibility"] = ("Yeni Tepsi Simgesi Görünürlüğü", "New Tray Icon Visibility"),

        // Notch
        ["OpenDelayMs"] = ("Açılma Gecikmesi (ms)", "Open Delay (ms)"),
        ["CloseDelayMs"] = ("Kapanma Gecikmesi (ms)", "Close Delay (ms)"),
        ["TabBarStyle"] = ("Sekme Çubuğu Stili", "Tab Bar Style"),
        ["ClockHourFormat"] = ("Saat Biçimi (12/24)", "Clock Format (12/24)"),
        ["ClockShowSeconds"] = ("Saniyeyi Göster", "Show Seconds"),
        ["ClockShowDate"] = ("Tarihi Göster", "Show Date"),
        ["ClockShowWeekNumber"] = ("Hafta Numarasını Göster", "Show Week Number"),
        ["ClockFirstDayOfWeek"] = ("Haftanın İlk Günü", "First Day of Week"),
        ["ClockCustomDateFormat"] = ("Özel Tarih Biçimi", "Custom Date Format"),
        ["ClockSecondTimeZoneId"] = ("İkinci Saat Dilimi", "Second Time Zone"),
        ["LiveBackgroundTarget"] = ("Canlı Arka Plan Hedefi", "Live Background Target"),
        ["LiveBackgroundSource"] = ("Canlı Arka Plan Kaynağı", "Live Background Source"),
        ["LiveBackgroundPreset"] = ("Canlı Arka Plan Önayarı", "Live Background Preset"),
        ["LiveBackgroundFilePath"] = ("Canlı Arka Plan Dosyası", "Live Background File"),
        ["LiveBackgroundScrimPercent"] = ("Canlı Arka Plan Perdesi (%)", "Live Background Scrim (%)"),

        // Music
        ["VisualizerStyle"] = ("Görselleştirici Stili", "Visualizer Style"),
        ["VisualizerColorSource"] = ("Görselleştirici Renk Kaynağı", "Visualizer Color Source"),
        ["VisualizerBarCount"] = ("Görselleştirici Çubuk Sayısı", "Visualizer Bar Count"),

        // Search
        ["RetainPrior"] = ("Son Sorguyu Koru", "Retain Prior Query"),
        ["HistoryMode"] = ("Geçmiş Kipi", "History Mode"),
        ["Pinned"] = ("Sabitli", "Pinned"),
    };

    /// <summary>
    /// Bölümün kullanıcı dostu başlığını döner. Tanımlı değilse PascalCase kelimelerine ayrılır.
    /// </summary>
    public static string GetSectionTitle(string section, string? languageOrCulture = null)
    {
        if (string.IsNullOrWhiteSpace(section))
            return "";

        var isTurkish = IsTurkish(languageOrCulture);
        if (SectionTitles.TryGetValue(section, out var pair))
            return isTurkish ? pair.Tr : pair.En;

        return SplitPascalCase(section);
    }

    /// <summary>
    /// Ayar alanının kullanıcı dostu başlığını döner. Tanımlı değilse PascalCase kelimelerine ayrılır.
    /// </summary>
    public static string GetFriendlyTitle(string propertyName, string? languageOrCulture = null)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
            return "";

        if (propertyName.Contains('.'))
        {
            var parts = propertyName.Split('.');
            propertyName = parts[^1];
        }

        var isTurkish = IsTurkish(languageOrCulture);
        if (PropertyTitles.TryGetValue(propertyName, out var pair))
            return isTurkish ? pair.Tr : pair.En;

        return SplitPascalCase(propertyName);
    }

    /// <summary>
    /// PascalCase metni kelimelerine ayırır ("BaseFontSize" -> "Base Font Size").
    /// </summary>
    public static string SplitPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "";

        var sb = new StringBuilder(input.Length + 8);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                char prev = input[i - 1];
                if (char.IsLower(prev) || char.IsDigit(prev) ||
                    (char.IsUpper(prev) && i + 1 < input.Length && char.IsLower(input[i + 1])))
                {
                    sb.Append(' ');
                }
            }
            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Listelenecek ayarlar: <paramref name="dedicatedUiKeys"/> içinde olmayan bool, sayı, metin ve enum alanları,
    /// bölüm ve ad sırasıyla.
    /// </summary>
    public static IReadOnlyList<AdvancedSetting> List(AppSettings current, IEnumerable<string> dedicatedUiKeys)
    {
        ArgumentNullException.ThrowIfNull(current);
        var dedicated = dedicatedUiKeys.ToHashSet(StringComparer.Ordinal);
        var defaults = new AppSettings();
        var result = new List<AdvancedSetting>();

        foreach (var section in Sections())
        {
            var currentSection = section.GetValue(current);
            var defaultSection = section.GetValue(defaults);
            foreach (var field in Fields(section.PropertyType))
            {
                var key = $"{section.Name}.{field.Name}";
                if (Internal.Contains(key) || dedicated.Contains(key))
                    continue;

                var kind = KindOf(field.PropertyType);
                if (kind is null)
                    continue;

                var choices = kind == AdvancedSettingKind.Choice ? Enum.GetNames(field.PropertyType) : [];
                result.Add(new AdvancedSetting(
                    key,
                    kind.Value,
                    Format(field.GetValue(currentSection)),
                    Format(field.GetValue(defaultSection)),
                    choices,
                    section.Name,
                    field.Name));
            }
        }

        return result;
    }

    /// <summary>
    /// Ayarları bölümlerine göre gruplar ve her bölüme kullanıcı dostu başlık atar.
    /// </summary>
    public static IReadOnlyList<AdvancedSettingGroup> GroupBySection(
        IEnumerable<AdvancedSetting> settings,
        string? languageOrCulture = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings
            .GroupBy(s => s.Section, StringComparer.Ordinal)
            .Select(g => new AdvancedSettingGroup(
                g.Key,
                GetSectionTitle(g.Key, languageOrCulture),
                g.ToList()))
            .ToList();
    }

    /// <summary>
    /// Belirtilen ayarları doğrudan bölüm bazında gruplanmış liste olarak döner.
    /// </summary>
    public static IReadOnlyList<AdvancedSettingGroup> ListGrouped(
        AppSettings current,
        IEnumerable<string> dedicatedUiKeys,
        string? languageOrCulture = null)
    {
        var flat = List(current, dedicatedUiKeys);
        return GroupBySection(flat, languageOrCulture);
    }

    /// <summary>
    /// Değeri ayara yazmayı dener. Başarısızsa <paramref name="updated"/> değişmemiş ayardır ve hata türü döner.
    /// Metin değişmez kültürde okunur (ondalık ayırıcı nokta); bool için true/false, enum için ad.
    /// </summary>
    public static AdvancedSettingError TrySet(AppSettings current, string key, string? text, out AppSettings updated)
    {
        ArgumentNullException.ThrowIfNull(current);
        updated = current;

        var parts = key.Split('.');
        if (parts.Length != 2 || Internal.Contains(key) || SkippedSections.Contains(parts[0]))
            return AdvancedSettingError.UnknownKey;

        var section = Sections().FirstOrDefault(s => s.Name == parts[0]);
        var field = section is null ? null : Fields(section.PropertyType).FirstOrDefault(f => f.Name == parts[1]);
        var kind = field is null ? null : KindOf(field.PropertyType);
        if (section is null || field is null || kind is null)
            return AdvancedSettingError.UnknownKey;

        var value = ToNode(kind.Value, field.PropertyType, text?.Trim() ?? "");
        if (value is null)
            return AdvancedSettingError.InvalidValue;

        var root = JsonSerializer.SerializeToNode(current, SettingsJsonContext.Default.AppSettings) as JsonObject;
        if (root?[section.Name] is not JsonObject sectionNode)
            return AdvancedSettingError.UnknownKey;

        sectionNode[field.Name] = value;
        var parsed = SettingsStore.ParseTolerant(root, out var dropped);
        if (dropped > 0)
            return AdvancedSettingError.InvalidValue;

        updated = parsed.FillMissingSections();
        return AdvancedSettingError.None;
    }

    /// <summary>Satırı varsayılana döndürür.</summary>
    public static AdvancedSettingError Reset(AppSettings current, string key, out AppSettings updated)
    {
        var row = List(current, []).FirstOrDefault(r => r.Key == key);
        if (row is null)
        {
            updated = current;
            return AdvancedSettingError.UnknownKey;
        }

        return TrySet(current, key, row.Default, out updated);
    }

    private static bool IsTurkish(string? languageOrCulture)
    {
        if (string.IsNullOrWhiteSpace(languageOrCulture))
            return AppLanguage.IsTurkish(CultureInfo.CurrentUICulture);

        return languageOrCulture.StartsWith("tr", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<PropertyInfo> Sections() =>
        typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.PropertyType.IsClass && p.PropertyType != typeof(string)
                && !SkippedSections.Contains(p.Name));

    private static IEnumerable<PropertyInfo> Fields(Type sectionType) =>
        sectionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    private static AdvancedSettingKind? KindOf(Type type)
    {
        if (type == typeof(bool)) return AdvancedSettingKind.Bool;
        if (type == typeof(int) || type == typeof(long)) return AdvancedSettingKind.Integer;
        if (type == typeof(double) || type == typeof(float)) return AdvancedSettingKind.Number;
        if (type == typeof(string)) return AdvancedSettingKind.Text;
        if (type.IsEnum) return AdvancedSettingKind.Choice;
        return null; // liste, sözlük, iç içe kayıt, nullable: kendi arayüzleri var ya da bu tabloya uymaz
    }

    private static string Format(object? value) => value switch
    {
        null => "",
        bool b => b ? "true" : "false",
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable x => x.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static JsonNode? ToNode(AdvancedSettingKind kind, Type type, string text)
    {
        switch (kind)
        {
            case AdvancedSettingKind.Bool:
                return bool.TryParse(text, out var b) ? JsonValue.Create(b) : null;
            case AdvancedSettingKind.Integer:
                return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? JsonValue.Create(l) : null;
            case AdvancedSettingKind.Number:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d)
                    ? JsonValue.Create(d) : null;
            case AdvancedSettingKind.Text:
                return text.Any(char.IsControl) ? null : JsonValue.Create(text);
            case AdvancedSettingKind.Choice:
                // Yalnız tanımlı ad kabul edilir; sayı ("3") ya da tanımsız değer enum'a sızmasın.
                var name = Enum.GetNames(type).FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
                return name is null ? null : JsonValue.Create(name);
            default:
                return null;
        }
    }
}
