using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Koy.App.Features.Dock;
using Koy.Core.StartMenu;

namespace Koy.UiSnapshots;

/// <summary>
/// Dock'taki Başlat düğmesinin sağ tık menüsü. Gerçek <see cref="DockStartMenu"/> kodu ve uygulamanın kendi menü
/// stilleri kullanılır; hiçbir eylem çalıştırılmaz. Alt menü ayrı bir pencere (Popup) olduğu için görüntüde
/// ana menünün yanına aynı stille elle yerleştirilir.
/// </summary>
internal static class StartMenuSnapshots
{
    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "tr", "en" })
            {
                CultureInfo.CurrentUICulture = new CultureInfo(culture);
                RenderScenes(outDir, prefix, theme, culture, renderElement);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static void RenderScenes(string outDir, string prefix, string theme, string culture, Action<FrameworkElement, double, double, string> renderElement)
    {
        string Path_(string name) => Path.Combine(outDir, $"{prefix}-{theme}-{culture}-{name}.png");

        // 1. Pilli makine (dizüstü): tam menü, Mobilite Merkezi dahil.
        renderElement(DockStartMenu.Build(StartMenuModel.Build(hasBattery: true), _ => { }), 260, 0, Path_("baslat-menu"));

        // 2. Pilsiz makine (masaüstü): Mobilite Merkezi yok.
        renderElement(DockStartMenu.Build(StartMenuModel.Build(hasBattery: false), _ => { }), 260, 0, Path_("baslat-menu-pilsiz"));

        // 3. Alt menü açık: "Kapat veya oturumu kapat" satırının yanında dört oturum komutu.
        renderElement(BuildWithSubmenu(), 560, 0, Path_("baslat-menu-altmenu"));
    }

    private static FrameworkElement BuildWithSubmenu()
    {
        var groups = StartMenuModel.Build(hasBattery: true);
        var parent = groups.SelectMany(g => g.Items).Single(i => i.Kind == StartMenuActionKind.Submenu);

        var mainMenu = DockStartMenu.Build(groups, _ => { });
        var subMenu = DockStartMenu.Build([new StartMenuGroup(parent.Children!)], _ => { });

        var main = Panel(mainMenu);
        var sub = Panel(subMenu);
        sub.HorizontalAlignment = HorizontalAlignment.Left;
        sub.VerticalAlignment = VerticalAlignment.Top;

        var grid = new Grid { Width = 560 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        main.HorizontalAlignment = HorizontalAlignment.Left;
        grid.Children.Add(main);
        grid.Children.Add(sub);
        Grid.SetColumn(sub, 1);

        // Alt menü, üst satırın hizasında açılır (Popup: sağa, -5 DIP dikey kayma).
        grid.Measure(new Size(560, double.PositiveInfinity));
        grid.Arrange(new Rect(0, 0, 560, grid.DesiredSize.Height));
        grid.UpdateLayout();
        var row = ((StackPanel)((Border)main).Child).Children.OfType<MenuItem>().First(m => m.Items.Count > 0);
        var y = row.TranslatePoint(new Point(0, 0), main).Y;
        sub.Margin = new Thickness(-2, y - 5, 0, 0);

        // Alt menü ana menünün altına taşar (gerçekte ekrana sığması için yukarı kayar); görüntüde kesilmesin diye boşluk bırakılır.
        var holder = new Grid { Width = 560 };
        holder.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        holder.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
        holder.Children.Add(grid);
        return holder;
    }

    /// <summary>ContextMenu şablonunun (Styles.xaml) aynısı: yüzey, kenarlık, köşe ve gölge. ContextMenu başka öğenin çocuğu olamadığı için öğeler buraya taşınır.</summary>
    private static Border Panel(ContextMenu menu)
    {
        var items = menu.Items.Cast<object>().ToList();
        menu.Items.Clear();

        var stack = new StackPanel();
        foreach (var item in items)
            stack.Children.Add((UIElement)item);
        Grid.SetIsSharedSizeScope(stack, true);

        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(4),
            MinWidth = 160,
            SnapsToDevicePixels = true,
            Child = stack,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black, Opacity = 0.5, BlurRadius = 20, ShadowDepth = 4, Direction = 270
            },
        };
        border.SetResourceReference(Border.BackgroundProperty, "KoyMenuBg");
        border.SetResourceReference(Border.BorderBrushProperty, "KoyMenuBorder");
        return border;
    }
}
