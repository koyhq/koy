using Koy.Core.Diagnostics;

namespace Koy.Core.Lyrics;

public enum LyricsState
{
    Idle,
    Loading,
    Ready,
    NotFound,
    Failed
}

/// <summary>
/// Bir görünümün söz durumu: hangi parça yüklü, hangi satır etkin. Arayüz iş parçacığından çağrılır; dosya ve ağ işi
/// arka plana alınır. Parça hızlı değişince eski istek iptal edilir, geç gelen eski sonuç yeni parçanın üzerine yazmaz.
/// </summary>
public sealed class LyricsSession(ILyricsProvider provider)
{
    private CancellationTokenSource? _load;
    private string? _key;

    public LyricsState State { get; private set; }
    public IReadOnlyList<LyricLine> Lines { get; private set; } = [];
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Durum ya da satır listesi değişti (etkin satır değişimi bunu tetiklemez).</summary>
    public event Action? Changed;

    /// <summary>Parçayı yükler. Aynı parça zaten yüklü ya da yükleniyorsa bir şey yapmaz; hata sonrası yeniden dener.</summary>
    public async Task LoadAsync(LyricsQuery? query)
    {
        if (query is null)
        {
            Clear();
            return;
        }

        if (query.Key == _key && State is LyricsState.Loading or LyricsState.Ready or LyricsState.NotFound)
            return;

        CancelLoad();
        var load = new CancellationTokenSource();
        _load = load;
        _key = query.Key;
        Apply(LyricsState.Loading, []);

        LyricsLoadResult result;
        try
        {
            result = await Task.Run(() => provider.LoadAsync(query, load.Token), load.Token);
        }
        catch (OperationCanceledException) when (load.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            CrashLog.Write("Şarkı sözü yüklenemedi", ex);
            result = LyricsLoadResult.Failed;
        }

        // Bu sırada başka parça başladıysa sonuç artık geçersiz.
        if (!ReferenceEquals(_load, load))
            return;

        _load = null;
        Apply(result.Status switch
        {
            LyricsLoadStatus.Found => LyricsState.Ready,
            LyricsLoadStatus.NotFound => LyricsState.NotFound,
            _ => LyricsState.Failed
        }, result.Lines);
    }

    /// <summary>Görünüm gizlendi: süren yükleme iptal edilir, biten sonuç saklanır (geri açılınca yeniden indirilmez).</summary>
    public void Suspend()
    {
        if (State != LyricsState.Loading)
            return;

        CancelLoad();
        _key = null;
        Apply(LyricsState.Idle, []);
    }

    public void Clear()
    {
        CancelLoad();
        _key = null;
        Apply(LyricsState.Idle, []);
    }

    /// <summary>Etkin satırı konuma göre günceller. Değiştiyse true.</summary>
    public bool UpdatePosition(TimeSpan position)
    {
        var index = LyricsTimeline.IndexAt(Lines, position);
        if (index == ActiveIndex)
            return false;

        ActiveIndex = index;
        return true;
    }

    // Kaynak önce boşaltılır: iptal geri çağrıları eşzamanlı çalışabilir, bu istek artık "güncel" sayılmamalı.
    private void CancelLoad()
    {
        var load = _load;
        _load = null;
        load?.Cancel();
    }

    private void Apply(LyricsState state, IReadOnlyList<LyricLine> lines)
    {
        State = state;
        Lines = lines;
        ActiveIndex = -1;
        Changed?.Invoke();
    }
}
