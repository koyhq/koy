using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Lyrics;

namespace Koy.App.Features.Music;

/// <summary>
/// Söz listesi (issue #182): etkin satır görünümün ortasına yayla gelir; öbür satırlar o çizgiye uzaklıklarına göre
/// solar ve küçülür (<see cref="LyricsEmphasis"/>). Vurgu konumdan hesaplandığı için kayma sürerken satırlar da
/// yumuşakça parlayıp söner. Yalnız kayarken kare çizilir (AGENTS #6).
/// </summary>
public partial class LyricsView : UserControl
{
    // Etkin satırın görünümdeki yeri: kenar maskesinin (üst %22, alt %28) tam ortası.
    private const double AnchorRatio = 0.47;

    // Uzaklığın "bir satır" birimi: tek satırlık sözün yüksekliği yazı boyutuyla ölçeklenir.
    private double LineUnit => Math.Max(16, FontSize * 1.33 + 8);

    private readonly Spring _scroll = new(0) { Stiffness = 150, Damping = 19, RestThreshold = 0.5 };
    private LyricsViewModel? _viewModel;
    private bool _animating;
    private bool _scrollQueued;
    private bool _snapNext = true;
    private TimeSpan _lastFrame;

    // Satırların liste içindeki orta noktaları (kaymadan bağımsız); yerleşim değişince yenilenir.
    private readonly List<(FrameworkElement Container, ScaleTransform Scale, double Center)> _rows = [];

    public LyricsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
        LinesHost.SizeChanged += (_, _) => QueueScroll();
        Viewport.SizeChanged += (_, _) =>
        {
            UpdateEdgeMask();
            QueueScroll();
        };
        Unloaded += (_, _) => StopAnimation();
    }

    // Satırlar arasındaki boşluğa tıklama kartı küçültmesin.
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        e.Handled = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = e.NewValue as LyricsViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LyricsViewModel.Lines))
        {
            _snapNext = true;
            QueueScroll();
        }
        else if (e.PropertyName == nameof(LyricsViewModel.ActiveLine))
        {
            QueueScroll();
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            _snapNext = true;
            QueueScroll();
        }
        else
        {
            StopAnimation();
        }
    }

    // Yeni satırların kabı henüz oluşmamış olabilir: yerleşimden sonra ölçülür.
    private void QueueScroll()
    {
        if (_scrollQueued)
            return;

        _scrollQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ScrollToActive);
    }

    private void ScrollToActive()
    {
        _scrollQueued = false;
        if (_viewModel is null || !IsVisible)
            return;

        MeasureRows();
        var target = TargetOffset(_viewModel);

        if (_snapNext || !Motion.Enabled)
        {
            _snapNext = false;
            StopAnimation();
            _scroll.SnapTo(target);
            ScrollTransform.Y = target;
            ApplyEmphasis();
            return;
        }

        _scroll.Target = target;
        WakeAnimation();
    }

    /// <summary>
    /// Etkin satıra animasyonsuz hizalar, görünürlüğe bakmaz. Yalnız görsel kanıt aracı (tools/Koy.UiSnapshots) kullanır:
    /// orada pencere ekrana çıkmadığı için görünüm kendini görünmez sayar.
    /// </summary>
    public void SnapForSnapshot()
    {
        if (_viewModel is null)
            return;

        UpdateLayout();
        MeasureRows();
        var target = TargetOffset(_viewModel);
        _scroll.SnapTo(target);
        ScrollTransform.Y = target;
        ApplyEmphasis();
    }

    // Etkin satır çizgiye gelir; henüz satır yoksa (giriş) ilk satır çizgide bekler: alan boş görünmesin.
    private double TargetOffset(LyricsViewModel viewModel)
    {
        var anchor = Viewport.ActualHeight * AnchorRatio;
        if (viewModel.ActiveLine is { } line && LinesHost.ItemContainerGenerator.ContainerFromItem(line) is FrameworkElement active
            && _rows.FindIndex(r => ReferenceEquals(r.Container, active)) is var index and >= 0)
            return anchor - _rows[index].Center;

        return _rows.Count > 0 ? anchor - _rows[0].Center : 0;
    }

    private void MeasureRows()
    {
        _rows.Clear();
        var generator = LinesHost.ItemContainerGenerator;
        for (var i = 0; i < LinesHost.Items.Count; i++)
        {
            if (generator.ContainerFromIndex(i) is not FrameworkElement container || container.ActualHeight <= 0)
                continue;

            if (container.RenderTransform is not ScaleTransform scale || scale.IsFrozen)
            {
                scale = new ScaleTransform();
                container.RenderTransform = scale;
                container.RenderTransformOrigin = new Point(0, 0.5);
            }

            var center = container.TranslatePoint(new Point(), LinesHost).Y + container.ActualHeight / 2;
            _rows.Add((container, scale, center));
        }
    }

    private void ApplyEmphasis()
    {
        var anchor = Viewport.ActualHeight * AnchorRatio - ScrollTransform.Y;
        var unit = LineUnit;
        foreach (var (container, scale, center) in _rows)
        {
            var distance = (center - anchor) / unit;
            container.Opacity = LyricsEmphasis.OpacityAt(distance);
            scale.ScaleX = scale.ScaleY = LyricsEmphasis.ScaleAt(distance);
        }
    }

    // Kenar maskesi görünümün kendi yüksekliğine göre, piksel cinsinden: göreli (0–1) maske öğenin değil içeriğinin
    // sınırına yayılır; uzun söz listesinde saydam olmayan bant görünen alanın dışına düşüp bütün sözleri soldururdu.
    private void UpdateEdgeMask()
    {
        var height = Viewport.ActualHeight;
        if (height <= 0)
        {
            Viewport.OpacityMask = null;
            return;
        }

        var mask = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, height),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, 0.22),
                new GradientStop(Colors.Black, 0.72),
                new GradientStop(Colors.Transparent, 1),
            },
        };
        mask.Freeze();
        Viewport.OpacityMask = mask;
    }

    private void WakeAnimation()
    {
        if (_animating)
            return;

        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopAnimation()
    {
        if (!_animating)
            return;

        CompositionTarget.Rendering -= OnRendering;
        _animating = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt <= 0)
            return;

        var moving = _scroll.Step(dt);
        ScrollTransform.Y = _scroll.Value;
        ApplyEmphasis();
        if (!moving)
            StopAnimation();
    }
}
