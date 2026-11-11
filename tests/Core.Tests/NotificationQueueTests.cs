using Koy.Core.Notifications;

namespace Koy.Core.Tests;

public class NotificationQueueTests
{
    private static NotificationItem Item(uint id, string app = "Mail", string title = "Hi", string body = "Body", DateTimeOffset? time = null) =>
        new(id, app, title, body, time ?? new FakeClock().GetUtcNow());

    [Fact]
    public void ShowsInArrivalOrderOneAtATime()
    {
        var queue = new NotificationQueue(new FakeClock());
        queue.Enqueue(Item(1), false);
        queue.Enqueue(Item(2), false);

        Assert.Equal(1u, queue.TryTakeNext(false)!.Id);
        Assert.Null(queue.TryTakeNext(false));

        queue.Complete();
        Assert.Equal(2u, queue.TryTakeNext(false)!.Id);
    }

    [Fact]
    public void WaitsWhileTheNotchIsExpanded()
    {
        var queue = new NotificationQueue(new FakeClock());
        queue.Enqueue(Item(1), false);

        Assert.Null(queue.TryTakeNext(notchExpanded: true));
        Assert.Equal(1, queue.PendingCount);
        Assert.Equal(1u, queue.TryTakeNext(notchExpanded: false)!.Id);
    }

    [Fact]
    public void CardDurationIsSixSeconds() =>
        Assert.Equal(TimeSpan.FromSeconds(6), NotificationQueue.CardDuration);

    [Fact]
    public void FiltersOwnNotifications()
    {
        var queue = new NotificationQueue(new FakeClock());
        Assert.False(queue.Enqueue(Item(1, app: "Koy"), false));
        Assert.False(queue.Enqueue(Item(2, app: " koy "), false));
        Assert.True(queue.Enqueue(Item(3, app: "Koyun Takvimi"), false));
    }

    [Fact]
    public void FiltersWhileDoNotDisturbIsOn()
    {
        var queue = new NotificationQueue(new FakeClock());
        Assert.False(queue.Enqueue(Item(1), doNotDisturb: true));
        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void FiltersEmptyNotifications()
    {
        var queue = new NotificationQueue(new FakeClock());
        Assert.False(queue.Enqueue(Item(1, title: " ", body: ""), false));
        Assert.True(queue.Enqueue(Item(2, title: "", body: "only body"), false));
    }

    [Fact]
    public void IgnoresDuplicateIds()
    {
        var queue = new NotificationQueue(new FakeClock());
        Assert.True(queue.Enqueue(Item(1), false));
        Assert.False(queue.Enqueue(Item(1), false));

        queue.TryTakeNext(false);
        Assert.False(queue.Enqueue(Item(1), false));
    }

    [Fact]
    public void KeepsAtMostFivePendingDroppingTheOldest()
    {
        var queue = new NotificationQueue(new FakeClock());
        for (uint id = 1; id <= 7; id++)
            queue.Enqueue(Item(id), false);

        Assert.Equal(NotificationQueue.MaxPending, queue.PendingCount);
        Assert.Equal(3u, queue.TryTakeNext(false)!.Id);
    }

    [Fact]
    public void DropsNotificationsThatGrewStaleWhileWaiting()
    {
        var clock = new FakeClock();
        var queue = new NotificationQueue(clock);
        queue.Enqueue(Item(1, time: clock.GetUtcNow()), false);

        clock.Advance(NotificationQueue.MaxAge + TimeSpan.FromSeconds(1));
        queue.Enqueue(Item(2, time: clock.GetUtcNow()), false);

        Assert.Equal(2u, queue.TryTakeNext(false)!.Id);
    }

    [Fact]
    public void ClearForgetsEverything()
    {
        var queue = new NotificationQueue(new FakeClock());
        queue.Enqueue(Item(1), false);
        queue.Enqueue(Item(2), false);
        queue.TryTakeNext(false);

        queue.Clear();

        Assert.Null(queue.Current);
        Assert.Equal(0, queue.PendingCount);
        Assert.Null(queue.TryTakeNext(false));
    }
}
