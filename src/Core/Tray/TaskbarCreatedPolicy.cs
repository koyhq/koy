namespace Koy.Core.Tray;

/// <summary>
/// "TaskbarCreated" yayını Gezgin yeniden başlayınca gelir. Koy'un tepsi penceresi de simgeleri yeniden eklettirmek için
/// aynı mesajı kendisi yayınlar (Gezgin'in tepsisi öne geçtikçe, en sık 5 sn'de bir) ve yayın Koy'un kendi pencerelerine
/// de ulaşır. Kendi yayınımız "Gezgin yeniden başladı" sanılırsa dock görev çubuğunu yeniden gizler ve AppBar kaydını
/// silip yeniden yapar; çalışma alanı her seferinde oynar, büyütülmüş pencereler 5 sn'de bir yeniden boyutlanırdı.
/// </summary>
public static class TaskbarCreatedPolicy
{
    /// <summary>Kendi yayınımızın bize ulaşması için tanınan süre.</summary>
    public static readonly TimeSpan OwnBroadcastWindow = TimeSpan.FromSeconds(3);

    /// <summary>Gelen yayın bizim son yayınımızın yankısı mı.</summary>
    public static bool IsOwnBroadcast(DateTime lastOwnBroadcastUtc, DateTime nowUtc) =>
        nowUtc - lastOwnBroadcastUtc <= OwnBroadcastWindow;
}
