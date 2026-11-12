using Koy.Core.Settings;

namespace Koy.Core.Input;

/// <summary>
/// Bir köşeye bağlanabilen eylem. Yalnız Koy'un bugün zaten yapabildiği şeyler; None = köşe kapalı.
/// Ayar dosyasında ad olarak yazılır: mevcut adlar değiştirilmez.
/// </summary>
public enum CornerAction
{
    None,
    OpenNotch,
    OpenSearch,
    OpenKoyWindow,
    ToggleDock
}

/// <summary>Köşe ayarının saf kararları: hangi köşe hangi eylemi çalıştırır, takip gerekli mi, süre sınırları.</summary>
public static class CornerActions
{
    public const int DefaultDwellMs = 400;
    public const int MinDwellMs = 150;
    public const int MaxDwellMs = 2000;

    /// <summary>Köşe bölgesinin boyu (fiziksel piksel). İmleç ekran kenarına dayandığı için küçük bir kare yeter.</summary>
    public const int CornerSizePx = 6;

    /// <summary>
    /// Köşe yoklamasının aralığı (ms). Fare kancası yok; yoklama yalnız en az bir köşe bağlıyken çalışır.
    /// 100 ms'nin altına inilmez.
    /// </summary>
    public const int PollIntervalMs = 120;

    public static int ClampDwell(int dwellMs) => Math.Clamp(dwellMs, MinDwellMs, MaxDwellMs);

    public static CornerAction ActionFor(HotCornersSettings settings, ScreenCorner corner) => corner switch
    {
        ScreenCorner.TopLeft => settings.TopLeft,
        ScreenCorner.TopRight => settings.TopRight,
        ScreenCorner.BottomLeft => settings.BottomLeft,
        _ => settings.BottomRight
    };

    public static HotCornersSettings With(HotCornersSettings settings, ScreenCorner corner, CornerAction action) => corner switch
    {
        ScreenCorner.TopLeft => settings with { TopLeft = action },
        ScreenCorner.TopRight => settings with { TopRight = action },
        ScreenCorner.BottomLeft => settings with { BottomLeft = action },
        _ => settings with { BottomRight = action }
    };

    /// <summary>Çalışacak <see cref="InputAction"/>; köşe kapalı ya da değer tanımsızsa null.</summary>
    public static InputAction? ToInputAction(CornerAction action) => action switch
    {
        CornerAction.OpenNotch => InputAction.OpenNotch,
        CornerAction.OpenSearch => InputAction.OpenSearch,
        CornerAction.OpenKoyWindow => InputAction.OpenKoyWindow,
        CornerAction.ToggleDock => InputAction.ToggleDock,
        _ => null
    };

    /// <summary>Takip gerekli mi: en az bir köşede geçerli bir eylem var. Hepsi None ise hiçbir şey çalışmamalı.</summary>
    public static bool IsActive(HotCornersSettings settings) =>
        Enum.GetValues<ScreenCorner>().Any(c => ToInputAction(ActionFor(settings, c)) is not null);

    /// <summary>Ayardaki köşeleri ortak bağ modeline çevirir (<see cref="InputBinding"/>); kapalı köşeler girmez.</summary>
    public static IReadOnlyList<InputBinding> ToBindings(HotCornersSettings settings)
    {
        var dwell = ClampDwell(settings.DwellMs);
        var result = new List<InputBinding>();
        foreach (var corner in Enum.GetValues<ScreenCorner>())
        {
            if (ToInputAction(ActionFor(settings, corner)) is { } action)
                result.Add(new InputBinding(action, new HotCornerTrigger(corner, dwell, InputBindingParser.DefaultPushBackPx)));
        }

        return result;
    }
}

/// <summary>
/// Köşe bekleme durumu (saf mantık). İmleç aynı köşede kesintisiz <c>dwellMs</c> kalınca bir kez tetikler;
/// bir daha tetiklenmek için köşeden çıkıp yeniden girmek gerekir (köşede dururken eylem tekrarlanmaz).
/// Fare tuşu basılıyken (sürükleme) ya da bastırılmışken (tam ekran) sıfırlanır. Zaman dışarıdan verilir.
/// </summary>
public sealed class HotCornerTracker
{
    private ScreenCorner? _current;
    private long _enteredAtMs;
    private bool _fired;

    /// <param name="corner">İmlecin bulunduğu köşe; köşede değilse null.</param>
    /// <param name="suppressed">Tuş basılı ya da tam ekran gibi nedenlerle tetikleme yasak.</param>
    /// <param name="nowMs">Tekdüze artan zaman (örn. Environment.TickCount64).</param>
    /// <returns>Bekleme bu çağrıda dolduysa köşe; aksi halde null.</returns>
    public ScreenCorner? Update(ScreenCorner? corner, bool suppressed, long nowMs, int dwellMs)
    {
        if (corner is null || suppressed)
        {
            Reset();
            return null;
        }

        if (_current != corner)
        {
            _current = corner;
            _enteredAtMs = nowMs;
            _fired = false;
        }

        if (_fired || !HotCornerMath.IsDwellComplete(_enteredAtMs, nowMs, ClampDwellFor(dwellMs)))
            return null;

        _fired = true;
        return corner;
    }

    public void Reset()
    {
        _current = null;
        _fired = false;
    }

    private static int ClampDwellFor(int dwellMs) => CornerActions.ClampDwell(dwellMs);
}
