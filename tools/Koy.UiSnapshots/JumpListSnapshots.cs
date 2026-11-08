using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Koy.App.Features.Dock;
using Koy.App.Resources;
using Koy.Core.JumpList;

namespace Koy.UiSnapshots;

/// <summary>
/// Dock sağ tık menüsünün bölümlü hâli (atlama listesi). Gerçek <see cref="DockJumpListMenu"/> kodu ve uygulamanın kendi
/// menü stilleri kullanılır; veri tamamen sentetiktir (VS Code ve Steam'e benzeyen ama gerçek kullanıcı verisi olmayan
/// adlar). Platform servisi çağrılmaz.
/// </summary>
internal static class JumpListSnapshots
{
    private static JumpListEntry Gorev(string title) => new(title, new JumpListLaunch(@"C:\Demo\uygulama.exe", "--" + title.Length));
    private static JumpListEntry Doc(string title) => new(title, new JumpListLaunch(@"C:\Demo\" + title, IsDocument: true));
    private static JumpListSection Section(JumpListSectionKind kind, string? name, params JumpListEntry[] entries) => new(kind, name, entries);

    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        // Türkçe metinlerle çiz (bölüm başlıkları ve Koy'un kendi öğeleri).
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo("tr");
        try
        {
            RenderScenes(outDir, prefix, theme, renderElement);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static void RenderScenes(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        // 1. VS Code benzeri: uygulamanın kendi kategorisi, son kullanılanlar, görevler, sonra Koy'un öğeleri.
        var vsCode = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Tasks, null, Gorev("Yeni Pencere"), Gorev("Yeni Sekme")),
            Section(JumpListSectionKind.Custom, "Son klasörler", Doc("demo-api"), Doc("site-taslak"), Doc("notlar"), Doc("oyun-denemesi")),
            Section(JumpListSectionKind.Recent, null, Doc("Program.cs"), Doc("ayarlar.json"), Doc("README.md")),
        ]);
        Shoot(vsCode, outDir, prefix, theme, "dock-atlama-vscode", renderElement);

        // 2. Steam benzeri: son oynananlar, ayırıcılı görevler.
        var steam = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Tasks, null,
                Gorev("Mağaza"), Gorev("Kütüphane"), Gorev("Topluluk"), JumpListEntry.Separator, Gorev("Büyük Resim Modu"), Gorev("Ayarlar")),
            Section(JumpListSectionKind.Recent, null, Doc("Örnek Oyun A"), Doc("Örnek Oyun B"), Doc("Örnek Oyun C")),
        ]);
        Shoot(steam, outDir, prefix, theme, "dock-atlama-steam", renderElement);

        // 3. Sınırlar: çok sayıda girdi menüyü ekran dışına taşırmaz (bölüm başına ve toplam üst sınır).
        var flood = JumpListLayout.Arrange([
            Section(JumpListSectionKind.Custom, "Kategori A", Many("A", 20)),
            Section(JumpListSectionKind.Custom, "Kategori B", Many("B", 20)),
            Section(JumpListSectionKind.Custom, "Kategori C", Many("C", 20)),
            Section(JumpListSectionKind.Frequent, null, Many("Sık", 20)),
            Section(JumpListSectionKind.Recent, null, Many("Son", 20)),
            Section(JumpListSectionKind.Tasks, null, Many("Görev", 20)),
        ]);
        Shoot(flood, outDir, prefix, theme, "dock-atlama-sinir", renderElement);

        // 4. Kimliği olmayan klasik uygulama: yalnız Koy'un kendi öğeleri.
        Shoot([], outDir, prefix, theme, "dock-atlama-yalniz-koy", renderElement);
    }

    private static JumpListEntry[] Many(string prefix, int count) =>
        Enumerable.Range(1, count).Select(i => Doc($"{prefix} {i}")).ToArray();

    private static void Shoot(
        IReadOnlyList<JumpListSection> sections, string outDir, string prefix, string theme, string name,
        Action<FrameworkElement, double, double, string> renderElement)
    {
        var menu = new ContextMenu();

        // Koy'un kendi öğeleri (DockItemsStrip.xaml'daki menüyle aynı).
        menu.Items.Add(new MenuItem { Header = Strings.LaunchNewInstance });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = Strings.Close });
        menu.Items.Add(new MenuItem { Header = Strings.EndTask });

        DockJumpListMenu.Populate(menu, sections, _ => { });

        // Açılır pencere olmadan, menüyü doğrudan görsel olarak çiz (ContextMenu başka bir öğenin çocuğu olamaz).
        renderElement(menu, 300, 0, Path.Combine(outDir, $"{prefix}-{theme}-{name}.png"));
    }
}
