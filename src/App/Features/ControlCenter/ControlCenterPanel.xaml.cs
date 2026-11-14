using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Notch;

namespace Koy.App.Features.ControlCenter;

public partial class ControlCenterPanel : UserControl
{
    private const double Gap = 8;

    // Her döşeme kendi yayıyla (Spring) yatay kayar; kareler yalnız hareket sürerken dinlenir (AGENTS.md #6).
    private sealed class Tile(string id, FrameworkElement element)
    {
        public string Id { get; } = id;
        public FrameworkElement Element { get; } = element;
        public Spring X { get; } = new(0);
        public TranslateTransform Transform { get; } = new();
    }

    private readonly List<Tile> _tiles = [];
    private List<string> _order = [];
    private double _slotWidth;
    private bool _animating;
    private TimeSpan _lastFrame;

    private Tile? _pressed;
    private Tile? _dragging;
    private double _pressX;
    private double _grabOffset;

    private ControlCenterViewModel? ViewModel => DataContext as ControlCenterViewModel;

    public ControlCenterPanel()
    {
        InitializeComponent();
        foreach (var element in new FrameworkElement[] { TileWifi, TileBluetooth, TileDoNotDisturb })
        {
            var tile = new Tile((string)element.Tag, element);
            element.RenderTransform = tile.Transform;
            _tiles.Add(tile);
        }

        DataContextChanged += (_, _) => LoadOrder();
        Unloaded += (_, _) => StopAnimation();
    }

    private void LoadOrder()
    {
        _order = ViewModel is { } vm ? vm.TileOrder.ToList() : ControlCenterTiles.DefaultOrder.ToList();
        ArrangeTiles(animate: false);
    }

    private void TilesHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _slotWidth = Math.Max(0, (e.NewSize.Width - Gap * (_tiles.Count - 1)) / _tiles.Count);
        foreach (var tile in _tiles)
            tile.Element.Width = _slotWidth;

        if (_order.Count == 0)
            LoadOrder();
        else
            ArrangeTiles(animate: false);
    }

    private double SlotX(int index) => index * (_slotWidth + Gap);

    // Sürüklenen döşeme dışındakiler sıradaki yuvalarına kayar.
    private void ArrangeTiles(bool animate)
    {
        foreach (var tile in _tiles)
        {
            var index = _order.IndexOf(tile.Id);
            if (index < 0 || tile == _dragging)
                continue;

            tile.X.Target = SlotX(index);
            if (!animate || !Motion.Enabled)
                tile.X.SnapTo(SlotX(index));
        }

        ApplyTransforms();
        if (animate && Motion.Enabled)
            WakeAnimation();
    }

    private void ApplyTransforms()
    {
        foreach (var tile in _tiles)
            tile.Transform.X = tile.X.Value;
    }

    private Tile? TileAt(DependencyObject? source)
    {
        while (source is not null)
        {
            var tile = _tiles.FirstOrDefault(t => ReferenceEquals(t.Element, source));
            if (tile is not null)
                return tile;
            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void TilesHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = TileAt(e.OriginalSource as DependencyObject);
        _pressX = e.GetPosition(TilesHost).X;
    }

    private void TilesHost_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressed is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var x = e.GetPosition(TilesHost).X;
        if (_dragging is null)
        {
            // Küçük kıpırdamalar tıklamadır; eşik aşılınca sürükleme başlar ve tıklama iptal olur.
            if (Math.Abs(x - _pressX) < SystemParameters.MinimumHorizontalDragDistance)
                return;

            _dragging = _pressed;
            _grabOffset = _pressX - _dragging.Transform.X;
            Panel.SetZIndex(_dragging.Element, 10);
            TilesHost.CaptureMouse();
        }

        // Sürüklenen döşeme imleci izler (yaysız); diğerleri yeni sıraya göre kayar.
        var left = Math.Clamp(x - _grabOffset, 0, SlotX(_tiles.Count - 1));
        _dragging.X.SnapTo(left);
        _dragging.X.Target = left;

        var slot = ControlCenterTiles.SlotAt(left + _slotWidth / 2, _slotWidth, Gap, _tiles.Count);
        if (_order.IndexOf(_dragging.Id) != slot)
            _order = ControlCenterTiles.Move(_order, _dragging.Id, slot);

        ArrangeTiles(animate: true);
        ApplyTransforms();
        e.Handled = true;
    }

    private void TilesHost_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var dragged = _dragging;
        _pressed = null;
        if (dragged is null)
            return;

        EndDrag();
        e.Handled = true;
    }

    private void TilesHost_LostMouseCapture(object sender, MouseEventArgs e)
    {
        // Yakalama başka yoldan kaybolduysa (ör. pencere odağı) sürükleme sonlandırılır, sıra yine de kaydedilir.
        if (_dragging is not null)
            EndDrag();
    }

    private void EndDrag()
    {
        var dragged = _dragging;
        _dragging = null;
        _pressed = null;
        if (dragged is null)
            return;

        if (TilesHost.IsMouseCaptured)
            TilesHost.ReleaseMouseCapture();

        // Bırakılan döşeme kendi yuvasına oturur, sonra sıra ayara yazılır.
        dragged.X.Target = SlotX(_order.IndexOf(dragged.Id));
        if (!Motion.Enabled)
            dragged.X.SnapTo(dragged.X.Target);
        Panel.SetZIndex(dragged.Element, 0);
        ArrangeTiles(animate: true);

        ViewModel?.SaveTileOrder(_order);
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

        var moving = false;
        foreach (var tile in _tiles)
        {
            if (tile == _dragging)
                continue;
            moving |= tile.X.Step(dt);
        }

        ApplyTransforms();
        if (!moving && _dragging is null)
            StopAnimation();
    }

    // Kalın çubuklar yalnız kullanıcı değiştirince olay verir; bağlamadan gelen değer servise geri yazılmaz.
    private void VolumeSlider_UserValueChanged(object? sender, double value) => ViewModel?.OnVolumeSliderChanged(value);

    private void VolumeSlider_IconClicked(object? sender, EventArgs e) => ViewModel?.ToggleMuteCommand.Execute(null);

    private void BrightnessSlider_UserValueChanged(object? sender, double value) => ViewModel?.OnBrightnessSliderChanged(value);
}
