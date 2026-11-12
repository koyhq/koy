using Koy.Core.Dock;
using Koy.Core.Dock.Items;

namespace Koy.Core.Tests;

public class DockItemRulesTests
{
    private static DockItemEntry E(string kind, string id = "", string command = "") =>
        new() { Kind = kind, Id = id, Command = command };

    [Theory]
    [InlineData("app", DockItemKind.App)]
    [InlineData("Folder", DockItemKind.Folder)]
    [InlineData("  SEPARATOR ", DockItemKind.Separator)]
    [InlineData("spacer", DockItemKind.Spacer)]
    [InlineData("clock", DockItemKind.Clock)]
    [InlineData("trash", DockItemKind.Trash)]
    [InlineData("custom", DockItemKind.Custom)]
    public void TryParseKind_bilinen_turleri_tanir(string text, DockItemKind expected)
    {
        Assert.True(DockItemRules.TryParseKind(text, out var kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("battery")]
    [InlineData("5")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParseKind_bilinmeyen_metni_reddeder(string? text)
    {
        Assert.False(DockItemRules.TryParseKind(text, out _));
    }

    [Fact]
    public void Normalize_bilinmeyen_turu_atar_ve_sirayi_korur()
    {
        var result = DockItemRules.Normalize([
            E("app", "A.lnk"),
            E("battery"),
            E("clock"),
            E("hologram", "x"),
            E("app", "B.lnk"),
        ]);

        Assert.Equal([DockItemKind.App, DockItemKind.Clock, DockItemKind.App], result.Select(r => r.Kind));
        Assert.Equal(["A.lnk", "Clock", "B.lnk"], result.Select(r => r.Id));
    }

    [Fact]
    public void Normalize_bos_kimlikli_uygulama_ve_klasoru_atar()
    {
        var result = DockItemRules.Normalize([E("app", "   "), E("folder", ""), E("app")]);

        Assert.Empty(result);
    }

    [Fact]
    public void Normalize_komutsuz_ya_da_cok_uzun_ozel_dugmeyi_atar()
    {
        var tooLong = new string('x', DockItemRules.MaxCommandLength + 1);

        var result = DockItemRules.Normalize([
            E("custom", "c1", ""),
            E("custom", "", "notepad"),
            E("custom", "c2", tooLong),
            E("custom", "c3", "https://example.com"),
        ]);

        var only = Assert.Single(result);
        Assert.Equal(DockItemKind.Custom, only.Kind);
        Assert.Equal("c3", only.Id);
    }

    [Fact]
    public void Normalize_ozel_komutu_oldugu_gibi_metin_olarak_saklar()
    {
        // Core komutu çalıştırmaz; yalnız baştaki ve sondaki boşluğu temizleyip saklar.
        var result = DockItemRules.Normalize([E("custom", "c1", "  calc.exe  ")]);

        Assert.Equal("calc.exe", Assert.Single(result).Command);
    }

    [Fact]
    public void Normalize_uygulama_kimligini_buyuk_kucuk_harf_duyarsiz_tekillestirir()
    {
        var result = DockItemRules.Normalize([E("app", "Obsidian.lnk"), E("app", "obsidian.LNK"), E("app", "Code.lnk")]);

        Assert.Equal(["Obsidian.lnk", "Code.lnk"], result.Select(r => r.Id));
    }

    [Fact]
    public void Normalize_ayni_kimlik_farkli_turlerde_cakismaz()
    {
        var result = DockItemRules.Normalize([E("app", "x"), E("folder", "x")]);

        Assert.Equal([DockItemKind.App, DockItemKind.Folder], result.Select(r => r.Kind));
    }

    [Fact]
    public void Normalize_saat_ve_cop_kutusunu_en_fazla_bir_kez_tutar()
    {
        var result = DockItemRules.Normalize([E("clock"), E("trash"), E("clock", "baska"), E("trash")]);

        Assert.Equal([DockItemKind.Clock, DockItemKind.Trash], result.Select(r => r.Kind));
        Assert.Equal(["Clock", "Trash"], result.Select(r => r.Id));
    }

    [Fact]
    public void Normalize_ayirici_ve_bosluk_tekrarlanabilir()
    {
        var result = DockItemRules.Normalize([E("separator"), E("spacer"), E("separator"), E("spacer")]);

        Assert.Equal(
            [DockItemKind.Separator, DockItemKind.Spacer, DockItemKind.Separator, DockItemKind.Spacer],
            result.Select(r => r.Kind));
    }

    [Fact]
    public void Normalize_ozel_dugme_kimligini_tekillestirir()
    {
        var result = DockItemRules.Normalize([E("custom", "c1", "a"), E("custom", "C1", "b")]);

        Assert.Equal("a", Assert.Single(result).Command);
    }

    [Fact]
    public void Normalize_sinir_asilinca_ilk_gelenleri_tutar()
    {
        var entries = Enumerable.Range(0, 10).Select(i => E("app", $"app{i}.lnk")).ToList();

        var result = DockItemRules.Normalize(entries, maxCount: 4);

        Assert.Equal(["app0.lnk", "app1.lnk", "app2.lnk", "app3.lnk"], result.Select(r => r.Id));
    }

    [Fact]
    public void Normalize_varsayilan_sinir_en_fazla_MaxItemCount_tutar()
    {
        var entries = Enumerable.Range(0, DockItemRules.MaxItemCount + 5).Select(i => E("spacer")).ToList();

        Assert.Equal(DockItemRules.MaxItemCount, DockItemRules.Normalize(entries).Count);
    }

    [Fact]
    public void Normalize_bos_girdi_ve_null_kaydi_guvenle_atlar()
    {
        Assert.Empty(DockItemRules.Normalize([]));

        var result = DockItemRules.Normalize([null!, E("spacer")]);

        Assert.Equal(DockItemKind.Spacer, Assert.Single(result).Kind);
    }

    [Fact]
    public void Move_ogeyi_ileri_tasir()
    {
        var items = Specs("A", "B", "C", "D");

        var result = DockItemRules.Move(items, fromIndex: 0, toIndex: 2);

        Assert.Equal(["B", "C", "A", "D"], result.Select(r => r.Id));
    }

    [Fact]
    public void Move_ogeyi_geri_tasir()
    {
        var items = Specs("A", "B", "C", "D");

        var result = DockItemRules.Move(items, fromIndex: 3, toIndex: 1);

        Assert.Equal(["A", "D", "B", "C"], result.Select(r => r.Id));
    }

    [Fact]
    public void Move_hedef_sinirin_disindaysa_uca_kenetlenir()
    {
        var items = Specs("A", "B", "C");

        Assert.Equal(["B", "C", "A"], DockItemRules.Move(items, 0, 99).Select(r => r.Id));
        Assert.Equal(["C", "A", "B"], DockItemRules.Move(items, 2, -5).Select(r => r.Id));
    }

    [Fact]
    public void Move_gecersiz_kaynak_ya_da_ayni_konum_listeyi_degistirmez()
    {
        var items = Specs("A", "B");

        Assert.Equal(["A", "B"], DockItemRules.Move(items, 7, 0).Select(r => r.Id));
        Assert.Equal(["A", "B"], DockItemRules.Move(items, 1, 1).Select(r => r.Id));
        Assert.Empty(DockItemRules.Move([], 0, 0));
    }

    [Fact]
    public void Move_girdi_listesini_degistirmez()
    {
        var items = Specs("A", "B", "C");

        DockItemRules.Move(items, 0, 2);

        Assert.Equal(["A", "B", "C"], items.Select(r => r.Id));
    }

    [Fact]
    public void FromPinnedOrder_sirayi_ve_klasorleri_korur()
    {
        var entries = DockItemRules.FromPinnedOrder(["Obsidian.lnk", "folder:f1", "Code.lnk"]);

        var specs = DockItemRules.Normalize(entries);

        Assert.Equal([DockItemKind.App, DockItemKind.Folder, DockItemKind.App], specs.Select(s => s.Kind));
        Assert.Equal(["Obsidian.lnk", "f1", "Code.lnk"], specs.Select(s => s.Id));
    }

    [Fact]
    public void ToPinnedOrder_PinnedOrder_ile_gidis_donus_yapar()
    {
        string[] original = ["Obsidian.lnk", "folder:f1", "Code.lnk"];

        var roundTrip = DockItemRules.ToPinnedOrder(DockItemRules.Normalize(DockItemRules.FromPinnedOrder(original)));

        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public void ToPinnedOrder_uygulama_ve_klasor_disindakileri_yazmaz()
    {
        var items = new List<DockItemSpec>
        {
            new(DockItemKind.App, "A.lnk", ""),
            new(DockItemKind.Separator, "", ""),
            new(DockItemKind.Custom, "c1", "calc"),
            new(DockItemKind.Clock, "Clock", ""),
            new(DockItemKind.Folder, "f1", ""),
        };

        Assert.Equal(["A.lnk", "folder:f1"], DockItemRules.ToPinnedOrder(items));
    }

    [Theory]
    [InlineData("folder:f1")]
    [InlineData("FOLDER:f1")]
    [InlineData("a/b.lnk")]
    public void Normalize_dosya_adi_olamayan_uygulama_kimligini_atar(string id)
    {
        // "folder:f1" uygulaması PinnedOrder'a yazılınca klasöre dönüşürdü.
        Assert.Empty(DockItemRules.Normalize([E("app", id)]));
    }

    [Fact]
    public void Resolve_kayit_yoksa_PinnedOrder_den_turetir_ve_sirayi_korur()
    {
        var result = DockItemRules.Resolve([], ["Obsidian.lnk", "folder:f1", "Code.lnk"]);

        Assert.Equal([DockItemKind.App, DockItemKind.Folder, DockItemKind.App], result.Select(r => r.Kind));
        Assert.Equal(["Obsidian.lnk", "f1", "Code.lnk"], result.Select(r => r.Id));
    }

    [Fact]
    public void Resolve_kayit_varsa_PinnedOrder_i_yok_sayar()
    {
        var result = DockItemRules.Resolve([E("clock")], ["Obsidian.lnk"]);

        Assert.Equal(DockItemKind.Clock, Assert.Single(result).Kind);
    }

    [Fact]
    public void Resolve_kayitlar_var_ama_hepsi_gecersizse_bos_doner()
    {
        Assert.Empty(DockItemRules.Resolve([E("battery")], ["Obsidian.lnk"]));
    }

    [Fact]
    public void Resolve_gecis_de_ust_siniri_uygular()
    {
        var pinned = Enumerable.Range(0, DockItemRules.MaxItemCount + 3).Select(i => $"app{i}.lnk").ToList();

        var result = DockItemRules.Resolve([], pinned);

        Assert.Equal(pinned.Take(DockItemRules.MaxItemCount), result.Select(r => r.Id));
    }

    private static List<DockItemSpec> Specs(params string[] ids) =>
        ids.Select(id => new DockItemSpec(DockItemKind.App, id, "")).ToList();
}
