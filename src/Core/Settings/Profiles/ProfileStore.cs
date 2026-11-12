namespace Koy.Core.Settings.Profiles;

/// <summary>
/// Kayıtlı profillerin dosyası (ayar klasöründe <c>profiles.json</c>). Okuma hatasında boş liste, yazma hatasında false
/// (bkz. <see cref="JsonListFile{T}"/>); çağıran çökmez. Mantık <see cref="ProfileLibrary"/> içindedir.
/// </summary>
public sealed class ProfileStore(string path)
{
    private readonly JsonListFile<SavedProfile> _file = new(path, DataJsonContext.Default.ListSavedProfile);

    public static string DefaultPath { get; } = JsonListFile.InSettingsFolder("profiles.json");

    public string Path => _file.Path;

    /// <summary>Boş kimlikli, adsız ya da yinelenen kimlikli kayıtlar atılır (elle bozulmuş dosya).</summary>
    public IReadOnlyList<SavedProfile> Load()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return _file.Load()
            .Where(p => p is not null && !string.IsNullOrEmpty(p.Id) && p.Name is not null && p.Json is not null && seen.Add(p.Id))
            .Take(ProfileLibrary.MaxProfiles)
            .ToList();
    }

    public bool Save(IReadOnlyList<SavedProfile> profiles) => _file.Save([.. profiles]);
}
