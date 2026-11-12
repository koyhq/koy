namespace Koy.Core.Settings.Profiles;

public enum ProfileLibraryError
{
    None,
    InvalidName,
    DuplicateName,
    NoSections,
    NotFound,
    LimitReached,
    /// <summary>Kayıtlı metin artık doğrulamadan geçmiyor (dosya elle bozulmuş).</summary>
    CorruptProfile,
    /// <summary>İşlem geçerliydi ama profil dosyası yazılamadı (disk dolu, kilit). Kütüphane üretmez, çağıran ekler.</summary>
    SaveFailed,
}

/// <summary>Kütüphane işleminin sonucu: hata yoksa <see cref="Profiles"/> yeni liste, <see cref="Profile"/> etkilenen kayıt.</summary>
public sealed record ProfileLibraryResult(ProfileLibraryError Error, IReadOnlyList<SavedProfile> Profiles, SavedProfile? Profile)
{
    public bool IsSuccess => Error == ProfileLibraryError.None;
}

/// <summary>Kayıtlı profili ayarlara uygulama sonucu. Hata yoksa <see cref="Settings"/> kaydedilecek yeni ayarlardır.</summary>
public sealed record ProfileApplyResult(
    ProfileLibraryError Error, SavedProfile? Profile, AppSettings? Settings, IReadOnlyList<ProfileSection> ChangedSections)
{
    public bool IsSuccess => Error == ProfileLibraryError.None && Settings is not null;
}

/// <summary>
/// Kayıtlı profil listesi üzerindeki saf işlemler: oluştur, içe al, yeniden adlandır, çoğalt, sil, bul, uygula.
/// Dosya G/Ç yok (bkz. <see cref="ProfileStore"/>); girdi liste değiştirilmez, her işlem yeni liste döner.
/// Bağlam kuralları (uygulamaya göre profil) profili <see cref="Find"/> + <see cref="Apply"/> ile kullanır.
/// </summary>
public static class ProfileLibrary
{
    public const int MaxProfiles = 50;

    /// <summary>İçe aktarılan dosyanın en büyük bayt boyutu (UTF-8'de karakter başına en çok 4 bayt): dosya belleğe okunmadan reddedilir.</summary>
    public const long MaxFileBytes = SettingsProfileCodec.MaxLength * 4L;

    /// <summary>Önce kimlik (birebir), sonra ad (büyük/küçük harf duyarsız, kırpılmış). Bulunamazsa null.</summary>
    public static SavedProfile? Find(IReadOnlyList<SavedProfile> profiles, string? idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
            return null;
        return profiles.FirstOrDefault(p => p.Id == idOrName)
            ?? profiles.FirstOrDefault(p => string.Equals(p.Name, idOrName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Profilin taşıdığı bölümler; metin geçersizse boş liste.</summary>
    public static IReadOnlyList<ProfileSection> SectionsOf(SavedProfile profile)
    {
        var result = SettingsProfileCodec.Read(profile.Json);
        return result.IsValid ? result.Profile!.Sections : [];
    }

    /// <summary>Geçerli ayarlardan seçili bölümlerle yeni profil. Ad benzersiz olmalı (büyük/küçük harf duyarsız).</summary>
    public static ProfileLibraryResult Create(
        IReadOnlyList<SavedProfile> profiles, AppSettings current, string name, IEnumerable<ProfileSection> sections, string newId)
    {
        if (!SettingsProfileCodec.IsValidName(name))
            return Fail(profiles, ProfileLibraryError.InvalidName);
        var chosen = sections.Distinct().ToList();
        if (chosen.Count == 0 || !chosen.All(Enum.IsDefined))
            return Fail(profiles, ProfileLibraryError.NoSections);
        if (profiles.Count >= MaxProfiles)
            return Fail(profiles, ProfileLibraryError.LimitReached);
        if (NameTaken(profiles, name, exceptId: null))
            return Fail(profiles, ProfileLibraryError.DuplicateName);

        var saved = new SavedProfile(newId, name.Trim(), SettingsProfileCodec.Export(current, name, chosen));
        return Ok([.. profiles, saved], saved);
    }

    /// <summary>
    /// Doğrulanmış içe aktarılan profili listeye ekler. Ad çakışırsa "Ad (2)" gibi benzersiz ad verilir (içe aktarma
    /// mevcut profili sessizce ezmez).
    /// </summary>
    public static ProfileLibraryResult AddImported(IReadOnlyList<SavedProfile> profiles, ImportedProfile imported, string newId)
    {
        if (profiles.Count >= MaxProfiles)
            return Fail(profiles, ProfileLibraryError.LimitReached);

        var name = UniqueName(profiles, imported.Name);
        var saved = new SavedProfile(newId, name, SettingsProfileCodec.Export(imported.Settings, name, imported.Sections));
        return Ok([.. profiles, saved], saved);
    }

    public static ProfileLibraryResult Rename(IReadOnlyList<SavedProfile> profiles, string id, string newName)
    {
        var target = profiles.FirstOrDefault(p => p.Id == id);
        if (target is null)
            return Fail(profiles, ProfileLibraryError.NotFound);
        if (!SettingsProfileCodec.IsValidName(newName))
            return Fail(profiles, ProfileLibraryError.InvalidName);
        if (NameTaken(profiles, newName, exceptId: id))
            return Fail(profiles, ProfileLibraryError.DuplicateName);

        var read = SettingsProfileCodec.Read(target.Json);
        if (!read.IsValid)
            return Fail(profiles, ProfileLibraryError.CorruptProfile);

        // Dosya içindeki ad da değişir: dışa aktarılan dosya listedeki adı taşısın.
        var renamed = target with
        {
            Name = newName.Trim(),
            Json = SettingsProfileCodec.Export(read.Profile!.Settings, newName, read.Profile.Sections),
        };
        return Ok(profiles.Select(p => p.Id == id ? renamed : p).ToList(), renamed);
    }

    public static ProfileLibraryResult Duplicate(IReadOnlyList<SavedProfile> profiles, string id, string newId)
    {
        var source = profiles.FirstOrDefault(p => p.Id == id);
        if (source is null)
            return Fail(profiles, ProfileLibraryError.NotFound);
        if (profiles.Count >= MaxProfiles)
            return Fail(profiles, ProfileLibraryError.LimitReached);

        var read = SettingsProfileCodec.Read(source.Json);
        if (!read.IsValid)
            return Fail(profiles, ProfileLibraryError.CorruptProfile);

        var name = UniqueName(profiles, source.Name);
        var copy = new SavedProfile(newId, name, SettingsProfileCodec.Export(read.Profile!.Settings, name, read.Profile.Sections));
        return Ok([.. profiles, copy], copy);
    }

    public static ProfileLibraryResult Delete(IReadOnlyList<SavedProfile> profiles, string id)
    {
        var target = profiles.FirstOrDefault(p => p.Id == id);
        if (target is null)
            return Fail(profiles, ProfileLibraryError.NotFound);
        return Ok(profiles.Where(p => p.Id != id).ToList(), target);
    }

    /// <summary>
    /// Profili geçerli ayarların üzerine uygular (yalnız profilin bölümleri değişir). Çağıran sonucu bilinen ayar
    /// kayıt yolundan geçirir; burada dosya yazılmaz.
    /// </summary>
    public static ProfileApplyResult Apply(IReadOnlyList<SavedProfile> profiles, AppSettings current, string? idOrName)
    {
        var profile = Find(profiles, idOrName);
        if (profile is null)
            return new ProfileApplyResult(ProfileLibraryError.NotFound, null, null, []);

        var read = SettingsProfileCodec.Read(profile.Json);
        if (!read.IsValid)
            return new ProfileApplyResult(ProfileLibraryError.CorruptProfile, profile, null, []);

        var imported = read.Profile!;
        var changed = SettingsProfileCodec.ChangedSections(current, imported);
        var settings = SettingsProfileCodec.Apply(current, imported, imported.Sections);
        return new ProfileApplyResult(ProfileLibraryError.None, profile, settings, changed);
    }

    /// <summary>Listede olmayan ad: "Ad", yoksa "Ad (2)", "Ad (3)"… Ad sınırını aşmamak için taban kısaltılır.</summary>
    public static string UniqueName(IReadOnlyList<SavedProfile> profiles, string baseName)
    {
        var trimmed = baseName.Trim();
        if (!NameTaken(profiles, trimmed, exceptId: null))
            return trimmed;

        for (var n = 2; ; n++)
        {
            var suffix = " (" + n + ")";
            var stem = trimmed.Length + suffix.Length > SettingsProfileCodec.MaxNameLength
                ? trimmed[..(SettingsProfileCodec.MaxNameLength - suffix.Length)].TrimEnd()
                : trimmed;
            var candidate = stem + suffix;
            if (!NameTaken(profiles, candidate, exceptId: null))
                return candidate;
        }
    }

    private static bool NameTaken(IReadOnlyList<SavedProfile> profiles, string name, string? exceptId) =>
        profiles.Any(p => p.Id != exceptId && string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static ProfileLibraryResult Ok(IReadOnlyList<SavedProfile> profiles, SavedProfile profile) =>
        new(ProfileLibraryError.None, profiles, profile);

    private static ProfileLibraryResult Fail(IReadOnlyList<SavedProfile> profiles, ProfileLibraryError error) =>
        new(error, profiles, null);
}
