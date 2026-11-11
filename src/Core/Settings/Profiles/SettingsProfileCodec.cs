using System.Text.Json;
using System.Text.Json.Nodes;

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

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>Ad boş olmamalı, en fazla 60 karakter olmalı ve denetim karakteri içermemeli.</summary>
    public static bool IsValidName(string? name)
    {
        if (name is null)
            return false;
        var trimmed = name.Trim();
        return trimmed.Length is > 0 and <= MaxNameLength && !trimmed.Any(char.IsControl);
    }

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

        // Dock başlat simgesinin yolu bu makineye özgü: paylaşılan dosyaya yazılmaz (içe aktarmada yerel değer korunur).
        var shareable = current with
        {
            Appearance = current.Appearance with { AppIconOverrides = new() },
            Dock = current.Dock with { StartIconPath = "" },
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
    /// Bölüm İÇİNDEKİ bilinmeyen anahtarlar sessizce yok sayılır: ileri sürümün eklediği alan bu sürüme taşınmaz.
    /// </summary>
    public static ProfileImportResult Read(string json)
    {
        if (json is null)
            return Fail(ProfileImportError.NotAProfile);
        if (json.Length > MaxLength)
            return Fail(ProfileImportError.TooLarge);

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
        try
        {
            settings = SettingsStore.ParseTolerant(scoped, out dropped).FillMissingSections();
        }
        catch (JsonException)
        {
            // Hata yolu çözülemezse ParseTolerant onarmaz ve istisna çıkar: dosya temiz biçimde reddedilir, hiçbir şey yazılmaz.
            return Fail(ProfileImportError.InvalidValues);
        }
        if (dropped > 0)
            return Fail(ProfileImportError.InvalidValues);

        return new ProfileImportResult(ProfileImportError.None, new ImportedProfile(name!.Trim(), sections, settings));
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
    /// Girdi değiştirilmez; sonuç yeni bir kayıttır.
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
                    Appearance = profile.Settings.Appearance with { AppIconOverrides = current.Appearance.AppIconOverrides },
                },
                ProfileSection.Dock => result with
                {
                    Dock = profile.Settings.Dock with { StartIconPath = current.Dock.StartIconPath },
                },
                ProfileSection.Notch => result with { Notch = profile.Settings.Notch },
                ProfileSection.Music => result with { Music = profile.Settings.Music },
                ProfileSection.Shortcuts => result with { Shortcuts = profile.Settings.Shortcuts },
                ProfileSection.Focus => result with { Focus = profile.Settings.Focus },
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
