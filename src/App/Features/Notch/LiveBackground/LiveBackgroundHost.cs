using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Koy.Core.Media;
using Koy.Core.Notch;
using Koy.Platform.Media;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>
/// Canlı arka plan yüzeyi: ambiyans geçişi, GIF, sabit görsel ya da video; üstünde koyu perde. Çentik zeminine ve müzik
/// kartının zeminine konur, içeriğin altında kalır ve fareyi geçirir.
/// Kararı vermez: <see cref="Mode"/> Core'dan gelir (<see cref="LiveBackgroundPlaybackPolicy.Decide"/>).
/// <see cref="LiveBackgroundPlaybackMode.Off"/> iken (çentik kapalı / gizli, yüzey hedef dışı), öğe ağaçtan çıkınca ya da
/// görünmez olunca bütün animasyon saatleri durdurulur, kareler ve medya bırakılır: boşta CPU ve bellek sıfır (AGENTS #5, #6).
/// Renk geçişi, GIF ve video WPF animasyon saatleriyle döner. Video yalnız Animate'te açılır; durağan karede yerleşik
/// döngülerin posteri gösterilir.
/// </summary>
public sealed class LiveBackgroundHost : Border
{
    // Yazılımla çizimde (AGENTS #3) her animasyon karesi bölgeyi yeniden çizer; yavaş geçişler için 15 fps yeter.
    private const int DesiredFrameRate = 15;

    // Yerleşik döngüler 3,5 sn'lik kısa kesitler; tam hızda hareket çentik zemini için fazla hızlı kalıyor.
    private const double BuiltinVideoSpeed = 0.5;

    /// <summary>Video kare çözücüsü; varsayılanı Media Foundation ile yazılımla çözer (Direct3D yüklemez).</summary>
    public static IVideoFrameReaderFactory VideoReaders { get; set; } = new MfVideoFrameReaderFactory();

    // Yerel video ilk kareyi çözene dek alttaki kart kapağı görünmesin diye çentik renginde dondurulmuş yedek zemin.
    private static readonly Brush VideoFallbackBrush = CreateFallbackBrush();

    private static SolidColorBrush CreateFallbackBrush()
    {
        var brush = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0B));
        brush.Freeze();
        return brush;
    }

    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection), typeof(LiveBackgroundSelection), typeof(LiveBackgroundHost),
        new PropertyMetadata(null, (d, _) => ((LiveBackgroundHost)d).Apply()));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(LiveBackgroundPlaybackMode), typeof(LiveBackgroundHost),
        new PropertyMetadata(LiveBackgroundPlaybackMode.Off, (d, _) => ((LiveBackgroundHost)d).Apply()));

    public static readonly DependencyProperty ScrimOpacityProperty = DependencyProperty.Register(
        nameof(ScrimOpacity), typeof(double), typeof(LiveBackgroundHost),
        new PropertyMetadata(0.45, (d, e) => ((LiveBackgroundHost)d)._scrim.Opacity = (double)e.NewValue));

    public LiveBackgroundSelection? Selection
    {
        get => (LiveBackgroundSelection?)GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    public LiveBackgroundPlaybackMode Mode
    {
        get => (LiveBackgroundPlaybackMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double ScrimOpacity
    {
        get => (double)GetValue(ScrimOpacityProperty);
        set => SetValue(ScrimOpacityProperty, value);
    }

    private readonly Border _art = new();
    // Perde yalnız bir arka plan gösterilirken çizilir: yoksa çentik zeminini (tema paketinin KoyNotchSurface rengi dahil)
    // siyahla koyultur ve saydam malzemede zemini neredeyse opak siyaha çevirirdi.
    private readonly Border _scrim = new() { Background = Brushes.Black, Opacity = 0.45, IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private CancellationTokenSource? _loading;
    private (LiveBackgroundSelection Selection, LiveBackgroundPlaybackMode Mode)? _applied;

    public LiveBackgroundHost()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = true;

        var layers = new Grid();
        layers.Children.Add(_art);
        layers.Children.Add(_scrim);
        Child = layers;

        Loaded += (_, _) => Apply();
        Unloaded += (_, _) => Clear();
        IsVisibleChanged += (_, _) => Apply();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        // Border çocuklarını yuvarlak köşeye kırpmaz; kart köşesi için geometri ile kırpılır.
        var radius = CornerRadius.TopLeft;
        Clip = radius > 0 && sizeInfo.NewSize.Width > 0 && sizeInfo.NewSize.Height > 0
            ? new RectangleGeometry(new Rect(sizeInfo.NewSize), radius, radius)
            : null;
    }

    private void Apply()
    {
        var selection = Selection;
        var mode = IsLoaded && IsVisible ? Mode : LiveBackgroundPlaybackMode.Off;

        if (selection is null || selection.Kind == LiveBackgroundContentKind.None || mode == LiveBackgroundPlaybackMode.Off)
        {
            Clear();
            return;
        }

        if (_applied is { } current && current.Selection == selection && current.Mode == mode)
            return;

        Clear();
        _applied = (selection, mode);
        _scrim.Visibility = Visibility.Visible;
        var animate = mode == LiveBackgroundPlaybackMode.Animate;

        switch (selection.Kind)
        {
            case LiveBackgroundContentKind.Ambient when selection.Preset is { VideoFile: { } videoFile } preset:
                var builtinVideoPath = Path.Combine(AppContext.BaseDirectory, "assets", "backgrounds", videoFile);
                var poster = LiveBackgroundPreviewRenderer.Get(preset.Id);
                if (!animate)
                    // Durağan karede poster zaten 0. kare: video çözücü (ve ekran kartı sürücüleri) hiç açılmaz.
                    ShowStill(poster);
                else if (File.Exists(builtinVideoPath))
                    ShowVideo(builtinVideoPath, animate, poster, BuiltinVideoSpeed);
                else
                    ShowAmbient(preset, animate);
                break;
            case LiveBackgroundContentKind.Ambient when selection.Preset is { } preset:
                ShowAmbient(preset, animate);
                break;
            case LiveBackgroundContentKind.AnimatedImage or LiveBackgroundContentKind.StillImage when selection.FilePath is { } imagePath:
                _ = ShowImageAsync(imagePath, selection.Kind, animate);
                break;
            case LiveBackgroundContentKind.Video when selection.FilePath is { } videoPath:
                ShowVideo(videoPath, animate, poster: null, speed: 1.0);
                break;
        }
    }

    // ---- Yerleşik ambiyans: renk durakları sırayla kayar; hareketsizse yalnız ilk kare (önayarın kendi renkleri) ----

    private void ShowAmbient(LiveBackgroundPreset preset, bool animate)
    {
        var colors = preset.Colors.Select(Parse).ToArray();
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        for (var i = 0; i < colors.Length; i++)
            brush.GradientStops.Add(new GradientStop(colors[i], colors.Length == 1 ? 0 : i / (double)(colors.Length - 1)));
        _art.Background = brush;

        if (!animate || colors.Length < 2)
            return;

        var cycle = TimeSpan.FromSeconds(preset.CycleSeconds);
        for (var i = 0; i < colors.Length; i++)
        {
            // Her durak, dizideki sıradaki renklere geçip başa döner: bütün gradyan kesintisiz döner.
            var animation = new ColorAnimationUsingKeyFrames { Duration = cycle, RepeatBehavior = RepeatBehavior.Forever };
            for (var k = 0; k <= colors.Length; k++)
                animation.KeyFrames.Add(new LinearColorKeyFrame(colors[(i + k) % colors.Length], KeyTime.FromPercent(k / (double)colors.Length)));
            Timeline.SetDesiredFrameRate(animation, DesiredFrameRate);
            brush.GradientStops[i].BeginAnimation(GradientStop.ColorProperty, animation);
        }
    }

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    // ---- GIF / görsel: kareler arka planda çözülür, ImageBrush kaynağı anahtar karelerle değiştirilir ----

    private async Task ShowImageAsync(string path, LiveBackgroundContentKind kind, bool animate)
    {
        var cts = new CancellationTokenSource();
        _loading = cts;

        var loaded = await LiveBackgroundLoader.LoadAsync(path, kind, firstFrameOnly: !animate, cts.Token);

        // Bu arada kapandı, hedef değişti ya da yenisi başladı: çözülen kareler bırakılır.
        if (cts.IsCancellationRequested || !ReferenceEquals(_loading, cts) || loaded is null || loaded.Frames.Count == 0)
            return;

        var brush = new ImageBrush(loaded.Frames[0]) { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        _art.Background = brush;

        if (!animate || loaded.Frames.Count < 2)
            return;

        var animation = new ObjectAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever };
        var at = TimeSpan.Zero;
        for (var i = 0; i < loaded.Frames.Count; i++)
        {
            animation.KeyFrames.Add(new DiscreteObjectKeyFrame(loaded.Frames[i], KeyTime.FromTimeSpan(at)));
            at += TimeSpan.FromMilliseconds(loaded.DelaysMs[i]);
        }

        animation.Duration = at;
        Timeline.SetDesiredFrameRate(animation, 25);
        brush.BeginAnimation(ImageBrush.ImageSourceProperty, animation);
    }

    // ---- Video: kendi yazılım çözücümüz (Media Foundation, DXVA kapalı); kareler tek bir WriteableBitmap'e yazılır ----

    private void ShowVideo(string path, bool animate, BitmapSource? poster, double speed)
    {
        // İlk kare çözülene dek yerleşik döngülerde 0. karenin posteri, yerel videoda koyu zemin görünür.
        if (poster is not null)
            ShowStill(poster);
        else
            _art.Background = VideoFallbackBrush;

        var cts = new CancellationTokenSource();
        _loading = cts;
        var token = cts.Token;
        _ = Task.Factory.StartNew(() => RunVideo(path, animate, speed, token), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    // Arka plan iş parçacığı: kareyi çöz, zamanı gelince arayüz iş parçacığında bitmap'e yaz. Hareket yoksa yalnız ilk kare.
    private void RunVideo(string path, bool animate, double speed, CancellationToken token)
    {
        using var reader = VideoReaders.Open(path);
        if (reader is null)
        {
            // Codec yoksa (webm için Windows eklentisi gerekebilir) ya da dosya bozuksa arka plan kalkar.
            Dispatcher.BeginInvoke(() =>
            {
                if (!token.IsCancellationRequested)
                    Clear();
            });
            return;
        }

        var frame = new byte[reader.Width * reader.Height * 4];
        var pacer = new VideoFramePacer(speed, DesiredFrameRate);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var origin = TimeSpan.Zero; // ilk karenin gösterildiği an, onun zamanına eşlenir
        WriteableBitmap? bitmap = null;

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (!reader.ReadNextFrame(frame, out var stamp))
                    return;

                var due = pacer.DueAt(stamp);
                if (bitmap is not null)
                {
                    if (!pacer.ShouldShow(due, clock.Elapsed - origin))
                        continue;
                    var wait = due - (clock.Elapsed - origin);
                    if (wait > TimeSpan.Zero && token.WaitHandle.WaitOne(wait))
                        return;
                }

                // Arayüz kareyi kopyalayana dek beklenir: tek tampon yeter, çözücü arayüzün önüne geçmez.
                Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    var rect = new Int32Rect(0, 0, reader.Width, reader.Height);
                    if (bitmap is null)
                    {
                        bitmap = new WriteableBitmap(reader.Width, reader.Height, 96, 96, PixelFormats.Bgra32, null);
                        bitmap.WritePixels(rect, frame, reader.Width * 4, 0);
                        var brush = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
                        _art.Background = brush;
                        return;
                    }
                    bitmap.WritePixels(rect, frame, reader.Width * 4, 0);
                }, DispatcherPriority.Render, token);

                if (!animate)
                    return;
                if (origin == TimeSpan.Zero)
                {
                    origin = clock.Elapsed - due;
                    pacer.ShouldShow(due, due);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException)
        {
            // Uygulama kapanırken Dispatcher.Invoke iptal edilebilir; çözücü yine bırakılır (using).
        }
    }

    private void ShowStill(BitmapSource image)
    {
        var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        _art.Background = brush;
    }

    /// <summary>Bütün saatleri durdurur, kareleri ve medyayı bırakır. Çentik kapanınca / yüzey görünmez olunca burası çalışır.</summary>
    private void Clear()
    {
        _applied = null;
        _scrim.Visibility = Visibility.Collapsed;
        if (_loading is { } cts)
        {
            _loading = null;
            cts.Cancel();
            cts.Dispose();
        }

        switch (_art.Background)
        {
            case ImageBrush image:
                image.BeginAnimation(ImageBrush.ImageSourceProperty, null);
                image.ImageSource = null;
                break;
            case LinearGradientBrush gradient:
                foreach (var stop in gradient.GradientStops)
                    stop.BeginAnimation(GradientStop.ColorProperty, null);
                break;
        }

        _art.Background = null;
    }
}
