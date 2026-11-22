using Koy.Core.Dock;
using Koy.Core.Dock.Items;

namespace Koy.Core.Tests;

/// <summary>
/// Klasör oluşturma uçtan uca (Core zinciri): görünüm modeli sürüklenen ve hedef uygulamanın kimliği olarak tam kısayol yolunu
/// verir (DockItem.Id), sıra ve klasörler ise dosya adı tutar. Ek öğeli (Pil, Saat) düzende de klasör doğru yere gelmeli,
/// üyeler korunmalı ve plan klasörü iki simgeyle üretmeli.
/// </summary>
public class DockFolderCreationTests
{
    private const string Root = @"C:\Users\kullanici\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\";

    private static string Path(string name) => Root + name;

    private static DockItem Model(string name, bool pinned = true) =>
        new(Path(name), System.IO.Path.GetFileNameWithoutExtension(name), Path(name), null, [1, 2, 3], pinned, false, false, []);

    [Fact]
    public void CreateFromDrop_tam_yolla_verilen_kimlikler_dosya_adina_cevrilir()
    {
        var order = new[] { "Code.lnk", "Obsidian.lnk", "Spotify.lnk" };

        var (newOrder, folders) = DockFolders.CreateFromDrop(order, [], Path("Spotify.lnk"), Path("Obsidian.lnk"), "Klasör");

        var folder = Assert.Single(folders);
        Assert.Equal(["Obsidian.lnk", "Spotify.lnk"], folder.Items);
        // Sürüklenen sıradan çıkar, hedefin yerine klasör gelir (sona eklenmez).
        Assert.Equal(["Code.lnk", PinnedOrder.ToFolderToken(folder.Id)], newOrder);
    }

    [Fact]
    public void AddToFolder_ve_RemoveFromFolder_tam_yolla_calisir()
    {
        var folder = new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk"]);

        var (order, folders) = DockFolders.AddToFolder(["folder:f1", "C.lnk"], [folder], Path("C.lnk"), "folder:f1");
        Assert.Equal(["folder:f1"], order);
        Assert.Equal(["A.lnk", "B.lnk", "C.lnk"], folders[0].Items);

        (order, folders) = DockFolders.RemoveFromFolder(order, folders, "f1", Path("C.lnk"), 1);
        Assert.Equal(["folder:f1", "C.lnk"], order);
        Assert.Equal(["A.lnk", "B.lnk"], folders[0].Items);
    }

    [Fact]
    public void Plan_tam_yolla_olusturulan_klasoru_iki_uyeyle_uretir_ve_sirada_ayri_simge_birakmaz()
    {
        var models = new[] { Model("Code.lnk"), Model("Obsidian.lnk"), Model("Spotify.lnk") };
        var order = new[] { "Code.lnk", "Obsidian.lnk", "Spotify.lnk" };

        var (newOrder, folders) = DockFolders.CreateFromDrop(order, [], models[2].Id, models[1].Id, "Klasör");
        var plan = DockEntryPlanner.Plan(models, newOrder, folders);

        Assert.Equal(2, plan.Entries.Count);
        Assert.IsType<PlannedItem>(plan.Entries[0]);
        var planned = Assert.IsType<PlannedFolder>(plan.Entries[1]);
        Assert.Equal(2, planned.Members.Count);
        Assert.All(planned.Members, m => Assert.False(m.IsFallback));
    }

    [Fact]
    public void Gizli_ek_ogelerle_klasor_olusturma_klasoru_hedefin_yerine_koyar_ve_ek_ogeleri_korur()
    {
        // Tam genişlikte sistem alanı Pil ve Saat'i kendisi çizer; kayıtlı düzende ise yine dururlar.
        var models = new[] { Model("A.lnk"), Model("B.lnk"), Model("C.lnk") };
        var layout = new List<DockItemSpec>
        {
            new(DockItemKind.App, "A.lnk", ""), new(DockItemKind.App, "B.lnk", ""), new(DockItemKind.App, "C.lnk", ""),
            new(DockItemKind.Battery, "Battery", ""), new(DockItemKind.Clock, "Clock", ""),
        };
        string[] order = ["A.lnk", "B.lnk", "C.lnk"];

        // B, A'nın üstüne bırakıldı.
        var (newOrder, folders) = DockFolders.CreateFromDrop(order, [], models[1].Id, models[0].Id, "Klasör");
        var rebased = DockItemLayout.Rebase(layout, newOrder, [], folders);
        var stored = DockItemLayout.ToStoredEntries(rebased);

        // Kayıttan okuma yolu: Resolve → Merge(etkin sıra) → plan.
        var specs = DockItemRules.Resolve(stored, newOrder);
        var plan = DockEntryPlanner.Plan(models, newOrder, folders);
        var merged = DockItemLayout.Merge(specs, plan.EffectiveOrder);

        var folderToken = PinnedOrder.ToFolderToken(folders[0].Id);
        Assert.Equal([folderToken, "C.lnk"], plan.EffectiveOrder);
        Assert.Equal(
            [DockItemKind.Folder, DockItemKind.App, DockItemKind.Battery, DockItemKind.Clock],
            merged.Select(i => i.Kind));
        Assert.Equal(2, ((PlannedFolder)plan.Entries[0]).Members.Count);
        Assert.Equal(["A.lnk", "B.lnk"], folders[0].Items);
    }

    [Fact]
    public void Gizli_ek_ogelerle_mevcut_klasore_ekleme_ek_ogeleri_yerinde_tutar()
    {
        var folder = new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk"]);
        var layout = new List<DockItemSpec>
        {
            new(DockItemKind.Folder, "f1", ""), new(DockItemKind.App, "C.lnk", ""), new(DockItemKind.Battery, "Battery", ""),
            new(DockItemKind.Clock, "Clock", ""),
        };

        var (order, folders) = DockFolders.AddToFolder(["folder:f1", "C.lnk"], [folder], Path("C.lnk"), "folder:f1");
        var rebased = DockItemLayout.Rebase(layout, order, [folder], folders);

        Assert.Equal(
            [DockItemKind.Folder, DockItemKind.Battery, DockItemKind.Clock],
            rebased.Select(i => i.Kind));
        Assert.Equal(["A.lnk", "B.lnk", "C.lnk"], folders[0].Items);
    }
}

public class DockFolderCoverTests
{
    [Theory]
    [InlineData(0, new int[] { }, 4)]
    [InlineData(1, new[] { 0 }, 3)]
    [InlineData(2, new[] { 0, 1 }, 2)]
    [InlineData(3, new[] { 0, 1, 2 }, 1)]
    [InlineData(4, new[] { 0, 1, 2, 3 }, 0)]
    [InlineData(5, new[] { 0, 1, 2, 3 }, 0)]
    [InlineData(6, new[] { 0, 1, 2, 3 }, 0)]
    public void Cells_ilk_dort_uygulama_sirayla_gorunur_gerisi_bos_kalir(int memberCount, int[] shown, int emptyCells)
    {
        var cells = DockFolderCover.Cells(memberCount);

        Assert.Equal(DockFolderCover.CellCount, cells.Count);
        Assert.Equal(shown, cells.Where(c => c.HasValue).Select(c => c!.Value));
        Assert.Equal(emptyCells, cells.Count(c => c is null));
        // Boş hücreler hep sonda: dolu hücreler satır öncelikli ilk hücrelerdir.
        Assert.Equal(cells.OrderBy(c => c is null).ToList(), cells.ToList());
    }

    [Fact]
    public void Cells_besinci_ve_sonrasi_kapakta_asla_yer_almaz()
    {
        Assert.DoesNotContain(DockFolderCover.Cells(6), c => c >= 4);
    }
}
