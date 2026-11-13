using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Koy.Core.Notch;
using Koy.Core.Notch.Effects;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>
/// Canlı arka plan yüzeyi: ambiyans geçişi, GIF, sabit görsel ya da video; üstünde koyu perde. Çentik zeminine ve müzik
/// kartının zeminine konur, içeriğin altında kalır ve fareyi geçirir.
/// Kararı vermez: <see cref="Mode"/> Core'dan gelir (<see cref="LiveBackgroundPlaybackPolicy.Decide"/>).
/// <see cref="LiveBackgroundPlaybackMode.Off"/> iken (çentik kapalı / gizli, yüzey hedef dışı), öğe ağaçtan çıkınca ya da
/// görünmez olunca bütün animasyon saatleri durdurulur, kareler ve medya bırakılır: boşta CPU ve bellek sıfır (AGENTS #5, #6).
/// Renk geçişi, GIF ve video WPF animasyon saatleriyle döner. Kodlu efektler (<see cref="BackgroundEffect"/>) küçük bir
/// tampona çizilir ve efektin kare hızında bir zamanlayıcıyla yenilenir; zamanlayıcı yalnız oynarken kuruludur.
/// </summary>
public sealed class LiveBackgroundHost : Border
{
    // Yazılımla çizimde (AGENTS #3) her animasyon karesi bölgeyi yeniden çizer; yavaş geçişler için 15 fps yeter.
    private const int DesiredFrameRate = 15;

    public static readonly DependencyProperty SelectionProperty = DependencyProperty.Register(
        nameof(Selection), typeof(LiveBackgroundSelection), typeof(LiveBackgroundHost),
        new PropertyMetadata(null, (d, _) => ((LiveBackgroundHost)d).Apply()));

    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode), typeof(LiveBackgroundPlaybackMode), typeof(LiveBackgroundHost),
        new PropertyMetadata(LiveBackgroundPlaybackMode.Off, (d, _) => ((LiveBackgroundHost)d).Apply()));

    public static readonly DependencyProperty ScrimOpacityProperty = DependencyProperty.Register(
        nameof(ScrimOpacity), typeof(double), typeof(LiveBackgroundHost),
        new PropertyMetadata(0.65, (d, e) => ((LiveBackgroundHost)d)._scrim.Opacity = (double)e.NewValue));

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
    private readonly Grid _mediaHost = new();
    // Perde yalnız bir arka plan gösterilirken çizilir: yoksa çentik zeminini (tema paketinin KoyNotchSurface rengi dahil)
    // %65 siyahla koyultur ve saydam malzemede zemini neredeyse opak siyaha çevirirdi.
    private readonly Border _scrim = new() { Background = Brushes.Black, Opacity = 0.65, IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    private CancellationTokenSource? _loading;
    private DispatcherTimer? _effectTimer;
    private MediaElement? _media;
    private (LiveBackgroundSelection Selection, LiveBackgroundPlaybackMode Mode)? _applied;

    public LiveBackgroundHost()
    {
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = true;

        var layers = new Grid();
        layers.Children.Add(_art);
        layers.Children.Add(_mediaHost);
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
            case LiveBackgroundContentKind.Ambient when selection.Preset is { Effect: { } effectId } preset
                                                        && BackgroundEffects.Find(effectId) is { } effect:
                ShowEffect(effect, preset, animate);
                break;
            case LiveBackgroundContentKind.Ambient when selection.Preset is { } preset:
                ShowAmbient(preset, animate);
                break;
            case LiveBackgroundContentKind.AnimatedImage or LiveBackgroundContentKind.StillImage when selection.FilePath is { } imagePath:
                _ = ShowImageAsync(imagePath, selection.Kind, animate);
                break;
            case LiveBackgroundContentKind.Video when selection.FilePath is { } videoPath:
                ShowVideo(videoPath, animate);
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

    // ---- Kodlu efekt: küçük tampon, yumuşak büyütme; hareketsizse tek kare ----

    private void ShowEffect(BackgroundEffect effect, LiveBackgroundPreset preset, bool animate)
    {
        // Ölçü henüz yoksa (ilk açılış) açık çentiğin ölçüsü: tuval sabit (AGENTS #13).
        var width = ActualWidth > 0 ? ActualWidth : NotchLayout.ExpandedWidth;
        var height = ActualHeight > 0 ? ActualHeight : NotchLayout.ExpandedHeight;
        var w = Math.Max(8, (int)Math.Ceiling(width / effect.Downscale));
        var h = Math.Max(8, (int)Math.Ceiling(height / effect.Downscale));

        var palette = preset.Colors.Select(EffectColor.Parse).ToArray();
        var pixels = new uint[w * h];
        var bitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
        var brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.Linear);
        _art.Background = brush;

        // Hareketsizde de canlı görünen bir an seçilir (t = 0'da bazı efektler boş başlar).
        const float stillTime = 12f;
        var clock = Stopwatch.StartNew();

        void Draw(float time)
        {
            effect.Render(new EffectFrame(pixels, w, h, time, palette));
            bitmap.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        }

        Draw(stillTime);
        if (!animate)
            return;

        var timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1.0 / effect.FramesPerSecond),
        };
        timer.Tick += (_, _) =>
        {
            if (!ReferenceEquals(_effectTimer, timer))
                return;
            Draw(stillTime + (float)clock.Elapsed.TotalSeconds);
        };
        _effectTimer = timer;
        timer.Start();
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

    // ---- Video: sessiz MediaElement; hareketsizse ilk kareyi gösterip bekler ----

    private void ShowVideo(string path, bool animate)
    {
        var media = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            ScrubbingEnabled = true,
            IsMuted = true,
            Volume = 0,
            Stretch = Stretch.UniformToFill,
            IsHitTestVisible = false,
        };

        media.MediaOpened += (_, _) =>
        {
            if (!ReferenceEquals(_media, media) || animate)
                return;
            media.Pause();
            media.Position = TimeSpan.Zero;
        };
        media.MediaEnded += (_, _) =>
        {
            if (!ReferenceEquals(_media, media) || !animate)
                return;
            media.Position = TimeSpan.Zero;
            media.Play();
        };
        // Codec yoksa (webm için Windows eklentisi gerekebilir) ya da dosya bozuksa arka plan kalkar.
        media.MediaFailed += (_, e) =>
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(() =>
            {
                if (ReferenceEquals(_media, media))
                    Clear();
            });
        };

        _media = media;
        _mediaHost.Children.Add(media);
        media.Source = new Uri(path, UriKind.Absolute);
        media.Play();
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

        if (_effectTimer is { } timer)
        {
            _effectTimer = null;
            timer.Stop();
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

        if (_media is { } media)
        {
            _media = null;
            _mediaHost.Children.Remove(media);
            media.Stop();
            media.Close();
            media.Source = null;
        }
    }
}
