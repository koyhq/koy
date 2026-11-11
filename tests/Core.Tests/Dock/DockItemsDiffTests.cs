using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

/// <summary>
/// Dock'un kendi yazdığı veriler (sıra, klasörler) ayrı bölümde. Sürükleyerek sıralama dock'u yeniden kurmamalı;
/// yoksa her sürüklemede pencereler sıçrar.
/// </summary>
public class DockItemsDiffTests
{
    [Fact]
    public void Reordering_pinned_apps_needs_nothing()
    {
        var before = new AppSettings { DockItems = new DockItemsSettings { PinnedOrder = ["a.lnk", "b.lnk"] } };
        var after = new AppSettings { DockItems = new DockItemsSettings { PinnedOrder = ["b.lnk", "a.lnk"] } };

        Assert.True(DockSettingsDiff.Compare(before, after).IsEmpty);
    }

    [Fact]
    public void Changing_folders_needs_nothing()
    {
        var before = new AppSettings();
        var after = new AppSettings
        {
            DockItems = new DockItemsSettings { Folders = [new DockFolder("f1", "Araçlar", ["calc.lnk"])] }
        };

        Assert.True(DockSettingsDiff.Compare(before, after).IsEmpty);
    }
}
