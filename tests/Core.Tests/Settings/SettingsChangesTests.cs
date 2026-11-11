using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class SettingsChangesTests
{
    [Fact]
    public void Varsayilan_ayarlar_hic_degisiklik_icermez()
    {
        Assert.Empty(SettingsChanges.ChangedKeys(new AppSettings(), new AppSettings()));
    }

    [Fact]
    public void Tek_skaler_degisiklik_bolum_ve_alan_adiyla_bulunur()
    {
        var defaults = new AppSettings();
        var current = new AppSettings { Dock = new DockSettings { Enabled = !defaults.Dock.Enabled } };

        var changed = SettingsChanges.ChangedKeys(current, defaults);

        Assert.Equal(["Dock.Enabled"], changed);
    }

    [Fact]
    public void Liste_degisikligi_de_algilanir()
    {
        var defaults = new AppSettings();
        var current = new AppSettings();
        current.DockItems.PinnedOrder.Add("explorer");

        var changed = SettingsChanges.ChangedKeys(current, defaults);

        Assert.Equal(["DockItems.PinnedOrder"], changed);
    }

    [Fact]
    public void Surum_alani_degisiklik_sayilmaz()
    {
        var defaults = new AppSettings();
        var current = new AppSettings { Version = AppSettings.CurrentVersion - 1 };

        Assert.Empty(SettingsChanges.ChangedKeys(current, defaults));
    }

    [Fact]
    public void Birden_cok_degisiklik_bildirim_sirasinda_doner()
    {
        var defaults = new AppSettings();
        var current = new AppSettings
        {
            Dock = new DockSettings { Enabled = !defaults.Dock.Enabled, HideWindowsTaskbar = !defaults.Dock.HideWindowsTaskbar },
        };

        var changed = SettingsChanges.ChangedKeys(current, defaults);

        Assert.Equal(2, changed.Count);
        Assert.Contains("Dock.Enabled", changed);
        Assert.Contains("Dock.HideWindowsTaskbar", changed);
    }

    [Fact]
    public void Sayfa_vurgusu_yalniz_o_bolumun_anahtarlarina_bakar()
    {
        var keys = new[] { "Dock.Enabled", "Notch.Enabled" };

        Assert.True(SettingsChanges.HasChanges(keys, "Dock"));
        Assert.True(SettingsChanges.HasChanges(keys, "Notch"));
        Assert.False(SettingsChanges.HasChanges(keys, "Dockx"));
        Assert.False(SettingsChanges.HasChanges(keys, "Music"));
    }

    [Fact]
    public void Null_ayar_reddedilir()
    {
        Assert.Throws<ArgumentNullException>(() => SettingsChanges.ChangedKeys(null!, new AppSettings()));
        Assert.Throws<ArgumentNullException>(() => SettingsChanges.ChangedKeys(new AppSettings(), null!));
    }
}
