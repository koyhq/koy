namespace Koy.Core.Notifications;

/// <summary>Windows'un bildirim erişim izni. Core'da WinRT tipi görünmez (AGENTS #11).</summary>
public enum NotificationAccessStatus
{
    /// <summary>Kullanıcı henüz sorulmadı; istek Windows'un izin diyaloğunu açar.</summary>
    Unspecified,
    Allowed,
    Denied,
    /// <summary>Okunamadı (API yok ya da hata): izin yokmuş gibi davranılır.</summary>
    Unavailable
}

/// <summary>Aksiyon merkezindeki bir Windows bildirimi. <paramref name="Icon"/> kodlanmış resim ya da null (simgesiz uygulama).</summary>
/// <param name="Id">Windows'un verdiği artan kimlik; yeni gelen bildirim bununla ayırt edilir.</param>
public sealed record NotificationItem(
    uint Id,
    string AppName,
    string Title,
    string Body,
    DateTimeOffset Time,
    byte[]? Icon = null);

/// <summary>
/// Windows bildirimlerini okuyan kaynak. Paketsiz süreçte olay yok (UserNotificationListener.NotificationChanged
/// 0x80070490 verir), bu yüzden uygulama yoklama ile çalışır: <see cref="NotificationArrived"/> arka plan
/// iş parçacığından gelir. İlk okumada var olan bildirimler "yeni" sayılmaz.
/// </summary>
public interface INotificationListener : IDisposable
{
    NotificationAccessStatus AccessStatus { get; }

    /// <summary>Durum <see cref="NotificationAccessStatus.Unspecified"/> ise Windows izin diyaloğunu açar; yeni durumu döner.</summary>
    Task<NotificationAccessStatus> RequestAccessAsync();

    event Action<NotificationItem>? NotificationArrived;

    /// <summary>Yoklamayı başlatır. İzin <see cref="NotificationAccessStatus.Allowed"/> değilse çağrılmaz.</summary>
    void Start();

    /// <summary>Yoklamayı durdurur ve zamanlayıcıyı söker (AGENTS #5).</summary>
    void Stop();
}
