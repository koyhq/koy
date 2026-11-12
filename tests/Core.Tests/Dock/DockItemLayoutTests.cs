using System.Globalization;
using Koy.Core.Dock;
using Koy.Core.Dock.Items;
using Koy.Core.Notch;

namespace Koy.Core.Tests;

public class DockItemLayoutTests
{
    private static DockItemSpec App(string id) => new(DockItemKind.App, id, "");
    private static DockItemSpec Folder(string id) => new(DockItemKind.Folder, id, "");
    private static DockItemSpec Sep() => new(DockItemKind.Separator, "", "");
    private static DockItemSpec Clock() => new(DockItemKind.Clock, "Clock", "");

    private static string Describe(IEnumerable<DockItemSpec> items) =>
        string.Join(",", items.Select(i => i.Kind == DockItemKind.App ? i.Id : i.Kind == DockItemKind.Folder ? "F" + i.Id : i.Kind.ToString()));

    [Fact]
    public void Merge_ek_ogeleri_yerinde_tutar()
    {
        var items = new[] { App("a.lnk"), Sep(), App("b.lnk"), Clock() };
        var merged = DockItemLayout.Merge(items, ["a.lnk", "b.lnk"]);
        Assert.Equal("a.lnk,Separator,b.lnk,Clock", Describe(merged));
    }

    [Fact]
    public void Merge_artik_olmayan_uygulamayi_dusurur()
    {
        var items = new[] { App("a.lnk"), Sep(), App("b.lnk") };
        var merged = DockItemLayout.Merge(items, ["b.lnk"]);
        Assert.Equal("Separator,b.lnk", Describe(merged));
    }

    [Fact]
    public void Merge_yeni_uygulamayi_son_uygulamanin_arkasina_ekler()
    {
        var items = new[] { App("a.lnk"), Clock() };
        var merged = DockItemLayout.Merge(items, ["a.lnk", "c.lnk", "folder:x"]);
        Assert.Equal("a.lnk,c.lnk,Fx,Clock", Describe(merged));
    }

    [Fact]
    public void Merge_uygulama_yoksa_yenileri_basa_ekler()
    {
        var merged = DockItemLayout.Merge([Clock()], ["a.lnk"]);
        Assert.Equal("a.lnk,Clock", Describe(merged));
    }

    [Fact]
    public void Merge_buyuk_kucuk_harf_duyarsiz_ve_tekrar_yok()
    {
        var items = new[] { App("A.lnk"), App("a.LNK") };
        var merged = DockItemLayout.Merge(items, ["a.lnk"]);
        Assert.Single(merged);
    }

    [Fact]
    public void Merge_klasoru_belirecinden_eslestirir()
    {
        var merged = DockItemLayout.Merge([Folder("x"), Sep()], ["folder:x"]);
        Assert.Equal("Fx,Separator", Describe(merged));
    }

    [Fact]
    public void Add_sona_ekler_ve_tekil_ogeyi_ikinci_kez_eklemez()
    {
        var list = DockItemLayout.Add([App("a.lnk")], Clock());
        Assert.Equal("a.lnk,Clock", Describe(list));

        var again = DockItemLayout.Add(list, Clock());
        Assert.Equal("a.lnk,Clock", Describe(again));
    }

    [Fact]
    public void Add_ayiriciyi_tekrarlar()
    {
        var list = DockItemLayout.Add(DockItemLayout.Add([], Sep()), Sep());
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void Add_siniri_asmaz()
    {
        var full = Enumerable.Range(0, DockItemRules.MaxItemCount).Select(i => App($"{i}.lnk")).ToList();
        var result = DockItemLayout.Add(full, Sep());
        Assert.Equal(DockItemRules.MaxItemCount, result.Count);
    }

    [Fact]
    public void Add_bos_komutlu_ozel_dugmeyi_reddeder()
    {
        var result = DockItemLayout.Add([App("a.lnk")], new DockItemSpec(DockItemKind.Custom, "abc", ""));
        Assert.Single(result);
    }

    [Fact]
    public void RemoveAt_gecersiz_indekste_degistirmez()
    {
        var items = new[] { App("a.lnk"), Sep() };
        Assert.Equal(2, DockItemLayout.RemoveAt(items, 5).Count);
        Assert.Equal("a.lnk", Describe(DockItemLayout.RemoveAt(items, 1)));
    }

    [Fact]
    public void ToStoredEntries_ek_ogesiz_listede_bos_doner()
    {
        Assert.Empty(DockItemLayout.ToStoredEntries([App("a.lnk")]));
        var stored = DockItemLayout.ToStoredEntries([App("a.lnk"), Clock()]);
        Assert.Equal(2, stored.Count);
        Assert.Equal("Clock", stored[1].Kind);
    }

    [Fact]
    public void ToStoredEntries_Normalize_ile_gidis_donus_ayni_kalir()
    {
        var items = new[] { App("a.lnk"), Sep(), Folder("x"), new DockItemSpec(DockItemKind.Custom, "c1", @"C:\t.exe"), Clock() };
        var round = DockItemRules.Normalize(DockItemLayout.ToStoredEntries(items));
        Assert.Equal(items, round);
    }

    [Fact]
    public void NewCustomId_bos_degil_ve_benzersiz()
    {
        var a = DockItemLayout.NewCustomId();
        Assert.NotEmpty(a);
        Assert.NotEqual(a, DockItemLayout.NewCustomId());
    }

    [Fact]
    public void Move_ek_ogeyi_uygulamanin_otesine_tasir()
    {
        var items = new[] { App("a.lnk"), App("b.lnk"), Clock() };
        Assert.Equal("a.lnk,Clock,b.lnk", Describe(DockItemRules.Move(items, 2, 1)));
    }

    [Fact]
    public void DelayToNextMinute_dakika_basina_hizalar()
    {
        var now = new DateTime(2026, 11, 12, 10, 15, 20, 500);
        var delay = DockClockTick.DelayToNextMinute(now);
        Assert.Equal(TimeSpan.FromMilliseconds(39500) + ClockTick.Margin, delay);
        Assert.Equal(0, (now + delay).Second);
    }

    [Fact]
    public void DelayToNextMinute_hep_pozitif_ve_en_cok_bir_dakika_ve_pay()
    {
        foreach (var s in new[] { 0, 1, 30, 59 })
        {
            var d = DockClockTick.DelayToNextMinute(new DateTime(2026, 1, 1, 0, 0, s, 999));
            Assert.InRange(d, ClockTick.MinimumDelay, TimeSpan.FromMinutes(1) + ClockTick.Margin);
        }
    }

    [Fact]
    public void Saat_bicimi_kulturu_kullanir()
    {
        var now = new DateTime(2026, 11, 12, 14, 5, 0);
        Assert.Equal("14:05", DockClockTick.FormatTime(now, new CultureInfo("tr-TR")));
        Assert.Contains("12", DockClockTick.FormatDate(now, new CultureInfo("en-US")));
    }
}

public class DockCustomCommandTests
{
    [Theory]
    [InlineData(@"C:\Tools\a.exe", @"C:\Tools\a.exe", null)]
    [InlineData(@"C:\Program Files\App\a.exe", @"C:\Program Files\App\a.exe", null)]
    [InlineData("\"C:\\Program Files\\a.exe\" --flag \"x y\"", @"C:\Program Files\a.exe", "--flag \"x y\"")]
    [InlineData(@"\\server\share\run.cmd", @"\\server\share\run.cmd", null)]
    [InlineData("https://example.com/a?b=1", "https://example.com/a?b=1", null)]
    [InlineData("mailto:a@b.com", "mailto:a@b.com", null)]
    public void TryParse_gecerli_komutlari_ayirir(string text, string target, string? args)
    {
        Assert.True(DockCustomCommand.TryParse(text, out var launch));
        Assert.Equal(target, launch.Target);
        Assert.Equal(args, launch.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notepad.exe")]
    [InlineData(@"..\a.exe")]
    [InlineData("shell:RecycleBinFolder")]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/a.exe")]
    [InlineData(@"\\?\C:\a.exe")]
    [InlineData(@"\\.\pipe\x")]
    [InlineData("C:\\a.exe\r\ncalc")]
    [InlineData("C:\\a.exe\0")]
    [InlineData("\"C:\\a.exe")]
    [InlineData("\"\" x")]
    [InlineData("C:\\a|b.exe")]
    [InlineData("\"https://example.com\" --x")]
    [InlineData("https://exa mple.com")]
    public void TryParse_gecersiz_ya_da_riskli_komutlari_reddeder(string? text)
    {
        Assert.False(DockCustomCommand.TryParse(text, out _));
    }

    [Fact]
    public void TryParse_cok_uzun_komutu_reddeder()
    {
        var text = @"C:\" + new string('a', DockItemRules.MaxCommandLength);
        Assert.False(DockCustomCommand.TryParse(text, out _));
    }
}
