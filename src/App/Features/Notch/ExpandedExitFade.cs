using System.Windows;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Appearance;

namespace Koy.App.Features.Notch;

/// <summary>
/// Çentik kapanırken açık içeriğin kısa solması (issue #182, ~120 ms). Açık görünüm normalde durum Expanded'dan
/// çıkar çıkmaz stil tetikleyicisiyle gizlenir; burada durum değişmeden önce yerel bir Visible değeriyle sabitlenir
/// (yerel değer stil tetikleyicisinin önüne geçer), saydamlığı sıfıra iner, sonra yerel değer silinir ve stil yeniden
/// karar verir. Tıklama solma boyunca geçmez.
/// Şekil içeriği kendi sınırına kırptığı için (NotchShape) solan içerik çentiğin dışına taşmaz.
/// </summary>
public sealed class ExpandedExitFade(FrameworkElement view)
{
    private bool _rendering;
    private TimeSpan _lastFrame;
    private double _elapsed;
    private double _timeScale = 1;

    private bool _pinned;

    public bool IsRunning => _rendering;

    /// <summary>
    /// Durum değişmeden hemen önce (PropertyChanging) çağrılır: görünüm o an açıksa yerel Visible değeriyle sabitlenir,
    /// böylece stil tetikleyicisi onu gizleyemez ve içerik bir kare bile kaybolmaz.
    /// </summary>
    public void Pin()
    {
        if (view.Visibility != Visibility.Visible || Motion.ScaleFor(MotionPolicy.MotionKind.NotchOpenClose) <= 0)
            return;

        view.Visibility = Visibility.Visible;
        _pinned = true;
    }

    /// <summary>Durum değiştikten sonra: hâlâ açıksa sabitleme kalkar, kapanıyorsa solma başlar.</summary>
    public void OnStateChanged(bool stillExpanded)
    {
        if (stillExpanded || !_pinned)
        {
            Cancel();
            return;
        }

        _timeScale = 1.0 / Math.Max(Motion.ScaleFor(MotionPolicy.MotionKind.NotchOpenClose), 0.01);
        _elapsed = 0;
        view.IsHitTestVisible = false;
        if (_rendering)
            return;

        _rendering = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Solmayı keser ve görünümü stile bırakır (çentik yeniden açıldı ya da pencere kapanıyor).</summary>
    public void Cancel()
    {
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }

        _pinned = false;
        view.ClearValue(UIElement.VisibilityProperty);
        view.ClearValue(UIElement.IsHitTestVisibleProperty);
        view.Opacity = 1;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt <= 0)
            return;

        _elapsed += dt * _timeScale;
        view.Opacity = StaggeredEntrance.ExitOpacityAt(_elapsed);
        if (_elapsed >= StaggeredEntrance.ExitSeconds)
            Cancel();
    }
}
