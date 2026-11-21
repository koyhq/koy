using Koy.Core.Shell;

namespace Koy.Platform.Shell;

/// <summary>
/// Bir tüketicinin <see cref="WindowTracker"/> üzerindeki isteği. Bırakılınca (<c>Dispose</c>) yalnız bu kiranın
/// gerektirdiği, başka kimsenin istemediği kancalar sökülür. Arayüz iş parçacığından kullanılır.
/// </summary>
public sealed class WindowHookLease : IDisposable
{
    private readonly WindowTracker _tracker;
    private bool _released;

    internal WindowHookLease(WindowTracker tracker, WindowHookNeeds needs)
    {
        _tracker = tracker;
        Needs = needs;
    }

    /// <summary>Kiranın şu anki isteği.</summary>
    public WindowHookNeeds Needs { get; internal set; }

    /// <summary>İsteği değiştirir (ör. bağlam kurallarına tam ekran koşulu eklenince); kancalar buna göre kurulur ya da sökülür.</summary>
    public void Update(WindowHookNeeds needs)
    {
        if (_released || needs == Needs)
            return;
        _tracker.UpdateLease(this, needs);
    }

    public void Dispose()
    {
        if (_released)
            return;
        _released = true;
        _tracker.ReleaseLease(this);
    }
}
