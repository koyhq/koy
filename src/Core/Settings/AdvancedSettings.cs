using System.Globalization;
using System.Reflection;
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
public sealed record AdvancedSetting(
    string Key,
    AdvancedSettingKind Kind,
    string Value,
    string Default,
    IReadOnlyList<string> Choices)
{
    public bool IsChanged => !string.Equals(Value, Default, StringComparison.Ordinal);
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
                result.Add(new AdvancedSetting(key, kind.Value, Format(field.GetValue(currentSection)), Format(field.GetValue(defaultSection)), choices));
            }
        }

        return result;
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
