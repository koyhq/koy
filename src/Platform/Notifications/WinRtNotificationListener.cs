using Koy.Core.Notifications;
using Windows.Storage.Streams;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Koy.Platform.Notifications;

/// <summary>
/// <see cref="UserNotificationListener"/> sarmalayıcısı. Paketsiz süreçte <c>NotificationChanged</c> aboneliği
/// 0x80070490 ile reddedilir (P3A denemesi), liste okuması çalışır; bu yüzden olay yok, <see cref="NotificationPoller"/>
/// ile yoklanır. Yoklama <see cref="Start"/> ile başlar, <see cref="Stop"/> ile söküldüğünde zamanlayıcı kalmaz (AGENTS #5).
/// İş thread pool'da yürür, olaylar UI iş parçacığında gelmez.
/// </summary>
public sealed class WinRtNotificationListener : INotificationListener
{
    private const int LogoSize = 40;

    private readonly NotificationPoller _poller;

    public WinRtNotificationListener()
    {
        _poller = new NotificationPoller(ReadAsync);
        _poller.Arrived += OnArrived;
    }

    public event Action<NotificationItem>? NotificationArrived;

    public NotificationAccessStatus AccessStatus => GetAccessStatus();

    /// <summary>Ayar sayfasının dinleyici kurmadan izni okuyabilmesi için statik.</summary>
    public static NotificationAccessStatus GetAccessStatus()
    {
        try
        {
            return Map(UserNotificationListener.Current.GetAccessStatus());
        }
        catch
        {
            return NotificationAccessStatus.Unavailable;
        }
    }

    public async Task<NotificationAccessStatus> RequestAccessAsync()
    {
        try
        {
            return Map(await UserNotificationListener.Current.RequestAccessAsync());
        }
        catch
        {
            return NotificationAccessStatus.Unavailable;
        }
    }

    public void Start() => _poller.Start();

    public void Stop() => _poller.Stop();

    public void Dispose()
    {
        _poller.Arrived -= OnArrived;
        _poller.Dispose();
    }

    private void OnArrived(NotificationItem item) => NotificationArrived?.Invoke(item);

    private static NotificationAccessStatus Map(UserNotificationListenerAccessStatus status) => status switch
    {
        UserNotificationListenerAccessStatus.Allowed => NotificationAccessStatus.Allowed,
        UserNotificationListenerAccessStatus.Denied => NotificationAccessStatus.Denied,
        UserNotificationListenerAccessStatus.Unspecified => NotificationAccessStatus.Unspecified,
        _ => NotificationAccessStatus.Unavailable
    };

    // known null: temel çizgi okuması, yalnız kimlikler. Aksi halde ayrıntı (simge dahil) yalnız yeni kimlikler için üretilir.
    private static async Task<NotificationSnapshot> ReadAsync(IReadOnlySet<uint>? known, CancellationToken token)
    {
        var list = await UserNotificationListener.Current.GetNotificationsAsync(NotificationKinds.Toast).AsTask(token).ConfigureAwait(false);

        var ids = new List<uint>(list.Count);
        var items = new List<NotificationItem>();
        foreach (var notification in list)
        {
            ids.Add(notification.Id);
            if (known is null || known.Contains(notification.Id))
                continue;

            var item = await TryBuildAsync(notification).ConfigureAwait(false);
            if (item is not null)
                items.Add(item);
        }

        return new NotificationSnapshot(ids, items);
    }

    private static async Task<NotificationItem?> TryBuildAsync(UserNotification notification)
    {
        try
        {
            var appName = "";
            byte[]? icon = null;
            try
            {
                var display = notification.AppInfo.DisplayInfo;
                appName = display.DisplayName ?? "";
                icon = await ReadLogoAsync(display).ConfigureAwait(false);
            }
            catch
            {
                // Uygulama bilgisi çözülemeyen bildirim yine de gösterilir (adsız, simgesiz).
            }

            var title = "";
            var body = new List<string>();
            var binding = notification.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (binding is not null)
            {
                foreach (var element in binding.GetTextElements())
                {
                    if (string.IsNullOrWhiteSpace(element.Text))
                        continue;
                    if (title.Length == 0)
                        title = element.Text;
                    else
                        body.Add(element.Text);
                }
            }

            return new NotificationItem(notification.Id, appName, title, string.Join(" ", body), notification.CreationTime, icon);
        }
        catch
        {
            return null;
        }
    }

    // GetLogo simgesiz uygulamada null/istisna verebilir.
    private static async Task<byte[]?> ReadLogoAsync(Windows.ApplicationModel.AppDisplayInfo display)
    {
        try
        {
            var reference = display.GetLogo(new Windows.Foundation.Size(LogoSize, LogoSize));
            if (reference is null)
                return null;

            using var stream = await reference.OpenReadAsync();
            if (stream is null || stream.Size == 0 || stream.Size > 512 * 1024)
                return null;

            var buffer = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(buffer);
            return buffer;
        }
        catch
        {
            return null;
        }
    }
}
