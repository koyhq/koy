using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.App.Features.Dock;
using Koy.App.Features.Dock.Folder;
using Koy.Core.Dock;
using Koy.Core.Shell;

namespace Koy.UiSnapshots;

internal static class DockFolderSnapshots
{
    internal sealed class FakePinnedAppsService : IPinnedAppsService
    {
        public IReadOnlyList<PinnedApp> GetPinnedApps() => [];
    }

    internal sealed class FakeWindowTracker : IWindowTracker
    {
        public event EventHandler<WindowInfo>? ForegroundWindowChanged { add { } remove { } }
        public event EventHandler? WindowsChanged { add { } remove { } }
        public event EventHandler<nint>? WindowFlashing { add { } remove { } }

        public WindowInfo? CurrentForeground => null;
        public IReadOnlyList<OpenWindowInfo> GetOpenWindows() => [];
        public void ActivateWindow(nint handle) { }
        public void MinimizeWindow(nint handle) { }
        public void CloseWindow(nint handle) { }
        public void TerminateProcess(int processId) { }
        public void Start() { }
        public void Stop() { }
        public bool GetCursorPosition(out int x, out int y) { x = 0; y = 0; return true; }
        public void Dispose() { }
    }

    internal sealed class FakeShellActionService : IShellActionService
    {
        public void SendWinKey() { }
        public void ToggleSystemTray() { }
        public void OpenQuickSettings() { }
        public void OpenNotificationCenter() { }
        public void Launch(string path, string? arguments = null) { }
    }

    internal static readonly DockController SharedController = new(
        new FakePinnedAppsService(),
        new FakeWindowTracker(),
        new FakeShellActionService());

    public static ImageSource CreateAppIcon(Color color, string letter)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(0, 0, 32, 32), 8, 8);
            var formattedText = new FormattedText(
                letter,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                16,
                Brushes.White,
                96);
            dc.DrawText(formattedText, new Point((32 - formattedText.Width) / 2, (32 - formattedText.Height) / 2));
        }
        var rtb = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    public static DockItemViewModel CreateAppVm(string id, string title, Color color, string letter, bool isRunning = false)
    {
        var model = new DockItem(id, title, id + ".exe", id + ".exe", [], true, isRunning, false, isRunning ? [1] : []);
        return new DockItemViewModel(model, SharedController)
        {
            Icon = CreateAppIcon(color, letter),
            ItemSize = 38,
            IconSize = 28
        };
    }

    public static void Render(string outDir, string prefix, string theme, Action<FrameworkElement, double, double, string> renderElement)
    {
        var appChrome = CreateAppVm("chrome", "Google Chrome", Color.FromRgb(0x42, 0x85, 0xF4), "C", isRunning: true);
        var appFirefox = CreateAppVm("firefox", "Mozilla Firefox", Color.FromRgb(0xFF, 0x71, 0x39), "F");
        var appEdge = CreateAppVm("edge", "Microsoft Edge", Color.FromRgb(0x00, 0x78, 0xD7), "E");
        var appBrave = CreateAppVm("brave", "Brave Browser", Color.FromRgb(0xFB, 0x54, 0x2B), "B");
        var appCode = CreateAppVm("code", "Visual Studio Code", Color.FromRgb(0x00, 0x98, 0xFF), "V", isRunning: true);
        var appTerminal = CreateAppVm("terminal", "Windows Terminal", Color.FromRgb(0x30, 0x30, 0x30), "T");

        // 1. Klasör kutusu: 2 uygulamalı (Tarayıcılar: Chrome, Firefox)
        var folder2 = new DockFolderViewModel(
            new DockFolder("tarayicilar", "Tarayıcılar", ["chrome", "firefox"]),
            [appChrome, appFirefox]);
        var visual2 = BuildDockItemBox(folder2);
        renderElement(visual2, 70, 70, Path.Combine(outDir, $"{prefix}-{theme}-dock-klasor-2.png"));

        // 2. Klasör kutusu: 4 uygulamalı (Web: Chrome, Firefox, Edge, Brave)
        var folder4 = new DockFolderViewModel(
            new DockFolder("web", "Web Araçları", ["chrome", "firefox", "edge", "brave"]),
            [appChrome, appFirefox, appEdge, appBrave]);
        var visual4 = BuildDockItemBox(folder4);
        renderElement(visual4, 70, 70, Path.Combine(outDir, $"{prefix}-{theme}-dock-klasor-4.png"));

        // 3. Klasör kutusu: 6 uygulamalı (Geliştirme: Chrome, Firefox, Edge, Brave, Code, Terminal)
        var folder6 = new DockFolderViewModel(
            new DockFolder("gelistirme", "Geliştirme", ["chrome", "firefox", "edge", "brave", "code", "terminal"]),
            [appChrome, appFirefox, appEdge, appBrave, appCode, appTerminal]);
        var visual6 = BuildDockItemBox(folder6);
        renderElement(visual6, 70, 70, Path.Combine(outDir, $"{prefix}-{theme}-dock-klasor-6.png"));

        // 4. Hedef parlama / büyüme efekti (1.1x scale + glow)
        var folderGlow = new DockFolderViewModel(
            new DockFolder("parlama", "Geliştirme", ["chrome", "firefox", "edge", "brave"]),
            [appChrome, appFirefox, appEdge, appBrave])
        {
            IsTargetGlow = true
        };
        var visualGlow = BuildDockItemBox(folderGlow);
        renderElement(visualGlow, 70, 70, Path.Combine(outDir, $"{prefix}-{theme}-dock-klasor-parlama.png"));

        // 5. Dock şeridi önizlemesi (Uygulamalar ve klasörler bir arada)
        var dockStrip = BuildDockStrip(appChrome, folder4, appCode, folder2, appTerminal);
        renderElement(dockStrip, 260, 64, Path.Combine(outDir, $"{prefix}-{theme}-dock-serit.png"));

        // 6. Açık FolderPanel (Kayan cam panel, başlık, işlem düğmeleri, 4 sütunlu ızgara)
        var folderPanel = new FolderPanel { DataContext = folder6 };
        var body = folderPanel.DetachBodyForSnapshot();
        body.DataContext = folder6;
        renderElement(body, 280, 0, Path.Combine(outDir, $"{prefix}-{theme}-dock-klasor-panel.png"));
    }

    private static FrameworkElement BuildDockItemBox(DockFolderViewModel vm)
    {
        var box = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x33)),
            BorderThickness = new Thickness(1),
            RenderTransformOrigin = new Point(0.5, 0.5)
        };

        if (vm.IsTargetGlow)
        {
            box.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x3B, 0x82, 0xF6));
            box.BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6));
            box.BorderThickness = new Thickness(1.5);
            box.RenderTransform = new ScaleTransform(1.1, 1.1);
        }

        var grid = new Grid();

        var itemsControl = new ItemsControl
        {
            ItemsSource = vm.PreviewIcons,
            Width = 28,
            Height = 28,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2)
        };
        var panelFactory = new FrameworkElementFactory(typeof(UniformGrid));
        panelFactory.SetValue(UniformGrid.RowsProperty, 2);
        panelFactory.SetValue(UniformGrid.ColumnsProperty, 2);
        itemsControl.ItemsPanel = new ItemsPanelTemplate(panelFactory);

        var itemTemplate = new DataTemplate();
        var imgFactory = new FrameworkElementFactory(typeof(Image));
        imgFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding());
        imgFactory.SetValue(FrameworkElement.WidthProperty, 11.0);
        imgFactory.SetValue(FrameworkElement.HeightProperty, 11.0);
        imgFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
        imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
        imgFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
        itemTemplate.VisualTree = imgFactory;
        itemsControl.ItemTemplate = itemTemplate;

        grid.Children.Add(itemsControl);

        if (vm.IsRunning)
        {
            var dot = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2),
                Width = 6,
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6))
            };
            grid.Children.Add(dot);
        }

        box.Child = grid;

        // Çevreleyen dock kapsülü
        var capsule = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x18, 0x18, 0x1B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        capsule.Child = box;
        return capsule;
    }

    private static FrameworkElement BuildDockStrip(params DockEntryViewModel[] entries)
    {
        var capsule = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x18, 0x18, 0x1B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(10, 6, 10, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var entry in entries)
        {
            if (entry is DockFolderViewModel fvm)
            {
                var box = new Border
                {
                    Width = 38,
                    Height = 38,
                    Margin = new Thickness(3, 0, 3, 0),
                    CornerRadius = new CornerRadius(10),
                    Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x2E, 0x2E, 0x33)),
                    BorderThickness = new Thickness(1)
                };
                var grid = new Grid();
                var itemsControl = new ItemsControl
                {
                    ItemsSource = fvm.PreviewIcons,
                    Width = 28,
                    Height = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2)
                };
                var panelFactory = new FrameworkElementFactory(typeof(UniformGrid));
                panelFactory.SetValue(UniformGrid.RowsProperty, 2);
                panelFactory.SetValue(UniformGrid.ColumnsProperty, 2);
                itemsControl.ItemsPanel = new ItemsPanelTemplate(panelFactory);

                var itemTemplate = new DataTemplate();
                var imgFactory = new FrameworkElementFactory(typeof(Image));
                imgFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding());
                imgFactory.SetValue(FrameworkElement.WidthProperty, 11.0);
                imgFactory.SetValue(FrameworkElement.HeightProperty, 11.0);
                imgFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(1));
                imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
                imgFactory.SetValue(Image.StretchProperty, Stretch.Uniform);
                itemTemplate.VisualTree = imgFactory;
                itemsControl.ItemTemplate = itemTemplate;
                grid.Children.Add(itemsControl);

                if (fvm.IsRunning)
                {
                    grid.Children.Add(new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 2),
                        Width = 6,
                        Height = 3,
                        CornerRadius = new CornerRadius(1.5),
                        Background = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6))
                    });
                }

                box.Child = grid;
                stack.Children.Add(box);
            }
            else if (entry is DockItemViewModel ivm)
            {
                var itemBox = new Border
                {
                    Width = 38,
                    Height = 38,
                    Margin = new Thickness(3, 0, 3, 0),
                    CornerRadius = new CornerRadius(10),
                    Background = Brushes.Transparent
                };
                var grid = new Grid();
                var img = new Image
                {
                    Source = ivm.Icon,
                    Width = 28,
                    Height = 28,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                grid.Children.Add(img);
                if (ivm.IsRunning)
                {
                    grid.Children.Add(new Border
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 2),
                        Width = 6,
                        Height = 3,
                        CornerRadius = new CornerRadius(1.5),
                        Background = new SolidColorBrush(Color.FromRgb(0xA1, 0xA1, 0xAA))
                    });
                }
                itemBox.Child = grid;
                stack.Children.Add(itemBox);
            }
        }

        capsule.Child = stack;
        return capsule;
    }
}
