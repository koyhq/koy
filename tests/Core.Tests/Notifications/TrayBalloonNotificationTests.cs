using Koy.Core.Notch;
using Koy.Core.Notifications;
using Koy.Core.Tray;

namespace Koy.Core.Tests.Notifications;

public class TrayBalloonNotificationTests
{
    private static readonly RelativeTimeLabels Labels = new("şimdi", "{0} dk önce", "{0} sa önce");

    [Fact]
    public void BalloonFlowsThroughNotificationQueueToNotchCard()
    {
        var clock = new FakeClock();
        var queue = new NotificationQueue(clock);
        byte[] icon = [1, 2, 3, 4];

        var balloon = new TrayBalloonNotification(
            new TrayIconKey(100, 1, Guid.Empty),
            AppName: "Steam",
            Title: "İndirme tamamlandı",
            Body: "Oyun oynamaya hazır.",
            Flags: TrayBalloonFlags.Info,
            Time: clock.GetUtcNow(),
            Icon: icon);

        var item = NotificationPresentation.FromTrayBalloon(balloon, 0x8000_0001u);
        Assert.True(queue.Enqueue(item, doNotDisturb: false));

        var next = queue.TryTakeNext(notchExpanded: false);
        Assert.NotNull(next);

        var card = NotificationPresentation.ToCard(next, clock.GetUtcNow(), hideBody: false, Labels);
        Assert.Equal(NotchCardKind.Notification, card.Kind);
        Assert.Equal("Steam · şimdi", card.Header);
        Assert.Equal("İndirme tamamlandı", card.Title);
        Assert.Equal("Oyun oynamaya hazır.", card.Subtitle);
        Assert.Same(icon, card.Icon);
    }

    [Fact]
    public void BalloonRespectsDoNotDisturbAndHideBody()
    {
        var clock = new FakeClock();
        var queue = new NotificationQueue(clock);
        var balloon = new TrayBalloonNotification(
            new TrayIconKey(100, 1, Guid.Empty),
            AppName: "Mail",
            Title: "Yeni ileti",
            Body: "Gizli içerik",
            Flags: TrayBalloonFlags.Info,
            Time: clock.GetUtcNow());

        var item = NotificationPresentation.FromTrayBalloon(balloon, 0x8000_0002u);

        Assert.False(queue.Enqueue(item, doNotDisturb: true));
        Assert.True(queue.Enqueue(item, doNotDisturb: false));

        var next = queue.TryTakeNext(notchExpanded: false)!;
        var card = NotificationPresentation.ToCard(next, clock.GetUtcNow(), hideBody: true, Labels);
        Assert.Equal("Yeni ileti", card.Title);
        Assert.Equal("", card.Subtitle);
    }
}
