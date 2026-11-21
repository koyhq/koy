using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.Core.Notch;

namespace Koy.App.Features.Music;

/// <summary>
/// Yakalama iş parçacığından gelen çubuk düzeylerini arayüz iş parçacığına taşır. Kareler birikmez: arayüz bir
/// öncekini çizmeden gelen yenisi yalnız son değeri günceller (kuyrukta en çok bir çağrı).
/// </summary>
public sealed class SpectrumFeed
{
    private readonly Dispatcher _dispatcher;
    private readonly object _gate = new();
    private readonly float[] _pending;
    private bool _queued;
    private long _minIntervalMs;
    private long _lastDispatchTick;

    public SpectrumFeed(int bandCount, Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _pending = new float[bandCount];
        Levels = new float[bandCount];
    }

    /// <summary>Arayüz iş parçacığında okunan son düzeyler (0..1).</summary>
    public float[] Levels { get; }

    /// <summary>Yeni düzeyler arayüze geçti (arayüz iş parçacığında).</summary>
    public event Action? Updated;

    /// <summary>
    /// En küçük arayüz yenileme aralığını (ms) ayarlar. Kapalı çentikte 15 fps için (~60 ms)
    /// gereksiz yazılımsal yeniden çizimi keser; açık kartta 0 (yakalama hızında, ~30 fps) olur.
    /// </summary>
    public void SetMinIntervalMs(int intervalMs) =>
        Volatile.Write(ref _minIntervalMs, Math.Max(0, intervalMs));

    /// <summary>Herhangi bir iş parçacığından çağrılır.</summary>
    public void Push(float[] levels) => Push(levels, force: false);

    private void Push(float[] levels, bool force)
    {
        lock (_gate)
        {
            Array.Copy(levels, _pending, Math.Min(levels.Length, _pending.Length));
            if (_queued)
                return;

            var minInterval = Volatile.Read(ref _minIntervalMs);
            // Sessizlik karesi hiçbir zaman düşürülmez ki yakalama durduğunda çubuklar havada donmasın.
            if (!force && minInterval > 0 && !IsSilent(levels))
            {
                var now = Environment.TickCount64;
                if (now - _lastDispatchTick < minInterval)
                    return;
            }

            _lastDispatchTick = Environment.TickCount64;
            _queued = true;
        }

        _dispatcher.BeginInvoke(DispatcherPriority.Render, Apply);
    }

    /// <summary>Çubukları sıfırlar (yakalama durdu).</summary>
    public void Clear() => Push(new float[_pending.Length], force: true);

    private static bool IsSilent(float[] levels)
    {
        for (var i = 0; i < levels.Length; i++)
        {
            if (levels[i] > 0.01f)
                return false;
        }
        return true;
    }

    private void Apply()
    {
        lock (_gate)
        {
            Array.Copy(_pending, Levels, Levels.Length);
            _queued = false;
        }

        Updated?.Invoke();
    }
}

/// <summary>
/// Müzik kartındaki ses çubukları (tasarım H: 24 çubuk, 4 px genişlik, 3 px boşluk, en çok 26 px, alttan büyür).
/// Tek OnRender ile çizilir; yalnız <see cref="SpectrumFeed.Updated"/> gelince yeniden çizilir (ses yokken çizim yok).
/// </summary>
public sealed class SpectrumBars : FrameworkElement
{
    private const double MinBar = 3;

    /// <summary>Çubuk genişliği (küçük kart 4, büyük kart daha geniş).</summary>
    public static readonly DependencyProperty BarWidthProperty = DependencyProperty.Register(
        nameof(BarWidth), typeof(double), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Çubuklar arası boşluk.</summary>
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public double BarWidth
    {
        get => (double)GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public static readonly DependencyProperty VisualizerStyleProperty = DependencyProperty.Register(
        nameof(VisualizerStyle), typeof(NotchVisualizerStyle), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(NotchVisualizerStyle.Bars, FrameworkPropertyMetadataOptions.AffectsRender));

    public NotchVisualizerStyle VisualizerStyle
    {
        get => (NotchVisualizerStyle)GetValue(VisualizerStyleProperty);
        set => SetValue(VisualizerStyleProperty, value);
    }

    /// <summary>
    /// Ayna dalga (#182, <see cref="MirrorWave"/>): stil ve çubuk sayısı ayarına bakılmaz; çubuklar bütün genişliği
    /// doldurur, ortada bas, kenarlarda tiz, ortadan iki yöne büyür. Müzik sayfası kullanır.
    /// </summary>
    public static readonly DependencyProperty MirrorProperty = DependencyProperty.Register(
        nameof(Mirror), typeof(bool), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public bool Mirror
    {
        get => (bool)GetValue(MirrorProperty);
        set => SetValue(MirrorProperty, value);
    }

    public static readonly DependencyProperty BarCountProperty = DependencyProperty.Register(
        nameof(BarCount), typeof(int), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public int BarCount
    {
        get => (int)GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    public static readonly DependencyProperty FeedProperty = DependencyProperty.Register(
        nameof(Feed), typeof(SpectrumFeed), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnFeedChanged));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(SpectrumBars),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public SpectrumFeed? Feed
    {
        get => (SpectrumFeed?)GetValue(FeedProperty);
        set => SetValue(FeedProperty, value);
    }

    public Brush Fill
    {
        get => (Brush)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public SpectrumBars()
    {
        // Görsel ağaçtan çıkınca olay aboneliği kalmasın (çentik pencereleri kapanıp açılabilir).
        Loaded += (_, _) => Subscribe(Feed);
        Unloaded += (_, _) => Unsubscribe(Feed);
    }

    private static void OnFeedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var bars = (SpectrumBars)d;
        bars.Unsubscribe(e.OldValue as SpectrumFeed);
        if (bars.IsLoaded)
            bars.Subscribe(e.NewValue as SpectrumFeed);
    }

    private void Subscribe(SpectrumFeed? feed)
    {
        if (feed is null)
            return;
        feed.Updated -= InvalidateVisual;
        feed.Updated += InvalidateVisual;
    }

    private void Unsubscribe(SpectrumFeed? feed)
    {
        if (feed is not null)
            feed.Updated -= InvalidateVisual;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Mirror)
            return new Size(0, 0);

        var rawCount = Feed?.Levels.Length ?? 0;
        var count = BarCount > 0 ? Math.Min(rawCount, NotchVisualizer.ClampBarCount(BarCount)) : rawCount;
        return new Size(count > 0 ? count * BarWidth + (count - 1) * Gap : 0, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var rawLevels = Feed?.Levels;
        if (rawLevels is null || rawLevels.Length == 0)
            return;

        var count = BarCount > 0 ? Math.Min(rawLevels.Length, NotchVisualizer.ClampBarCount(BarCount)) : rawLevels.Length;
        if (count <= 0)
            return;

        if (Mirror)
        {
            RenderMirror(drawingContext, rawLevels);
            return;
        }

        var height = ActualHeight;
        var barWidth = BarWidth;
        var gap = Gap;
        var radius = barWidth / 2;
        var total = count * barWidth + (count - 1) * gap;
        var x = Math.Max(0, (ActualWidth - total) / 2);
        var fill = Fill ?? TryFindResource("KoyNotchMusicSpectrum") as Brush ?? Brushes.White;
        var style = VisualizerStyle;

        for (var i = 0; i < count; i++, x += barWidth + gap)
        {
            var level = SampleLevel(rawLevels, i, count);
            switch (style)
            {
                case NotchVisualizerStyle.Wave:
                    var midY = height / 2;
                    var halfH = Math.Max(MinBar / 2, Math.Round((level * height) / 2));
                    drawingContext.DrawRoundedRectangle(fill, null, new Rect(x, midY - halfH, barWidth, halfH * 2), radius, radius);
                    break;

                case NotchVisualizerStyle.Dots:
                    var dotBarHeight = Math.Max(MinBar, Math.Round(level * height));
                    var dotY = Math.Max(radius, Math.Min(height - radius, height - dotBarHeight));
                    drawingContext.DrawEllipse(fill, null, new Point(x + radius, dotY), radius, radius);
                    break;

                case NotchVisualizerStyle.Bars:
                default:
                    var barHeight = Math.Max(MinBar, Math.Round(level * height));
                    drawingContext.DrawRoundedRectangle(fill, null, new Rect(x, height - barHeight, barWidth, barHeight), radius, radius);
                    break;
            }
        }
    }

    private void RenderMirror(DrawingContext drawingContext, float[] levels)
    {
        var count = MirrorWave.DefaultBarCount;
        var gap = Gap;
        var barWidth = (ActualWidth - gap * (count - 1)) / count;
        if (barWidth <= 0)
            return;

        var height = ActualHeight;
        var midY = height / 2;
        var radius = Math.Min(barWidth / 2, 2);
        var fill = Fill ?? TryFindResource("KoyNotchMusicSpectrum") as Brush ?? Brushes.White;
        var x = 0.0;
        for (var i = 0; i < count; i++, x += barWidth + gap)
        {
            var halfH = Math.Max(MinBar / 2, MirrorWave.LevelFor(levels, i, count) * height / 2);
            drawingContext.DrawRoundedRectangle(fill, null, new Rect(x, midY - halfH, barWidth, halfH * 2), radius, radius);
        }
    }

    private static float SampleLevel(float[] rawLevels, int barIndex, int barCount)
    {
        if (barCount >= rawLevels.Length)
            return rawLevels[barIndex];

        // Küçük çubuk sayısında (ör. kapalı çentikte 5 çubuk) yalnız en düşük bas bantları yerine tüm tayfı örnekler.
        var start = barIndex * rawLevels.Length / barCount;
        var end = Math.Max(start + 1, (barIndex + 1) * rawLevels.Length / barCount);
        var peak = 0f;
        for (var j = start; j < end && j < rawLevels.Length; j++)
        {
            if (rawLevels[j] > peak)
                peak = rawLevels[j];
        }
        return peak;
    }
}
