using Koy.Core.Dock;
using Koy.Core.Dock.Items;

namespace Koy.Core.Tests;

/// <summary>Yeni öğe listesi eski ayar dosyalarını bozmamalı; PinnedOrder aynen okunmalı.</summary>
public class DockItemSettingsTests
{
    [Fact]
    public void Eski_PinnedOrder_verisi_degismeden_yuklenir_ve_Items_bos_kalir()
    {
        const string json = """{ "Version": 4, "DockItems": { "PinnedOrder": ["Obsidian.lnk", "folder:f1"] } }""";

        var loaded = SettingsFile.Load(json);

        Assert.Equal(["Obsidian.lnk", "folder:f1"], loaded.DockItems.PinnedOrder);
        Assert.Empty(loaded.DockItems.Items);
    }

    [Fact]
    public void Items_kaydi_okunur_ve_bilinmeyen_tur_dogrulamada_atilir()
    {
        const string json = """
        {
          "Version": 4,
          "DockItems": {
            "Items": [
              { "Kind": "app", "Id": "Obsidian.lnk" },
              { "Kind": "battery", "Id": "" },
              { "Kind": "custom", "Id": "c1", "Command": "calc.exe" }
            ]
          }
        }
        """;

        var loaded = SettingsFile.Load(json);
        var specs = DockItemRules.Normalize(loaded.DockItems.Items);

        Assert.Equal(3, loaded.DockItems.Items.Count);
        Assert.Equal([DockItemKind.App, DockItemKind.Custom], specs.Select(s => s.Kind));
        Assert.Equal("calc.exe", specs[1].Command);
    }

    [Fact]
    public void Items_bos_ya_da_yoksa_varsayilan_bos_listedir()
    {
        var loaded = SettingsFile.Load("""{ "Version": 4, "DockItems": { "Items": null } }""");

        Assert.NotNull(loaded.DockItems.Items);
        Assert.Empty(loaded.DockItems.Items);
    }

    [Fact]
    public void Varsayilan_ayarlarda_Items_bos_ve_PinnedOrder_degismemistir()
    {
        var settings = new Koy.Core.Settings.AppSettings();

        Assert.Empty(settings.DockItems.Items);
        Assert.Empty(settings.DockItems.PinnedOrder);
    }
}
