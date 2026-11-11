using System.Text.Json.Nodes;
using Koy.Core.Display;

namespace Koy.Core.Settings;

/// <summary>
/// Eski sürümlerin ayar dosyalarını güncel şemaya taşır. JSON ağacı üstünde çalışır (tipli okumadan önce): tip
/// değişse bile eski dosya okunabilir ve bilinmeyen değer yalnız kendi alanını bozar. Saf: monitör listesi gerekmez.
/// </summary>
public static class SettingsMigration
{
    /// <summary>Dosyada "Version" yoksa varsayılan: tipli dönemde eksik sürüm güncel sayılırdı.</summary>
    private const int MissingVersionAssumed = 3;

    /// <summary>
    /// Ağacı yerinde güncel şemaya çevirir ve dosyadaki ÖZGÜN sürümü döner.
    /// <para>
    /// Sürüm 2 → 3: tek hedefli <c>MonitorTarget</c> yerine monitör başına ayar.
    /// </para>
    /// <list type="bullet">
    /// <item>"Primary" ve "All": eski varsayılan "yalnız ana ekran" idi ve değiştirmek için arayüz yoktu; çoklu ekran artık
    /// gerçek olduğundan kayıt yazılmaz, bütün ekranlar yeni ekran varsayılanını (hepsi açık) alır.</item>
    /// <item>Belirli ekran (elle yazılmış aygıt adı ya da kimlik): yalnız o ekran açık kalır, diğerleri (yeni ekran
    /// varsayılanı "hiçbiri" olduğundan) kapalı. Anahtar eski haliyle yazılır; ekran ilk bulunduğunda
    /// <see cref="MonitorPolicy.KeyMatches"/> eşler, ilk düzenlemede kararlı kimliğe çevrilir.</item>
    /// </list>
    /// </summary>
    public static int Migrate(JsonObject root)
    {
        var original = ReadVersion(root);
        if (original >= AppSettings.CurrentVersion)
            return original;

        if (original < 3)
            MigrateV2ToV3(root);
        if (original < 4)
            MigrateV3ToV4(root);

        root["Version"] = AppSettings.CurrentVersion;
        return original;
    }

    /// <summary>
    /// Sürüm 3 → 4: (1) sayı ve dile bağlı metin değerleri enum adına çevrilir (<see cref="LegacyValueTranslator"/>),
    /// (2) düz anahtarlar bölümlere taşınır (<see cref="SettingsKeyMap"/>). Tanınmayan değerin anahtarı düşer, alan
    /// varsayılanını alır; tanınmayan üst düzey anahtar (elle yazılmış ya da gelecek sürüm) atılır. Dosyanın tamamı sıfırlanmaz.
    /// </summary>
    private static void MigrateV3ToV4(JsonObject root)
    {
        var sections = new Dictionary<string, JsonObject>();

        foreach (var (key, node) in root.ToList())
        {
            if (!SettingsKeyMap.V3ToV4.TryGetValue(key, out var target))
                continue;

            var value = node;
            if (LegacyValueTranslator.ByKey.TryGetValue(key, out var translate))
            {
                // Açık null değer enum'a okunamaz; tanınmayan sayılır.
                var translated = node is null ? null : translate(node);
                if (translated is null)
                    continue;
                value = JsonValue.Create(translated);
            }
            else
            {
                // Düğüm eski kökün çocuğu; yenisine geçmeden önce ayrılmalı.
                value = node?.DeepClone();
            }

            if (!sections.TryGetValue(target.Section, out var section))
                sections[target.Section] = section = new JsonObject();
            section[target.Name] = value;
        }

        var version = root["Version"]?.DeepClone();
        root.Clear();
        root["Version"] = version;
        foreach (var (name, section) in sections)
            root[name] = section;
    }

    private static int ReadVersion(JsonObject root)
    {
        if (root["Version"] is JsonValue value && value.TryGetValue<int>(out var version))
            return version;
        return MissingVersionAssumed;
    }

    private static void MigrateV2ToV3(JsonObject root)
    {
        var monitors = root["Monitors"] as JsonObject;
        var created = monitors is null;
        monitors ??= new JsonObject();
        var target = root["MonitorTarget"] is JsonValue t && t.TryGetValue<string>(out var s) ? s : null;

        if (DisplayLayoutResolver.ParseMode(target) == MonitorTargetMode.Specific && !string.IsNullOrWhiteSpace(target))
        {
            // Monitör anahtarları büyük/küçük harf duyarsız (MonitorPolicy): aynı ekranın eski kaydı yinelenmesin.
            foreach (var existing in monitors.Select(p => p.Key).Where(k => string.Equals(k, target, StringComparison.OrdinalIgnoreCase)).ToList())
                monitors.Remove(existing);

            monitors[target!] = new JsonObject { ["NotchEnabled"] = true, ["DockEnabled"] = true };
            root["NewScreenDefault"] = nameof(NewScreenMode.None);
        }

        if (created)
            root["Monitors"] = monitors;
    }
}
