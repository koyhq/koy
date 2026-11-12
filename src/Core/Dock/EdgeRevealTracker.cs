using Koy.Core.Shell;

namespace Koy.Core.Dock;

/// <summary>
/// Alt kenardan dock açma kararı (saf mantık). Fare kenarda kesintisiz <see cref="Delay"/> kadar kalınca "silahlanır";
/// kenardan ayrılınca ya da bir fare tuşu basılıyken (pencere sürükleme/boyutlandırma, seçim) sıfırlanır.
/// Zaman dışarıdan verilir; kare/zamanlayıcı aralığından bağımsız ve testlenebilir.
/// </summary>
public sealed class EdgeRevealTracker(TimeSpan delay)
{
    /// <summary>Varsayılan bekleme: kasıtlı bir niyetle yanlışlıkla geçişi ayırır.</summary>
    public static readonly TimeSpan DefaultDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>Kenar bölgesi (piksel): fare gizliyken bu kadar yakınsa kenarda sayılır.</summary>
    public const int EdgeZonePx = 4;

    /// <summary>Dock açıkken fare bu kadar (DIP) yukarıya kadar "dock bölgesi" sayılır; kenardan dock'a yürürken kapanmaz.</summary>
    public const double RevealedZoneDip = 80.0;

    private TimeSpan? _since;

    public TimeSpan Delay { get; } = delay;

    /// <summary>Şu an silahlı mı (dock açılmalı mı).</summary>
    public bool IsArmed { get; private set; }

    /// <param name="atEdge">Fare bu monitörün alt kenar bölgesinde mi.</param>
    /// <param name="buttonPressed">Herhangi bir fare tuşu basılı mı.</param>
    /// <param name="now">Tekdüze artan zaman (örn. Environment.TickCount64).</param>
    public bool Update(bool atEdge, bool buttonPressed, TimeSpan now)
    {
        if (!atEdge || buttonPressed)
        {
            _since = null;
            IsArmed = false;
            return false;
        }

        _since ??= now;
        if (now - _since.Value >= Delay)
            IsArmed = true;

        return IsArmed;
    }

    public void Reset()
    {
        _since = null;
        IsArmed = false;
    }

    /// <summary>
    /// Fare bu monitörün kenar bölgesinde mi. Gizliyken ince bir şerit, açıkken (dock'a ulaşmak için) daha yüksek bir bölge.
    /// Koordinatlar fiziksel pikseldir; <paramref name="dpiScaleY"/> DIP'ten piksele çevirir.
    /// </summary>
    public static bool IsInZone(int x, int y, int left, int right, int bottom, bool revealed, double dpiScaleY)
    {
        if (x < left || x >= right)
            return false;

        var zone = revealed ? (int)Math.Ceiling(RevealedZoneDip * Math.Max(dpiScaleY, 1.0)) : EdgeZonePx;
        return y >= bottom - zone && y <= bottom;
    }

    /// <summary>
    /// Fare bu monitörün seçili kenar bölgesinde mi (Bottom, Top, Left, Right).
    /// </summary>
    public static bool IsInZone(
        DockEdge edge,
        int x,
        int y,
        WindowRect monitorBounds,
        bool revealed,
        double dpiScaleX,
        double dpiScaleY)
    {
        if (!Enum.IsDefined(edge)) edge = DockEdge.Bottom;

        switch (edge)
        {
            case DockEdge.Top:
            {
                if (x < monitorBounds.Left || x >= monitorBounds.Right)
                    return false;
                var zone = revealed ? (int)Math.Ceiling(RevealedZoneDip * Math.Max(dpiScaleY, 1.0)) : EdgeZonePx;
                return y >= monitorBounds.Top && y <= monitorBounds.Top + zone;
            }
            case DockEdge.Left:
            {
                if (y < monitorBounds.Top || y >= monitorBounds.Bottom)
                    return false;
                var zone = revealed ? (int)Math.Ceiling(RevealedZoneDip * Math.Max(dpiScaleX, 1.0)) : EdgeZonePx;
                return x >= monitorBounds.Left && x <= monitorBounds.Left + zone;
            }
            case DockEdge.Right:
            {
                if (y < monitorBounds.Top || y >= monitorBounds.Bottom)
                    return false;
                var zone = revealed ? (int)Math.Ceiling(RevealedZoneDip * Math.Max(dpiScaleX, 1.0)) : EdgeZonePx;
                return x >= monitorBounds.Right - zone && x <= monitorBounds.Right;
            }
            case DockEdge.Bottom:
            default:
                return IsInZone(x, y, monitorBounds.Left, monitorBounds.Right, monitorBounds.Bottom, revealed, dpiScaleY);
        }
    }
}
