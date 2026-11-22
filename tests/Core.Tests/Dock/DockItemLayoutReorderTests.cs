using Koy.Core.Dock;
using Koy.Core.Dock.Items;

namespace Koy.Core.Tests;

/// <summary>Sürükleme ve klasörleme sonrası düzen ile PinnedOrder'ın tutarlılığı (ek öğeler varken).</summary>
public class DockItemLayoutReorderTests
{
    private static DockItemSpec App(string id) => new(DockItemKind.App, id, "");
    private static DockItemSpec Folder(string id) => new(DockItemKind.Folder, id, "");
    private static DockItemSpec Sep() => new(DockItemKind.Separator, "", "");
    private static DockItemSpec Clock() => new(DockItemKind.Clock, "Clock", "");

    private static string Describe(IEnumerable<DockItemSpec> items) =>
        string.Join(",", items.Select(i => i.Kind == DockItemKind.App ? i.Id : i.Kind == DockItemKind.Folder ? "F" + i.Id : i.Kind.ToString()));

    [Fact]
    public void Reorder_gorunen_ogeler_yuvalarini_yeni_sirayla_doldurur()
    {
        // a, saat, b, c: a sürüklenip c'nin yerine bırakıldı -> şerit sırası saat, b, c, a.
        var layout = new[] { App("a"), Clock(), App("b"), App("c") };
        var next = DockItemLayout.Reorder(layout, [1, 2, 3, 0]);
        Assert.Equal("Clock,b,c,a", Describe(next));
        Assert.Equal(["b", "c", "a"], DockItemLayout.ToTokens(next));
    }

    [Fact]
    public void Reorder_uygulama_ek_ogenin_oteki_yanina_gecebilir()
    {
        var layout = new[] { App("a"), App("b"), Clock(), Sep(), App("c") };
        // c, a'nın önüne.
        var next = DockItemLayout.Reorder(layout, [4, 0, 1, 2, 3]);
        Assert.Equal("c,a,b,Clock,Separator", Describe(next));
    }

    [Fact]
    public void Reorder_gorunmeyen_pil_yerinde_kalir()
    {
        var battery = new DockItemSpec(DockItemKind.Battery, "Battery", "");
        var layout = new[] { App("a"), battery, App("b"), Clock() };
        // Pil şeritte yok: görünenler 0, 2, 3; a ve b yer değiştirdi.
        var next = DockItemLayout.Reorder(layout, [2, 0, 3]);
        Assert.Equal("b,Battery,a,Clock", Describe(next));
    }

    [Fact]
    public void Reorder_gecersiz_indekste_degismez()
    {
        var layout = new[] { App("a"), App("b") };
        Assert.Same(layout, DockItemLayout.Reorder(layout, [0, 5]));
        Assert.Same(layout, DockItemLayout.Reorder(layout, [1, 1]));
    }

    [Fact]
    public void Reorder_iki_kayit_tutarli_PinnedOrder_ve_Items()
    {
        var layout = new[] { App("a"), Clock(), App("b"), Folder("f1"), App("c") };
        var next = DockItemLayout.Reorder(layout, [0, 1, 3, 2, 4]);   // klasör b'nin önüne
        Assert.Equal(["a", "folder:f1", "b", "c"], DockItemLayout.ToTokens(next));
        var stored = DockItemLayout.ToStoredEntries(next);
        // Kayıtlı Items çözülünce aynı sıra ve ek öğe konumu çıkar.
        var resolved = DockItemRules.Resolve(stored, DockItemLayout.ToTokens(next));
        Assert.Equal(Describe(next), Describe(resolved));
    }

    [Fact]
    public void Rebase_klasor_olusunca_ek_oge_klasorun_yaninda_kalir()
    {
        // a, b, saat, c: a b'nin üstüne bırakıldı -> klasör (a, b), saat, c.
        var layout = new[] { App("a"), App("b"), Clock(), App("c") };
        var folder = new DockFolder("f1", "Klasör", ["b", "a"]);
        var next = DockItemLayout.Rebase(layout, ["folder:f1", "c"], [], [folder]);
        Assert.Equal("Ff1,Clock,c", Describe(next));
    }

    [Fact]
    public void Rebase_ek_oge_basta_ve_ortada_sayisini_korur()
    {
        var layout = new[] { Sep(), App("a"), Clock(), App("b"), App("c"), Sep() };
        var folder = new DockFolder("f1", "K", ["b", "c"]);
        var next = DockItemLayout.Rebase(layout, ["a", "folder:f1"], [], [folder]);
        Assert.Equal("Separator,a,Clock,Ff1,Separator", Describe(next));
    }

    [Fact]
    public void Rebase_klasore_ekleme_ve_dagitma()
    {
        var f = new DockFolder("f1", "K", ["b", "c"]);
        var layout = new[] { App("a"), Folder("f1"), Clock(), App("d") };
        // a klasöre eklendi: klasör (a, b, c) ilk yuvaya, saat klasörden sonra.
        var added = DockItemLayout.Rebase(layout, ["folder:f1", "d"], [f], [f with { Items = ["b", "c", "a"] }]);
        Assert.Equal("Ff1,Clock,d", Describe(added));

        // Klasör dağıtıldı: b, c yerine geçer, saat bunların arkasında kalır.
        var dissolved = DockItemLayout.Rebase(layout, ["a", "b", "c", "d"], [f], []);
        Assert.Equal("a,b,c,Clock,d", Describe(dissolved));
    }
}
