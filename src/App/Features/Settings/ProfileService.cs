using System.Windows;
using Koy.App.Features.Menu;
using Koy.App.Resources;
using Koy.Core.Settings;
using Koy.Core.Settings.Profiles;

namespace Koy.App.Features.Settings;

/// <summary>
/// Kayıtlı profillerin uygulama tarafı girişi: listeler, kaydeder, içe/dışa aktarır ve profili uygular. Mantığın hepsi
/// Core'daki <see cref="ProfileLibrary"/> ve <see cref="SettingsProfileCodec"/> içindedir; burası dosyaları ve çalışan
/// özellikleri bağlar. Ayarlar penceresinden bağımsızdır: bağlam kuralları da aynı <see cref="Apply"/> çağrısını kullanır.
/// Hepsi arayüz iş parçacığında çağrılmalıdır (Apply çalışan çentik ve dock'u yeniden kurar).
/// </summary>
public sealed class ProfileService(SettingsStore settings, ProfileStore profiles)
{
    public static ProfileService Instance { get; } = new(new SettingsStore(SettingsStore.DefaultPath), new ProfileStore(ProfileStore.DefaultPath));

    /// <summary>Bir profil uygulandıktan sonra (ayarlar kaydedilmiş, özellikler yeniden kurulmuş). Açık Koy penceresi buradan yenilenir.</summary>
    public event Action<SavedProfile>? Applied;

    public IReadOnlyList<SavedProfile> List() => profiles.Load();

    /// <summary>Diskteki geçerli ayarlar (dışa aktarma ve içe aktarma önizlemesi için).</summary>
    public AppSettings CurrentSettings() => settings.Load();

    /// <summary>Kimliğe ya da ada göre kayıtlı profil; yoksa null.</summary>
    public SavedProfile? Find(string? idOrName) => ProfileLibrary.Find(List(), idOrName);

    public ProfileLibraryResult CreateFromCurrent(string name, IEnumerable<ProfileSection> sections) =>
        Persist(ProfileLibrary.Create(List(), settings.Load(), name, sections, NewId()));

    public ProfileLibraryResult AddImported(ImportedProfile imported) =>
        Persist(ProfileLibrary.AddImported(List(), imported, NewId()));

    public ProfileLibraryResult Rename(string id, string newName) => Persist(ProfileLibrary.Rename(List(), id, newName));

    public ProfileLibraryResult Duplicate(string id) => Persist(ProfileLibrary.Duplicate(List(), id, NewId()));

    public ProfileLibraryResult Delete(string id) => Persist(ProfileLibrary.Delete(List(), id));

    /// <summary>
    /// Profili uygular. Ayar değişikliği elle yapılmış gibi yürür: ayar dosyası kaydedilir, tema yenilenir, çentik ve dock
    /// yeni ayarla yeniden kurulur (dock ayarları <c>DockFeature.ApplySettings</c> / <c>DockSettingsDiff</c> üzerinden,
    /// ayarlar sıfırlamasıyla aynı yol). Profil zaten geçerli ayarlarla aynıysa hiçbir şey yeniden kurulmaz.
    /// </summary>
    public ProfileApplyResult Apply(string? idOrName)
    {
        var result = ProfileLibrary.Apply(List(), settings.Load(), idOrName);
        if (!result.IsSuccess || result.ChangedSections.Count == 0)
            return result;

        settings.Save(result.Settings!);

        if (Application.Current is App app)
        {
            var saved = settings.Load();
            ThemeManager.Apply(app, saved);
            app.ReloadAllFeatures();
            // Dock / çentik açık-kapalı değiştiyse pencere ve sağ alt menü anahtarları da eşitlenir.
            KoyWindow.SyncFeatureStates(saved.Notch.Enabled, saved.Dock.Enabled);
            SagAltMenuWindow.SyncFeatureStates(saved.Notch.Enabled, saved.Dock.Enabled);
        }

        Applied?.Invoke(result.Profile!);
        return result;
    }

    private ProfileLibraryResult Persist(ProfileLibraryResult result)
    {
        if (!result.IsSuccess)
            return result;
        return profiles.Save(result.Profiles) ? result : result with { Error = ProfileLibraryError.SaveFailed };
    }

    private static string NewId() => Guid.NewGuid().ToString("N");
}
