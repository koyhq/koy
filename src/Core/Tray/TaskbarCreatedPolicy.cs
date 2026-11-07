namespace Koy.Core.Tray;

/// <summary>
/// "TaskbarCreated" yayını Gezgin yeniden başlayınca gelir. Koy'un tepsi penceresi de simgeleri yeniden eklettirmek için
/// aynı mesajı kendisi yayınlar (Gezgin'in tepsisi öne geçtikçe, en sık 5 sn'de bir) ve yayın Koy'un kendi pencerelerine
/// de ulaşır. Kendi yayınımız "Gezgin yeniden başladı" sanılırsa dock görev çubuğunu yeniden gizler ve AppBar kaydını
/// silip yeniden yapar; çalışma alanı her seferinde oynar, büyütülmüş pencereler 5 sn'de bir yeniden boyutlanırdı.
/// Ayrım yalnız zamana değil kimliğe de bakar: gerçek yeniden başlamada Gezgin'in Shell_TrayWnd tutamacı değişir.
/// </summary>
public static class TaskbarCreatedPolicy
{
    /// <summary>Kendi yayınımızın bize ulaşması için tanınan süre.</summary>
    public static readonly TimeSpan OwnBroadcastWindow = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Gelen yayın bizim son yayınımızın yankısı mı. Yankı: süre içinde VE Gezgin'in tepsi tutamacı yayın anındakiyle
    /// aynı. Tutamaç değiştiyse (ya da bilinmiyorsa / Gezgin'in tepsisi şu an yoksa) yayın kendi yayınımızla çakışsa bile
    /// gerçek bir yeniden başlamadır.
    /// </summary>
    /// <param name="explorerTrayAtBroadcast">Yayın anında Gezgin'in Shell_TrayWnd tutamacı (0 = bilinmiyor).</param>
    /// <param name="explorerTrayNow">Şu an Gezgin'in Shell_TrayWnd tutamacı (0 = yok).</param>
    public static bool IsOwnBroadcast(DateTime lastOwnBroadcastUtc, DateTime nowUtc, nint explorerTrayAtBroadcast, nint explorerTrayNow) =>
        nowUtc - lastOwnBroadcastUtc <= OwnBroadcastWindow
        && explorerTrayAtBroadcast != 0
        && explorerTrayNow == explorerTrayAtBroadcast;
}
