using Koy.Core.Display;
using Koy.Core.Settings;
using Koy.Core.Shell;

namespace Koy.Core.Tests;

/// <summary>Karar (2026-11-06): yeni takılan ekranda çentik ve dock varsayılan açık; ayar ekran başına baştan yapılmaz.</summary>
public class NewScreenDefaultTests
{
    private const string IdMain = @"\\?\DISPLAY#GSM5B09#5&1a2b&0&UID4352#{e6f07b5f}";
    private const string IdSide = @"\\?\DISPLAY#DEL4161#5&1a2b&0&UID4353#{e6f07b5f}";
    private const string IdTv = @"\\?\DISPLAY#SAM0F00#5&1a2b&0&UID7001#{e6f07b5f}";

    private static DisplayMonitor Mon(string id, string name, bool primary, int left) => new(
        id, name, new WindowRect(left, 0, left + 1920, 1080), new WindowRect(left, 0, left + 1920, 1040),
        IsPrimary: primary, Handle: left + 1);

    [Fact]
    public void Varsayilan_yeni_ekran_modu_cenik_ve_dock_acik()
    {
        Assert.Equal(NewScreenMode.NotchAndDock, MonitorPolicy.DefaultNewScreenMode);
        Assert.Equal(NewScreenMode.NotchAndDock, new AppSettings().NewScreenDefault);
    }

    [Fact]
    public void Kayitsiz_ekranlar_varsayilan_ayarlarla_hepsi_acik()
    {
        var settings = new AppSettings();

        var result = MonitorPolicy.Resolve(
            settings.Monitors, settings.NewScreenDefault,
            [Mon(IdMain, "A", true, 0), Mon(IdSide, "B", false, 1920)]);

        Assert.All(result, a => Assert.True(a.NotchEnabled && a.DockEnabled));
        Assert.Equal(2, MonitorPolicy.DockMonitors(result).Count);
        Assert.Equal(2, MonitorPolicy.NotchMonitors(result).Count);
    }

    [Fact]
    public void Bir_ekrani_kapatmak_yalniz_o_ekranin_kaydini_yazar_yeni_ekran_yine_acik_gelir()
    {
        var settings = new AppSettings();
        var side = Mon(IdSide, "B", false, 1920);

        // Kullanıcı Ekranlar sayfasında yan ekranın dock'unu kapatır.
        var monitors = MonitorPolicy.Update(settings.Monitors, settings.NewScreenDefault, side, s => s with { DockEnabled = false });
        Assert.Single(monitors);

        // Sonra üçüncü bir ekran (TV) takılır: kaydı yok, varsayılan açık uygulanır; yan ekran kapalı kalır.
        var result = MonitorPolicy.Resolve(monitors, settings.NewScreenDefault,
            [Mon(IdMain, "A", true, 0), side, Mon(IdTv, "TV", false, 3840)]);

        Assert.True(result[0].DockEnabled && !result[0].IsKnown);
        Assert.False(result[1].DockEnabled);
        Assert.True(result[1].NotchEnabled);
        Assert.True(result[2].NotchEnabled && result[2].DockEnabled && !result[2].IsKnown);
    }

    [Fact]
    public void Eski_Primary_ve_All_gocu_kayit_yazmaz_ve_butun_ekranlar_acik_olur()
    {
        foreach (var target in new[] { "Primary", "All", "" })
        {
            var migrated = SettingsFile.Load("{ \"Version\": 2, \"MonitorTarget\": \"" + target + "\" }");

            Assert.Empty(migrated.Monitors);
            Assert.Equal(NewScreenMode.NotchAndDock, migrated.NewScreenDefault);

            var result = MonitorPolicy.Resolve(migrated.Monitors, migrated.NewScreenDefault,
                [Mon(IdMain, "A", true, 0), Mon(IdSide, "B", false, 1920)]);
            Assert.All(result, a => Assert.True(a.NotchEnabled && a.DockEnabled));
        }
    }
}
