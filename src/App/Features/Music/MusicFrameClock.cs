using System.Windows.Media;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Müzik sayfasındaki animasyonların (akan kapak rengi, plak) tek ortak saati. Her görünüm kendi
/// <c>CompositionTarget.Rendering</c> aboneliğiyle koşarsa pencere her birinin ritminde ayrı sunulur; burada tek
/// abonelik var ve görünümler 30 Hz ızgaranın bölenlerine (<see cref="FrameGrid"/>) oturur, kareler aynı vuruşta toplanır.
/// Abonelik yalnız en az bir görünüm hareket isterken bağlıdır (AGENTS #6: hareket yokken kare dinlenmez).
/// Yalnız arayüz iş parçacığında kullanılır.
/// </summary>
internal static class MusicFrameClock
{
    private sealed class Subscription(int divisor, Action<double> tick)
    {
        public int Divisor { get; } = divisor;
        public Action<double> Tick { get; } = tick;
        public TimeSpan Last { get; set; } = TimeSpan.Zero;
        public bool Active { get; set; } = true;
    }

    private static readonly List<Subscription> Subscribers = [];
    private static Subscription[] _snapshot = [];
    private static bool _attached;
    private static long _lastTick = -1;

    /// <summary>
    /// <paramref name="fps"/> hızına en yakın bölenle abone ol; <paramref name="tick"/> iki çizim arası geçen süreyi (sn) alır.
    /// Dönen nesne <see cref="Unsubscribe"/> ile bırakılır.
    /// </summary>
    public static object Subscribe(double fps, Action<double> tick)
    {
        var sub = new Subscription(FrameGrid.DivisorFor(fps), tick);
        Subscribers.Add(sub);
        _snapshot = [.. Subscribers];
        if (!_attached)
        {
            _attached = true;
            _lastTick = -1;
            CompositionTarget.Rendering += OnRendering;
        }

        return sub;
    }

    public static void Unsubscribe(object? handle)
    {
        if (handle is not Subscription sub || !Subscribers.Remove(sub))
            return;

        sub.Active = false; // geçerli turdaki kopyada kalsa da bir daha çağrılmaz
        _snapshot = [.. Subscribers];
        if (Subscribers.Count == 0 && _attached)
        {
            CompositionTarget.Rendering -= OnRendering;
            _attached = false;
        }
    }

    private static void OnRendering(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        var tick = FrameGrid.TickAt(now.TotalSeconds);
        if (tick == _lastTick)
            return; // aynı vuruşta ikinci Rendering (60 Hz ekranda iki karede bir vuruş ilerler)

        var previous = _lastTick < 0 ? tick - 1 : _lastTick;
        _lastTick = tick;

        // Yeni abone ilk vuruşta hemen çizer (ilk dt = ızgara süresi); yoksa bölenine göre.
        foreach (var sub in _snapshot)
        {
            if (!sub.Active)
                continue;

            var first = sub.Last == TimeSpan.Zero;
            if (!first && !FrameGrid.IsDue(previous, tick, sub.Divisor))
                continue;

            var dt = first ? FrameGrid.PeriodSeconds(sub.Divisor) : (now - sub.Last).TotalSeconds;
            sub.Last = now;
            if (dt >= 0)
                sub.Tick(dt);
        }
    }
}
