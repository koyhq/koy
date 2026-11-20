using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Features.Music;

namespace Koy.UiSnapshots;

/// <summary>
/// Sözler görünümünün canlı yolu (#182): pencere ekranda (görünür alanın dışında) gösterilir, konum gerçek zamanla
/// ilerler; satır değişince yaylı kayma ve uzaklık vurgusu kare kare çalışır. Her yarım saniyede etkin satır, kayma
/// ve satır saydamlıkları yazılır, birkaç kare PNG olur. Görsel araçtaki öbür sahneler pencereyi göstermediği için
/// bu yolu sınamaz.
/// </summary>
internal static class LyricsLiveCheck
{
    public static void Run(string outDir, string prefix, Action<FrameworkElement, double, double, string> render)
    {
        var info = new Koy.Core.Media.MediaInfo("Gece Yolculuğu", "Mor ve Ötesi", "Spotify.exe", true, null);
        var media = new FakeMedia(info, liveClock: true);
        var vm = new LyricsViewModel(media, new FakeLyrics(), Dispatcher.CurrentDispatcher);
        var view = new LyricsView { FontSize = 17, Width = 320, Height = 190, DataContext = vm };
        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x17, 0x33)), Child = view };
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            Left = -4000, Top = -4000, SizeToContent = SizeToContent.WidthAndHeight, Content = root
        };
        window.Show();
        vm.SetAllowed(true);
        vm.SetPanelActive(true);

        var log = new List<string>();
        var start = DateTime.Now;
        var shot = 0;
        while ((DateTime.Now - start).TotalSeconds < 8)
        {
            Pump(TimeSpan.FromMilliseconds(500));
            var scroll = (FindScroll(view) as TranslateTransform)?.Y ?? double.NaN;
            var host = (ItemsControl)view.FindName("LinesHost");
            var opacities = new List<string>();
            for (var i = 0; i < host.Items.Count; i++)
                if (host.ItemContainerGenerator.ContainerFromIndex(i) is UIElement c)
                    opacities.Add(c.Opacity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            log.Add(FormattableString.Invariant($"{(DateTime.Now - start).TotalSeconds:0.0}s etkin={vm.ActiveLine?.Text ?? "-"} durum={vm.State} gorunur={view.IsVisible} kayma={scroll:0.0} saydamlik=[{string.Join(" ", opacities)}]"));
            if (shot < 4 && (DateTime.Now - start).TotalSeconds > 1 + shot * 2)
                render(root, 320, 190, Path.Combine(outDir, $"{prefix}-sozler-canli-{shot++}.png"));
        }

        File.WriteAllLines(Path.Combine(outDir, $"{prefix}-sozler-canli.txt"), log);
        foreach (var line in log)
            Console.WriteLine(line);
        window.Close();
        vm.Dispose();
    }

    private static Transform? FindScroll(LyricsView view) => ((ItemsControl)view.FindName("LinesHost")).RenderTransform;

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
