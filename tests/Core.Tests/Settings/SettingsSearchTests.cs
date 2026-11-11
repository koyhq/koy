using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class SettingsSearchTests
{
    private static SettingSearchEntry Entry(string key, string title, string description = "", params string[] keywords) =>
        new(key, "page", title, description, keywords);

    private static readonly SettingSearchEntry DarkTheme = Entry("Appearance.Theme", "Karanlık tema", "Koy penceresinin rengi", "koyu", "dark");
    private static readonly SettingSearchEntry Dock = Entry("Dock.Enabled", "Dock", "Ekranın alt kenarındaki çubuk");
    private static readonly SettingSearchEntry HideTaskbar = Entry("Dock.HideWindowsTaskbar", "Görev çubuğunu gizle", "Dock açıkken Windows görev çubuğu saklanır", "taskbar");

    [Fact]
    public void Bos_veya_bosluk_sorgu_hic_sonuc_dondurmez()
    {
        Assert.Empty(SettingsSearch.Search([DarkTheme], null));
        Assert.Empty(SettingsSearch.Search([DarkTheme], "   "));
    }

    [Fact]
    public void Turkce_harfler_farksizdir_ve_buyuk_kucuk_harf_farketmez()
    {
        var hits = SettingsSearch.Search([DarkTheme], "KARANLIK");
        Assert.Single(hits);

        Assert.Single(SettingsSearch.Search([DarkTheme], "karanlik"));
        Assert.Single(SettingsSearch.Search([Entry("x", "Işık")], "isik"));
    }

    [Fact]
    public void Her_parca_satirda_gecmelidir()
    {
        // "dock" başlıkta, "gizle" yalnız açıklamada; ikisi de olan satır bulunur.
        var onlyDock = Entry("a", "Dock", "başka bir şey");
        var both = Entry("b", "Dock", "gizle");

        var hits = SettingsSearch.Search([onlyDock, both], "dock gizle");

        var hit = Assert.Single(hits);
        Assert.Equal("b", hit.Entry.Key);
    }

    [Fact]
    public void Baslik_eslesmesi_aciklama_eslesmesinden_yuksek_puan_alir()
    {
        var inTitle = Entry("title", "Gizle", "");
        var inDescription = Entry("desc", "Başka", "gizle");

        var hits = SettingsSearch.Search([inDescription, inTitle], "gizle");

        Assert.Equal(["title", "desc"], hits.Select(h => h.Entry.Key));
        Assert.True(hits[0].Score > hits[1].Score);
    }

    [Fact]
    public void Anahtar_sozcuk_ile_bulunur()
    {
        var hits = SettingsSearch.Search([HideTaskbar], "taskbar");

        var hit = Assert.Single(hits);
        Assert.Equal("Dock.HideWindowsTaskbar", hit.Entry.Key);
    }

    [Fact]
    public void Basligi_sorguyla_baslayan_satir_one_gecer()
    {
        var startsWith = Entry("start", "Dock konumu", "");
        var contains = Entry("contains", "Yeni dock", "");

        var hits = SettingsSearch.Search([contains, startsWith], "dock");

        Assert.Equal("start", hits[0].Entry.Key);
    }

    [Fact]
    public void Eslesmeyen_sorgu_bos_doner()
    {
        Assert.Empty(SettingsSearch.Search([Dock, HideTaskbar], "bulunmayan"));
    }

    [Fact]
    public void Limit_sonuc_sayisini_sinirlar_ve_sira_korunur()
    {
        var entries = new[] { Entry("1", "ayar"), Entry("2", "ayar"), Entry("3", "ayar") };

        var hits = SettingsSearch.Search(entries, "ayar", limit: 2);

        Assert.Equal(["1", "2"], hits.Select(h => h.Entry.Key));
    }

    [Fact]
    public void Negatif_limit_reddedilir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettingsSearch.Search([Dock], "dock", limit: -1));
    }
}
