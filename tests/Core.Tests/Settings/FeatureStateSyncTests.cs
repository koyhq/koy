using Koy.Core.Appearance;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public sealed class FeatureStateSyncTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-sync-tests-" + Guid.NewGuid());
    private SettingsStore NewStore() => new(Path.Combine(_dir, "settings.json"));

    [Fact]
    public void Reconcile_UpdatesOnlySpecifiedStates()
    {
        var initial = new AppSettings { Notch = new NotchSettings { Enabled = true }, Dock = new DockSettings { Enabled = true } };

        var dockDisabled = FeatureStateSyncPolicy.Reconcile(initial, dockEnabled: false);
        Assert.True(dockDisabled.Notch.Enabled);
        Assert.False(dockDisabled.Dock.Enabled);

        var notchDisabled = FeatureStateSyncPolicy.Reconcile(initial, notchEnabled: false);
        Assert.False(notchDisabled.Notch.Enabled);
        Assert.True(notchDisabled.Dock.Enabled);

        var bothChanged = FeatureStateSyncPolicy.Reconcile(initial, notchEnabled: false, dockEnabled: false);
        Assert.False(bothChanged.Notch.Enabled);
        Assert.False(bothChanged.Dock.Enabled);

        var unchanged = FeatureStateSyncPolicy.Reconcile(initial);
        Assert.Equal(initial, unchanged);
    }

    [Fact]
    public void Reconcile_PreservesAllOtherSettings()
    {
        var complex = new AppSettings { General = new GeneralSettings { Language = "en" }, Appearance = new AppearanceSettings { Theme = AppTheme.Light, CornerRadius = 18.0 }, Dock = new DockSettings { IconSize = 36.0, HideWindowsTaskbar = false, Enabled = true }, Notch = new NotchSettings { Enabled = true } };

        var reconciled = FeatureStateSyncPolicy.Reconcile(complex, dockEnabled: false);

        Assert.False(reconciled.Dock.Enabled);
        Assert.True(reconciled.Notch.Enabled);
        Assert.Equal("en", reconciled.General.Language);
        Assert.Equal(AppTheme.Light, reconciled.Appearance.Theme);
        Assert.Equal(18.0, reconciled.Appearance.CornerRadius);
        Assert.Equal(36.0, reconciled.Dock.IconSize);
        Assert.False(reconciled.Dock.HideWindowsTaskbar);
    }

    [Fact]
    public void SimulatedExternalToggle_PreventsStaleOverwrite()
    {
        var store = NewStore();
        store.Save(new AppSettings { Notch = new NotchSettings { Enabled = true }, Dock = new DockSettings { Enabled = true }, Appearance = new AppearanceSettings { Theme = AppTheme.Dark } });

        // 1. Bellek kopyası başlangıç ayarlarını yükler
        var inMemoryCopy = store.Load();
        Assert.True(inMemoryCopy.Dock.Enabled);

        // 2. Harici işlem (Sağ alt menü veya kısayol) dock'u kapatıp diske yazar
        var currentDisk = store.Load();
        store.Save(currentDisk with { Dock = currentDisk.Dock with { Enabled = false } });

        // 3. Kullanıcı arayüzde Theme ayarını "Light" olarak değiştirir
        inMemoryCopy = inMemoryCopy with { Appearance = inMemoryCopy.Appearance with { Theme = AppTheme.Light } };

        // 4. Kapanışta diski ezmeden önce disk durumuyla uzlaştırılır
        var diskStateOnClose = store.Load();
        var reconciledToSave = FeatureStateSyncPolicy.Reconcile(
            inMemoryCopy,
            notchEnabled: diskStateOnClose.Notch.Enabled,
            dockEnabled: diskStateOnClose.Dock.Enabled);

        store.Save(reconciledToSave);

        // 5. Sonuç: Tema "Light" olur, ancak DockEnabled false kalır (ezilmez)
        var finalDisk = store.Load();
        Assert.Equal(AppTheme.Light, finalDisk.Appearance.Theme);
        Assert.False(finalDisk.Dock.Enabled);
        Assert.True(finalDisk.Notch.Enabled);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            try
            {
                Directory.Delete(_dir, recursive: true);
            }
            catch
            {
                // Test geçici klasör temizleme hatası göz ardı edilir
            }
        }
    }
}
