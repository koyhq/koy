using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Koy.App.Resources;
using Koy.Core.Appearance;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Müzik sayfasındaki parça değişimi geçişini çizer (issue #182). Hesap <see cref="TrackTransition"/>'da;
/// bu sınıf yalnız değerleri öğelere yazar. Kare yalnız geçiş sürerken dinlenir (AGENTS #6), müzik yüzü
/// görünmüyorsa ya da sekme geçişleri kapalıysa hiç başlamaz, yeni kapak anında yerinde olur.
/// Giden kapak, değişimden önce ekranda olan resmin kendisidir (dondurulmuş, kopyalanmaz) ve geçiş bitince bırakılır.
/// </summary>
public sealed class TrackTransitionPresenter
{
    private readonly FrameworkElement _face;
    private readonly FrameworkElement _incoming;
    private readonly Rectangle _outgoing;
    private readonly FrameworkElement _title;

    private readonly TranslateTransform _incomingMove = new();
    private readonly ScaleTransform _incomingScale = new();
    private readonly TranslateTransform _outgoingMove = new();
    private readonly ScaleTransform _outgoingScale = new();
    private readonly TranslateTransform _titleMove = new();

    private readonly TrackTransition _transition = new();
    private MusicViewModel? _music;

    // Ekranda olan kapak ve ondan önceki; başlık değişimiyle aynı güncellemede kapak da değiştiyse giden kapak öncekidir.
    private ImageSource? _shownCover;
    private ImageSource? _previousCover;
    private string? _coverChangedWithTitle;

    private bool _rendering;
    private TimeSpan _lastFrame;
    private double _timeScale = 1;

    public TrackTransitionPresenter(FrameworkElement face, FrameworkElement incoming, Rectangle outgoing, FrameworkElement title)
    {
        _face = face;
        _incoming = incoming;
        _outgoing = outgoing;
        _title = title;

        _incoming.RenderTransform = new TransformGroup { Children = { _incomingScale, _incomingMove } };
        _outgoing.RenderTransform = new TransformGroup { Children = { _outgoingScale, _outgoingMove } };
        _title.RenderTransform = _titleMove;

        // Yüz gizlenirse (çentik kapandı, Ana'ya geçildi) yarım kalan geçiş bitirilir; kare dinlemesi bırakılır.
        _face.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is false)
                Finish();
        };
    }

    public void Attach(MusicViewModel? music)
    {
        if (ReferenceEquals(_music, music))
            return;

        if (_music is not null)
        {
            _music.PropertyChanged -= OnMusicPropertyChanged;
            _music.TrackChanged -= OnTrackChanged;
        }

        Finish();
        _music = music;
        _shownCover = music?.Thumbnail;
        _previousCover = null;
        _coverChangedWithTitle = null;

        if (music is not null)
        {
            music.PropertyChanged += OnMusicPropertyChanged;
            music.TrackChanged += OnTrackChanged;
        }
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MusicViewModel.Thumbnail) || _music is null)
            return;

        _previousCover = _shownCover;
        _shownCover = _music.Thumbnail;
        _coverChangedWithTitle = _music.Title;
    }

    private void OnTrackChanged()
    {
        if (_music is null)
            return;

        // Kapak bu güncellemede değiştiyse ekrandaki eskisi _previousCover'dır; değişmediyse (kapak sonra gelecek)
        // ekrandaki hâlâ eski parçanınkidir.
        var outgoing = _coverChangedWithTitle == _music.Title ? _previousCover : _shownCover;
        _coverChangedWithTitle = null;

        var scale = Motion.ScaleFor(MotionPolicy.MotionKind.TabTransitions);
        if (!_face.IsVisible || scale <= 0)
        {
            Finish();
            return;
        }

        _timeScale = 1.0 / scale;
        ShowOutgoing(outgoing);
        _transition.Start();
        Apply();
        if (!_rendering)
        {
            _rendering = true;
            _lastFrame = TimeSpan.Zero;
            CompositionTarget.Rendering += OnRendering;
        }
    }

    private void ShowOutgoing(ImageSource? cover)
    {
        if (cover is null)
        {
            _outgoing.Fill = null;
            _outgoing.Visibility = Visibility.Collapsed;
            return;
        }

        // Yatay (video) kapak sayfada bütünüyle gösterilir; kare kapak çerçeveyi doldurur.
        var wide = cover is BitmapSource b && b.PixelHeight > 0 && b.PixelWidth > b.PixelHeight * 1.15;
        var brush = new ImageBrush(cover) { Stretch = wide ? Stretch.Uniform : Stretch.UniformToFill };
        brush.Freeze();
        _outgoing.Fill = brush;
        _outgoing.Visibility = Visibility.Visible;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt <= 0)
            return;

        var moving = _transition.Step(dt * _timeScale);
        Apply();
        if (!moving)
            Finish();
    }

    /// <summary>
    /// Geçişin tek bir karesini çizer, kare dinlemez. Yalnız görsel kanıt aracı (tools/Koy.UiSnapshots) kullanır;
    /// sonraki gerçek geçiş ya da yüzün gizlenmesi durumu sıfırlar.
    /// </summary>
    public void ShowFrame(ImageSource? outgoing, double progress)
    {
        ShowOutgoing(outgoing);
        Apply(TrackTransition.FrameAt(progress, CoverWidth));
    }

    private double CoverWidth => _incoming.ActualWidth > 0 ? _incoming.ActualWidth : 200;

    private void Apply() => Apply(_transition.Frame(CoverWidth));

    private void Apply(TrackTransitionFrame f)
    {

        _incomingMove.X = f.IncomingOffsetX;
        _incomingScale.ScaleX = _incomingScale.ScaleY = f.IncomingScale;
        _incoming.Opacity = f.IncomingOpacity;

        _outgoingMove.X = f.OutgoingOffsetX;
        _outgoingScale.ScaleX = _outgoingScale.ScaleY = f.OutgoingScale;
        _outgoing.Opacity = f.OutgoingOpacity;

        _titleMove.Y = f.TitleOffsetY;
        _title.Opacity = f.TitleOpacity;
    }

    private void Finish()
    {
        _transition.Finish();
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }

        Apply();
        // Eski kapak bırakılır: geçiş dışında giden katman boş durur.
        _outgoing.Fill = null;
        _outgoing.Visibility = Visibility.Collapsed;
    }
}
