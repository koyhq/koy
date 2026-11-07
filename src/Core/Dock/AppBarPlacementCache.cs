using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// AppBar SETPOS tekrarını eler. Kurallar: ilk kayıtta HER ZAMAN gönderilir; zorlanınca gönderilir; alt kenar Koy'un
/// değilken (görev çubuğu görünür) Gezgin'in durumu değişmiş olabileceğinden gönderilir; yalnız alt kenar Koy'undayken
/// ve istenen alan en son uygulananla aynıysa atlanır. Sahiplik değişince önbellek temizlenir.
/// </summary>
public sealed class AppBarPlacementCache
{
    private readonly Dictionary<nint, WindowRect> _last = [];

    public bool ShouldSend(nint window, WindowRect requested, bool ownsBottomEdge, bool force)
    {
        if (force || !ownsBottomEdge)
            return true;

        return !_last.TryGetValue(window, out var last) || last != requested;
    }

    public void Record(nint window, WindowRect requested) => _last[window] = requested;

    public void Forget(nint window) => _last.Remove(window);

    public void Clear() => _last.Clear();
}
