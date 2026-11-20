using System.Windows;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Core.Appearance;

namespace Koy.App.Features.Notch;

/// <summary>
/// Bir sayfanın öğelerini sırayla içeri alır (issue #182, <see cref="StaggeredEntrance"/>). Öğeler sıra numarasıyla
/// verilir; aynı sırada birden çok öğe olabilir (müzik sayfasında dalga ve sözler aynı yerde durur).
/// Her öğeye kendi ölçek + kayma dönüşümü verilir, başka bir dönüşümü olan öğe verilmez. Kare yalnız giriş sürerken
/// dinlenir (AGENTS #6); açılış/kapanış animasyonu kapalıysa öğeler anında yerinde olur.
/// </summary>
public sealed class StaggeredEntrancePresenter
{
    private readonly (FrameworkElement Element, int Index, ScaleTransform Scale, TranslateTransform Move)[] _items;
    private readonly StaggeredEntrance _entrance;

    private bool _rendering;
    private TimeSpan _lastFrame;
    private double _elapsed;
    private double _timeScale = 1;

    public StaggeredEntrancePresenter(params (FrameworkElement Element, int Index)[] items)
    {
        _items = new (FrameworkElement, int, ScaleTransform, TranslateTransform)[items.Length];
        var count = 0;
        for (var i = 0; i < items.Length; i++)
        {
            var (element, index) = items[i];
            var scale = new ScaleTransform();
            var move = new TranslateTransform();
            element.RenderTransform = new TransformGroup { Children = { scale, move } };
            element.RenderTransformOrigin = new Point(0.5, 0.5);
            _items[i] = (element, index, scale, move);
            count = Math.Max(count, index + 1);
        }

        _entrance = new StaggeredEntrance(count);
    }

    /// <summary>Girişi baştan oynatır.</summary>
    public void Play()
    {
        var scale = Motion.ScaleFor(MotionPolicy.MotionKind.NotchOpenClose);
        if (scale <= 0)
        {
            Finish();
            return;
        }

        _timeScale = 1.0 / scale;
        _elapsed = 0;
        Apply();
        if (_rendering)
            return;

        _rendering = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Girişi anında bitirir: öğeler yerinde ve tam görünür.</summary>
    public void Finish()
    {
        _elapsed = _entrance.TotalSeconds;
        if (_rendering)
        {
            CompositionTarget.Rendering -= OnRendering;
            _rendering = false;
        }
        Apply();
    }

    /// <summary>Girişin <paramref name="elapsed"/> saniyedeki karesini çizer, kare dinlemez. Yalnız görsel kanıt aracı
    /// (tools/Koy.UiSnapshots) kullanır.</summary>
    public void ShowFrame(double elapsed)
    {
        _elapsed = elapsed;
        Apply();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var dt = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt <= 0)
            return;

        _elapsed += dt * _timeScale;
        if (_entrance.IsDone(_elapsed))
        {
            Finish();
            return;
        }
        Apply();
    }

    private void Apply()
    {
        foreach (var (element, index, scale, move) in _items)
        {
            var f = _entrance.FrameAt(index, _elapsed);
            element.Opacity = f.Opacity;
            scale.ScaleX = scale.ScaleY = f.Scale;
            move.Y = f.OffsetY;
        }
    }
}
