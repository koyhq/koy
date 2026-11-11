using System.Reflection;

namespace Koy.Core.Settings;

/// <summary>
/// Varsayılandan farklı ayarları bulur ("Yalnız değişenleri göster" ve satır vurgusu için).
/// Bölüm kayıtlarının her yazılabilir alanı tek tek karşılaştırılır. Eşitlik kayıt eşitliğinden ve
/// ValueList / ValueDictionary eşitliğinden gelir. Anahtar biçimi "Bölüm.Alan" (örn. "Dock.Enabled").
/// Yansıma yalnız bu karşılaştırmada kullanılır; JSON'a dokunmaz (AGENTS #10).
/// Sürüm alanı (<see cref="AppSettings.Version"/>) ve uygulamanın kendi yazdığı durum alanları (<see cref="InternalKeys"/>)
/// kullanıcı ayarı olmadığı için hiç karşılaştırılmaz.
/// </summary>
public static class SettingsChanges
{
    // İlk açılıştan sonra hep varsayılandan farklıdır ve "Varsayılana dön" bunları bilerek korur (FirstRunPolicy);
    // sayılsalar General sayfası sıfırlandıktan sonra bile "değişmiş" görünürdü.
    private static readonly HashSet<string> InternalKeys =
    [
        "General.FirstRunCompleted",
        "General.LastSeenVersion",
        "Weather.LocationUpdatedUnix",
    ];

    public static IReadOnlyList<string> ChangedKeys(AppSettings current, AppSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(defaults);

        var changed = new List<string>();
        foreach (var section in EditableProperties(typeof(AppSettings)))
        {
            if (section.Name == nameof(AppSettings.Version))
            {
                continue;
            }

            if (IsSection(section.PropertyType))
            {
                var currentSection = section.GetValue(current);
                var defaultSection = section.GetValue(defaults);
                foreach (var field in EditableProperties(section.PropertyType))
                {
                    var key = $"{section.Name}.{field.Name}";
                    if (!InternalKeys.Contains(key) && !Equals(field.GetValue(currentSection), field.GetValue(defaultSection)))
                    {
                        changed.Add(key);
                    }
                }
            }
            else if (!Equals(section.GetValue(current), section.GetValue(defaults)))
            {
                changed.Add(section.Name);
            }
        }

        return changed;
    }

    /// <summary>Bir sayfanın (bölümün) varsayılandan farklı en az bir ayarı var mı.</summary>
    public static bool HasChanges(IEnumerable<string> changedKeys, string section)
    {
        ArgumentNullException.ThrowIfNull(changedKeys);
        ArgumentException.ThrowIfNullOrEmpty(section);

        var prefix = section + ".";
        return changedKeys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal) || k == section);
    }

    private static IEnumerable<PropertyInfo> EditableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

    // Bölümler sınıf (sealed record); string gibi skaler referans tipleri bölüm sayılmaz.
    private static bool IsSection(Type type) => type.IsClass && type != typeof(string);
}
