namespace Koy.Core.Undo;

/// <summary>Geri alınabilir silmenin türü; mesajı App seçer (Core metin bilmez).</summary>
public enum UndoKind
{
    ClipboardCleared,
    ClipboardItemRemoved,
    ShelfCleared,
    ShelfItemRemoved,
    EventDeleted,
    TaskDeleted
}

/// <summary>Bekleyen tek geri alma kaydı. <paramref name="Count"/> silinen öğe sayısıdır (mesajda kullanılabilir).</summary>
public sealed record UndoEntry(UndoKind Kind, int Count, DateTimeOffset ExpiresAt, Action Undo);

/// <summary>
/// Onaysız silmenin güvenlik ağı (Burhan kararı, 2026-11-21): silme hemen uygulanır, kısa bir süre "Geri al" kalır.
/// Tek yuvalıdır: yeni silme eskisinin geri alınabilirliğini bitirir (eski silme kalıcı kalır). Saf mantık: zamanlayıcı
/// App'tedir ve çentik görünmezken durur; süre dolumu <see cref="ExpireIfDue"/> ile denetlenir, saat <see cref="TimeProvider"/>'dan gelir.
/// Silmeler zaten veri kaynağında uygulanmış olur; geri alma eylemi öğeleri aynı kimlikle yerine koyar (dosya silinmez:
/// Raf yalnız yol tutar).
/// </summary>
public sealed class UndoBuffer
{
    /// <summary>"Geri al" seçeneğinin kalma süresi.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _time;
    private readonly TimeSpan _window;

    public UndoBuffer(TimeProvider? time = null, TimeSpan? window = null)
    {
        _time = time ?? TimeProvider.System;
        _window = window ?? DefaultWindow;
    }

    /// <summary>Geri alınabilir kayıt; yoksa null.</summary>
    public UndoEntry? Current { get; private set; }

    /// <summary>Kayıt eklendi, geri alındı, doldu ya da temizlendi.</summary>
    public event EventHandler? Changed;

    /// <summary>Kalan süre; kayıt yoksa ya da dolduysa sıfır.</summary>
    public TimeSpan Remaining =>
        Current is { } entry ? Max(entry.ExpiresAt - _time.GetUtcNow(), TimeSpan.Zero) : TimeSpan.Zero;

    /// <summary>
    /// Silmeyi kaydeder (önceki kayıt düşer). Silinen yoksa (<paramref name="count"/> 0 ya da eksi) kayıt açılmaz.
    /// </summary>
    public void Push(UndoKind kind, int count, Action undo)
    {
        ArgumentNullException.ThrowIfNull(undo);
        if (count <= 0)
            return;

        Current = new UndoEntry(kind, count, _time.GetUtcNow() + _window, undo);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Geri alır. Kayıt yoksa ya da süresi dolmuşsa false; dolmuşsa kayıt temizlenir. Eylem bir kez çalışır.</summary>
    public bool TryUndo()
    {
        var entry = Current;
        if (entry is null)
            return false;

        Current = null;
        var live = _time.GetUtcNow() < entry.ExpiresAt;
        try
        {
            if (live)
                entry.Undo();
        }
        finally
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return live;
    }

    /// <summary>Süresi dolduysa kaydı bırakır; bıraktıysa true.</summary>
    public bool ExpireIfDue()
    {
        if (Current is not { } entry || _time.GetUtcNow() < entry.ExpiresAt)
            return false;

        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Kaydı hemen bırakır (çentik kapandı ya da gizlendi: zamanlayıcı da durur).</summary>
    public void Clear()
    {
        if (Current is null)
            return;

        Current = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
