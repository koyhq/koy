using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Koy.App.Features.Notch;

namespace Koy.UiSnapshots;

/// <summary>
/// Müzik sayfasının tanıtım kaydı (#182): gerçek çentik penceresi ekran dışında açılır, örnek parçalarla bir senaryo
/// oynar (kapalı çentik, yaylı açılış ve kademeli giriş, ayna dalga, parça değişimi, plak, duraklat/çal, sözler,
/// kapanış). Animasyonlar uygulamadaki gibi gerçek zamanda çalışır; ~15 fps'de saydam PNG kare ve her karenin zamanı
/// (<c>frames.txt</c>, ms) yazılır. GIF ve WebP'yi kare klasöründen ayrı bir betik üretir (marketing/, git dışı).
/// Gerçek medya oturumu, ses ya da ağ kullanılmaz; ses düzeyleri yapaydır.
/// </summary>
internal static class MusicPromoRecorder
{
    private const double Scale = 2;
    private const double FrameSeconds = 1.0 / 15;

    public static void Record(string outDir, string prefix,
        Func<(NotchWindow Window, NotchViewModel Vm, FakeMedia Media, byte[] SecondCover)> make)
    {
        var dir = Path.Combine(outDir, prefix + "-muzik-kayit");
        Directory.CreateDirectory(dir);
        foreach (var old in Directory.GetFiles(dir, "*.png"))
            File.Delete(old);

        var (window, vm, media, secondCover) = make();
        window.Show();
        Pump(TimeSpan.FromMilliseconds(300));
        // Açılışta pencere ekranın üst ortasına yerleşir; kayıt ekranda görünmesin.
        window.Left = -4000;
        window.Top = -4000;

        var music = vm.Music;
        music.SetSpectrumAllowed(true);
        var second = new Koy.Core.Media.MediaInfo(Sample.T("Deniz Feneri", "Lighthouse"), Sample.T("Kıyı", "The Shore"),
            "Spotify.exe", true, secondCover);

        // Senaryo: (saniye, eylem). Zamanlar gerçek saatle; animasyonlar kendi yaylarıyla akar.
        var script = new (double At, Action Do)[]
        {
            (1.5, () => { vm.Controller.Expand(Koy.Core.Notch.NotchPanel.Home); vm.ShowHomePageCommand.Execute(Koy.Core.Notch.NotchHomePage.Music); }),
            (5.0, () => media.Set(second, TimeSpan.FromSeconds(66))),
            (7.5, () => music.SetVinyl(true)),
            (10.0, () => media.Set(second with { IsPlaying = false })),
            (11.5, () => media.Set(second with { IsPlaying = true }, TimeSpan.FromSeconds(81))),
            (13.0, () => { music.SetVinyl(false); music.Lyrics.SetAllowed(true); }),
            (17.5, () => vm.Controller.OnMouseLeaveTimeout()),
        };
        const double end = 19;

        // Pencerenin tamamı (sabit 640×364 tuval): kök öğe çentik şekli kadar büyüdüğü için onu değil pencereyi çizeriz.
        var content = (FrameworkElement)window;
        var times = new List<long>();
        var clock = Stopwatch.StartNew();
        var next = 0;
        var frame = 0;
        while (clock.Elapsed.TotalSeconds < end)
        {
            var now = clock.Elapsed.TotalSeconds;
            while (next < script.Length && script[next].At <= now)
                script[next++].Do();

            music.Spectrum.Push(Levels(now, music.IsPlaying));
            Pump(TimeSpan.FromMilliseconds(5));
            Capture(content, Path.Combine(dir, $"f{frame++:0000}.png"));
            times.Add(clock.ElapsedMilliseconds);

            var wait = FrameSeconds * frame - clock.Elapsed.TotalSeconds;
            if (wait > 0)
                Pump(TimeSpan.FromSeconds(wait));
        }

        File.WriteAllLines(Path.Combine(dir, "frames.txt"), times.Select(t => t.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Console.WriteLine($"{frame} kare, {clock.Elapsed.TotalSeconds:0.0} sn: {dir}");
        window.Close();
        vm.Dispose();
    }

    // Yapay ses: bas bantlar güçlü, her bant kendi hızında dalgalanır; duraklatınca sessiz.
    private static float[] Levels(double t, bool playing)
    {
        var levels = new float[24];
        if (!playing)
            return levels;

        for (var i = 0; i < levels.Length; i++)
        {
            var weight = 1.0 - i / 32.0;
            var wave = Math.Abs(Math.Sin(t * (2.1 + i * 0.37) + i * 1.7)) * 0.6 + Math.Abs(Math.Sin(t * 5.3 + i)) * 0.25;
            levels[i] = (float)Math.Clamp(0.12 + wave * weight, 0, 1);
        }
        return levels;
    }

    private static void Capture(FrameworkElement content, string path)
    {
        var width = (int)Math.Ceiling(content.ActualWidth * Scale);
        var height = (int)Math.Ceiling(content.ActualHeight * Scale);
        var bitmap = new RenderTargetBitmap(width, height, 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }
}
