using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Koy.App.Resources;
using Koy.Core.Animation;

namespace Koy.App.Features.Music;

/// <summary>Söz listesi: etkin satırı yayla görünümün üst kısmına getirir. Yalnız satır değişirken kare çizilir (AGENTS #6).</summary>
public partial class LyricsView : UserControl
{
    // Etkin satırın görünümdeki yeri: altta gelecek satırlara daha çok yer kalır.
    private const double AnchorRatio = 0.35;

    private readonly Spring _scroll = new(0) { Stiffness = 130, Damping = 20, RestThreshold = 0.5 };
    private LyricsViewModel? _viewModel;
    private bool _animating;
    private bool _scrollQueued;
    private bool _snapNext = true;
    private TimeSpan _lastFrame;

    public LyricsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        IsVisibleChanged += OnIsVisibleChanged;
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

        var target = 0.0;
        if (_viewModel.ActiveLine is { } line && LinesHost.ItemContainerGenerator.ContainerFromItem(line) is FrameworkElement container)
        {
            var center = container.TranslatePoint(new Point(), LinesHost).Y + container.ActualHeight / 2;
            var lowest = Math.Min(0, Viewport.ActualHeight - LinesHost.ActualHeight);
            target = Math.Clamp(Viewport.ActualHeight * AnchorRatio - center, lowest, 0);
        }

        if (_snapNext || !Motion.Enabled)
        {
            _snapNext = false;
            StopAnimation();
            _scroll.SnapTo(target);
            ScrollTransform.Y = target;
            return;
        }

        _scroll.Target = target;
        WakeAnimation();
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
        if (!moving)
            StopAnimation();
    }
}
