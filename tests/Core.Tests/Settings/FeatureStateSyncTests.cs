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
        var initial = new AppSettings { NotchEnabled = true, DockEnabled = true };

        var dockDisabled = FeatureStateSyncPolicy.Reconcile(initial, dockEnabled: false);
        Assert.True(dockDisabled.NotchEnabled);
        Assert.False(dockDisabled.DockEnabled);

        var notchDisabled = FeatureStateSyncPolicy.Reconcile(initial, notchEnabled: false);
        Assert.False(notchDisabled.NotchEnabled);
        Assert.True(notchDisabled.DockEnabled);

        var bothChanged = FeatureStateSyncPolicy.Reconcile(initial, notchEnabled: false, dockEnabled: false);
        Assert.False(bothChanged.NotchEnabled);
        Assert.False(bothChanged.DockEnabled);

        var unchanged = FeatureStateSyncPolicy.Reconcile(initial);
        Assert.Equal(initial, unchanged);
    }

    [Fact]
    public void Reconcile_PreservesAllOtherSettings()
    {
        var complex = new AppSettings
        {
            Language = "en",
            Theme = AppTheme.Light,
            CornerRadius = 18.0,
            DockIconSize = 36.0,
            HideWindowsTaskbar = false,
            NotchEnabled = true,
            DockEnabled = true
        };

        var reconciled = FeatureStateSyncPolicy.Reconcile(complex, dockEnabled: false);

        Assert.False(reconciled.DockEnabled);
        Assert.True(reconciled.NotchEnabled);
        Assert.Equal("en", reconciled.Language);
        Assert.Equal(AppTheme.Light, reconciled.Theme);
        Assert.Equal(18.0, reconciled.CornerRadius);
        Assert.Equal(36.0, reconciled.DockIconSize);
        Assert.False(reconciled.HideWindowsTaskbar);
    }

    [Fact]
    public void SimulatedExternalToggle_PreventsStaleOverwrite()
    {
        var store = NewStore();
        store.Save(new AppSettings { NotchEnabled = true, DockEnabled = true, Theme = AppTheme.Dark });

        // 1. Bellek kopyası başlangıç ayarlarını yükler
        var inMemoryCopy = store.Load();
        Assert.True(inMemoryCopy.DockEnabled);

        // 2. Harici işlem (Sağ alt menü veya kısayol) dock'u kapatıp diske yazar
        var currentDisk = store.Load();
        store.Save(currentDisk with { DockEnabled = false });

        // 3. Kullanıcı arayüzde Theme ayarını "Light" olarak değiştirir
        inMemoryCopy = inMemoryCopy with { Theme = AppTheme.Light };

        // 4. Kapanışta diski ezmeden önce disk durumuyla uzlaştırılır
        var diskStateOnClose = store.Load();
        var reconciledToSave = FeatureStateSyncPolicy.Reconcile(
            inMemoryCopy,
            notchEnabled: diskStateOnClose.NotchEnabled,
            dockEnabled: diskStateOnClose.DockEnabled);

        store.Save(reconciledToSave);

        // 5. Sonuç: Tema "Light" olur, ancak DockEnabled false kalır (ezilmez)
        var finalDisk = store.Load();
        Assert.Equal(AppTheme.Light, finalDisk.Theme);
        Assert.False(finalDisk.DockEnabled);
        Assert.True(finalDisk.NotchEnabled);
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
