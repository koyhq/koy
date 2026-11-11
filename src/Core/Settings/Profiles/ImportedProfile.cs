namespace Koy.Core.Settings.Profiles;

/// <summary>
/// Doğrulanmış bir profil dosyası. <see cref="Settings"/> yalnız <see cref="Sections"/> içindeki bölümleri taşır,
/// diğerleri varsayılandır; bu yüzden uygulanırken yalnız bildirilen bölümlere bakılır.
/// </summary>
public sealed record ImportedProfile(string Name, IReadOnlyList<ProfileSection> Sections, AppSettings Settings);

public enum ProfileImportError
{
    None,
    TooLarge,
    MalformedJson,
    NotAProfile,
    UnsupportedFormatVersion,
    InvalidName,
    UnknownSection,
    NoSections,
    InvalidValues,
}

/// <summary>Okuma sonucu: hata varsa <see cref="Profile"/> null'dır. Hiçbir dosya yazılmaz; yazma çağıranın işidir.</summary>
public sealed record ProfileImportResult(ProfileImportError Error, ImportedProfile? Profile)
{
    public bool IsValid => Error == ProfileImportError.None && Profile is not null;
}
