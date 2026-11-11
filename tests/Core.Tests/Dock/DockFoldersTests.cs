using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockFoldersTests
{
    [Fact]
    public void CreateFromDrop_HedefinKonumundaYeniKlasorOlusturur()
    {
        var order = new[] { "Code.lnk", "Obsidian.lnk", "Spotify.lnk" };
        var folders = Array.Empty<DockFolder>();

        var (newOrder, newFolders) = DockFolders.CreateFromDrop(
            order, folders, dragged: "Spotify.lnk", target: "Obsidian.lnk", defaultName: "Klasör");

        Assert.Single(newFolders);
        var folder = newFolders[0];
        Assert.Equal("Klasör", folder.Name);
        Assert.Equal(["Obsidian.lnk", "Spotify.lnk"], folder.Items);

        var token = PinnedOrder.ToFolderToken(folder.Id);
        Assert.Equal(["Code.lnk", token], newOrder);
    }

    [Fact]
    public void CreateFromDrop_AyniOgeyeSuruklenirseIslemYapilmaz()
    {
        var order = new[] { "Code.lnk", "Obsidian.lnk" };
        var folders = Array.Empty<DockFolder>();

        var (newOrder, newFolders) = DockFolders.CreateFromDrop(
            order, folders, dragged: "Code.lnk", target: "Code.lnk", defaultName: "Klasör");

        Assert.Equal(order, newOrder);
        Assert.Empty(newFolders);
    }

    [Fact]
    public void CreateFromDrop_KlasorSuruklenirseIcIceKlasoreIzinVerilmez()
    {
        var order = new[] { "folder:f1", "Obsidian.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk"]) };

        var (newOrder, newFolders) = DockFolders.CreateFromDrop(
            order, folders, dragged: "folder:f1", target: "Obsidian.lnk", defaultName: "Klasör");

        Assert.Equal(order, newOrder);
        Assert.Equal(folders, newFolders);
    }

    [Fact]
    public void CreateFromDrop_HedefKlasorseAddToFolderaYonlendirir()
    {
        var order = new[] { "folder:f1", "Spotify.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk"]) };

        var (newOrder, newFolders) = DockFolders.CreateFromDrop(
            order, folders, dragged: "Spotify.lnk", target: "folder:f1", defaultName: "Klasör");

        Assert.Single(newFolders);
        Assert.Equal(["A.lnk", "B.lnk", "Spotify.lnk"], newFolders[0].Items);
        Assert.Equal(["folder:f1"], newOrder);
    }

    [Fact]
    public void AddToFolder_UygulamayiEklerVeKökSiradanCikarir()
    {
        var order = new[] { "Code.lnk", "folder:f1", "Slack.lnk" };
        var folders = new[] { new DockFolder("f1", "İletişim", ["Teams.lnk", "Discord.lnk"]) };

        var (newOrder, newFolders) = DockFolders.AddToFolder(order, folders, "Slack.lnk", "f1");

        Assert.Equal(["Code.lnk", "folder:f1"], newOrder);
        Assert.Equal(["Teams.lnk", "Discord.lnk", "Slack.lnk"], newFolders[0].Items);
    }

    [Fact]
    public void AddToFolder_ZatenVarsaCiftEklenmez()
    {
        var order = new[] { "folder:f1" };
        var folders = new[] { new DockFolder("f1", "İletişim", ["Teams.lnk", "Discord.lnk"]) };

        var (newOrder, newFolders) = DockFolders.AddToFolder(order, folders, "Teams.lnk", "f1");

        Assert.Equal(["Teams.lnk", "Discord.lnk"], newFolders[0].Items);
    }

    [Fact]
    public void RemoveFromFolder_IkiVeyaDahaFazlaKalirsaKlasorKorunur()
    {
        var order = new[] { "folder:f1", "Terminal.lnk" };
        var folders = new[] { new DockFolder("f1", "Geliştirme", ["VS.lnk", "Code.lnk", "Rider.lnk"]) };

        // "Code.lnk" çıkarılıp 0. indekse yerleştiriliyor
        var (newOrder, newFolders) = DockFolders.RemoveFromFolder(order, folders, "f1", "Code.lnk", insertIndex: 0);

        Assert.Single(newFolders);
        Assert.Equal(["VS.lnk", "Rider.lnk"], newFolders[0].Items);
        Assert.Equal(["Code.lnk", "folder:f1", "Terminal.lnk"], newOrder);
    }

    [Fact]
    public void RemoveFromFolder_TekOgeKaldigindaKlasorKendiligindenDagilir()
    {
        var order = new[] { "Terminal.lnk", "folder:f1", "Browser.lnk" };
        var folders = new[] { new DockFolder("f1", "Çift", ["A.lnk", "B.lnk"]) };

        // "B.lnk" çıkarılıp en sona (indeks 99) yerleştiriliyor
        var (newOrder, newFolders) = DockFolders.RemoveFromFolder(order, folders, "f1", "B.lnk", insertIndex: 99);

        // Klasör dağıldı: folders boş kalmalı
        Assert.Empty(newFolders);
        // Kalan tek öğe "A.lnk" klasörün yerine geçer, çıkarılan "B.lnk" sona eklenir
        Assert.Equal(["Terminal.lnk", "A.lnk", "Browser.lnk", "B.lnk"], newOrder);
    }

    [Fact]
    public void RemoveFromFolder_SifirOgeKalirsaKlasorSilinir()
    {
        var order = new[] { "folder:f1" };
        var folders = new[] { new DockFolder("f1", "Tekil", ["A.lnk"]) };

        var (newOrder, newFolders) = DockFolders.RemoveFromFolder(order, folders, "f1", "A.lnk", insertIndex: 0);

        Assert.Empty(newFolders);
        Assert.Equal(["A.lnk"], newOrder);
    }

    [Fact]
    public void Dissolve_KlasorBelirteciniOgeleriyleDegistirir()
    {
        var order = new[] { "Code.lnk", "folder:f1", "Spotify.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["Notepad.lnk", "Calc.lnk", "Paint.lnk"]) };

        var (newOrder, newFolders) = DockFolders.Dissolve(order, folders, "f1");

        Assert.Empty(newFolders);
        Assert.Equal(["Code.lnk", "Notepad.lnk", "Calc.lnk", "Paint.lnk", "Spotify.lnk"], newOrder);
    }

    [Fact]
    public void Dissolve_OlmayanKlasordeDegisiklikYapmaz()
    {
        var order = new[] { "Code.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk"]) };

        var (newOrder, newFolders) = DockFolders.Dissolve(order, folders, "olmayan");

        Assert.Equal(order, newOrder);
        Assert.Equal(folders, newFolders);
    }

    [Fact]
    public void Rename_GecerliYeniAdiKaydeder()
    {
        var folders = new[] { new DockFolder("f1", "Eski Ad", ["A.lnk", "B.lnk"]) };

        var result = DockFolders.Rename(folders, "f1", "Yeni Ad");

        Assert.Equal("Yeni Ad", result[0].Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rename_BosVeyaGecersizMetindeEskiAdiKorur(string? invalidName)
    {
        var folders = new[] { new DockFolder("f1", "Sabit Ad", ["A.lnk", "B.lnk"]) };

        var result = DockFolders.Rename(folders, "f1", invalidName!);

        Assert.Equal("Sabit Ad", result[0].Name);
    }

    [Fact]
    public void ReorderInFolder_IndekseGoreSiralama()
    {
        var folders = new[] { new DockFolder("f1", "Klasör", ["A.lnk", "B.lnk", "C.lnk"]) };

        var result = DockFolders.ReorderInFolder(folders, "f1", oldIndex: 0, newIndex: 2);

        Assert.Equal(["B.lnk", "C.lnk", "A.lnk"], result[0].Items);
    }

    [Fact]
    public void ReorderInFolder_OgeAdinaGoreSiralama()
    {
        var folders = new[] { new DockFolder("f1", "Klasör", ["A.lnk", "B.lnk", "C.lnk"]) };

        var result = DockFolders.ReorderInFolder(folders, "f1", item: "A.lnk", newIndex: 2);

        Assert.Equal(["B.lnk", "C.lnk", "A.lnk"], result[0].Items);
    }

    [Fact]
    public void NextDefaultName_ArdisikVeEksikDolduranNumaralandirma()
    {
        var empty = Array.Empty<DockFolder>();
        Assert.Equal("Klasör", DockFolders.NextDefaultName(empty, "Klasör"));

        var one = new[] { new DockFolder("1", "Klasör", ["A.lnk", "B.lnk"]) };
        Assert.Equal("Klasör 2", DockFolders.NextDefaultName(one, "Klasör"));

        var two = new[]
        {
            new DockFolder("1", "Klasör", ["A.lnk", "B.lnk"]),
            new DockFolder("2", "Klasör 2", ["C.lnk", "D.lnk"])
        };
        Assert.Equal("Klasör 3", DockFolders.NextDefaultName(two, "Klasör"));

        // "Klasör 2" silindiyse tekrar 2 seçilmeli
        var gap = new[]
        {
            new DockFolder("1", "Klasör", ["A.lnk", "B.lnk"]),
            new DockFolder("3", "Klasör 3", ["C.lnk", "D.lnk"])
        };
        Assert.Equal("Klasör 2", DockFolders.NextDefaultName(gap, "Klasör"));
    }

    [Fact]
    public void Reconcile_SilinmisKisayollariKlasorlerdenAyiklar()
    {
        var order = new[] { "folder:f1", "Code.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["A.lnk", "B.lnk", "Silindi.lnk"]) };
        var available = new[] { "A.lnk", "B.lnk", "Code.lnk" };

        var (newOrder, newFolders) = DockFolders.Reconcile(order, folders, available);

        Assert.Single(newFolders);
        Assert.Equal(["A.lnk", "B.lnk"], newFolders[0].Items);
        Assert.Equal(["folder:f1", "Code.lnk"], newOrder);
    }

    [Fact]
    public void Reconcile_TekOgeSurukleninceKlasorOtomatikDagilir()
    {
        var order = new[] { "folder:f1", "Code.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["A.lnk", "Silindi.lnk"]) };
        var available = new[] { "A.lnk", "Code.lnk" };

        var (newOrder, newFolders) = DockFolders.Reconcile(order, folders, available);

        Assert.Empty(newFolders);
        // Klasör dağıldı; A.lnk klasörün yuvasına yerleşti
        Assert.Equal(["A.lnk", "Code.lnk"], newOrder);
    }

    [Fact]
    public void Reconcile_KlasorIcindekiUygulamalarKökDuzeydeTekrarGorunmez()
    {
        var order = new[] { "A.lnk", "folder:f1", "B.lnk", "Code.lnk" };
        var folders = new[] { new DockFolder("f1", "İletişim", ["Teams.lnk", "Discord.lnk"]) };
        var available = new[] { "A.lnk", "Teams.lnk", "Discord.lnk", "Code.lnk" };

        var (newOrder, newFolders) = DockFolders.Reconcile(order, folders, available);

        Assert.Single(newFolders);
        // Teams.lnk ve Discord.lnk klasörde olduğu için kök dock sırasında olmamalı
        Assert.Equal(["A.lnk", "folder:f1", "Code.lnk"], newOrder);
        Assert.DoesNotContain("Teams.lnk", newOrder);
        Assert.DoesNotContain("Discord.lnk", newOrder);
    }

    [Fact]
    public void DockFolder_DegerTabanliEsitlikVeKlonlama()
    {
        var f1 = new DockFolder("f1", "Test", ["A.lnk", "B.lnk"]);
        var f2 = new DockFolder("f1", "Test", ["A.lnk", "B.lnk"]);
        var f3 = new DockFolder("f1", "Farklı", ["A.lnk", "B.lnk"]);

        Assert.Equal(f1, f2);
        Assert.Equal(f1.GetHashCode(), f2.GetHashCode());
        Assert.NotEqual(f1, f3);

        // 'with' ifadesi Items referansını kopyalamalı
        var copy = f1 with { Name = "Yeni" };
        copy.Items.Add("C.lnk");

        Assert.Equal(2, f1.Items.Count);
        Assert.Equal(3, copy.Items.Count);
    }

    [Fact]
    public void PinnedOrder_Reconcile_KlasorBelirtecleriniKorur()
    {
        var saved = new[] { "folder:f1", "A.lnk", "folder:f2" };
        var currentIds = new[] { "A.lnk", "B.lnk" };

        var result = PinnedOrder.Reconcile(saved, currentIds);

        // folder:f1 ve folder:f2 düşmemeli, B.lnk sona eklenmeli
        Assert.Equal(["folder:f1", "A.lnk", "folder:f2", "B.lnk"], result);
    }

    [Fact]
    public void PinnedOrder_Reconcile_SifirOgeKalanKlasorDocktanSilinir()
    {
        var saved = new[] { "folder:f1", "A.lnk" };
        var folders = new[] { new DockFolder("f1", "Araçlar", ["Silinen1.lnk", "Silinen2.lnk"]) };
        var currentIds = new[] { "A.lnk" };

        var (order, remainingFolders) = PinnedOrder.Reconcile(saved, currentIds, folders);

        Assert.Empty(remainingFolders);
        Assert.Equal(["A.lnk"], order);
        Assert.DoesNotContain("folder:f1", order);
    }

    [Fact]
    public void SettingsStore_DockFoldersSerilestirmeVeGeriyeUyumlulukTesti()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-dockfolder-test-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var store = new Koy.Core.Settings.SettingsStore(Path.Combine(dir, "settings.json"));

            // 1. Serileştirme ve geri yükleme (roundtrip)
            var original = new Koy.Core.Settings.AppSettings { DockItems = new Koy.Core.Settings.DockItemsSettings { Folders = [
                    new DockFolder("f1", "Araçlar", ["calc.lnk", "notepad.lnk"]),
                    new DockFolder("f2", "Medya", ["spotify.lnk", "vlc.lnk"])
                ] } };

            store.Save(original);
            var restored = store.Load();

            Assert.Equal(original, restored);
            Assert.Equal(2, restored.DockItems.Folders.Count);
            Assert.Equal("Araçlar", restored.DockItems.Folders[0].Name);
            Assert.Equal(["calc.lnk", "notepad.lnk"], restored.DockItems.Folders[0].Items);

            // 2. Eski ayar dosyasında DockFolders alanı yoksa boş liste olarak yüklenmeli
            File.WriteAllText(Path.Combine(dir, "settings.json"), """{ "DockEnabled": true }""");
            var legacy = store.Load();
            Assert.NotNull(legacy.DockItems.Folders);
            Assert.Empty(legacy.DockItems.Folders);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
