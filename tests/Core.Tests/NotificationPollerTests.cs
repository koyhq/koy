using Koy.Core.Notifications;

namespace Koy.Core.Tests;

public class NotificationPollerTests
{
    private static NotificationItem Item(uint id) => NotificationDiffTests.Item(id);

    private static NotificationSnapshot Snap(NotificationItem[] items, bool withDetails = true) =>
        new(items.Select(i => new NotificationStamp(i.Id, i.Time)).ToList(), withDetails ? items : []);

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Koşul zamanında gerçekleşmedi.");
            await Task.Delay(5);
        }
    }

    [Fact]
    public async Task PollOnce_FirstReadAsksForBaselineThenPassesKnownIds()
    {
        var seen = new List<IReadOnlyDictionary<uint, DateTimeOffset>?>();
        var poller = new NotificationPoller((known, _) =>
        {
            seen.Add(known);
            return Task.FromResult(Snap([Item(1)]));
        });
        var diff = new NotificationDiff();
        var arrived = new List<uint>();
        poller.Arrived += i => arrived.Add(i.Id);

        await poller.PollOnceAsync(diff, CancellationToken.None);
        await poller.PollOnceAsync(diff, CancellationToken.None);

        Assert.Null(seen[0]);
        Assert.True(seen[1]!.ContainsKey(1));
        Assert.Empty(arrived);
    }

    [Fact]
    public async Task PollOnce_RaisesArrivedForNewItemsOnly()
    {
        var round = 0;
        var poller = new NotificationPoller((_, _) =>
        {
            round++;
            return Task.FromResult(round == 1
                ? Snap([Item(1)], withDetails: false)
                : Snap([Item(1), Item(2)]));
        });
        var diff = new NotificationDiff();
        var arrived = new List<uint>();
        poller.Arrived += i => arrived.Add(i.Id);

        await poller.PollOnceAsync(diff, CancellationToken.None);
        await poller.PollOnceAsync(diff, CancellationToken.None);

        Assert.Equal([2u], arrived);
    }

    [Fact]
    public async Task Loop_PollsAtTheFixedIntervalAndNeverOverlaps()
    {
        var clock = new FakeClock();
        var polls = 0;
        var running = 0;
        var overlapped = false;
        var poller = new NotificationPoller(async (_, _) =>
        {
            if (Interlocked.Increment(ref running) > 1)
                overlapped = true;
            Interlocked.Increment(ref polls);
            await Task.Yield();
            Interlocked.Decrement(ref running);
            return new NotificationSnapshot([], []);
        }, clock);

        poller.Start();
        await WaitUntil(() => Volatile.Read(ref polls) == 1 && clock.ActiveTimers == 1);

        // Aralık dolmadan yeni yoklama yok.
        clock.Advance(NotificationPoller.Interval - TimeSpan.FromMilliseconds(1));
        await Task.Delay(30);
        Assert.Equal(1, Volatile.Read(ref polls));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        await WaitUntil(() => Volatile.Read(ref polls) == 2);

        poller.Stop();
        Assert.False(overlapped);
    }

    [Fact]
    public async Task Stop_RemovesTheTimerAndEndsPolling()
    {
        var clock = new FakeClock();
        var polls = 0;
        var poller = new NotificationPoller((_, _) =>
        {
            Interlocked.Increment(ref polls);
            return Task.FromResult(new NotificationSnapshot([], []));
        }, clock);

        poller.Start();
        await WaitUntil(() => Volatile.Read(ref polls) == 1 && clock.ActiveTimers == 1);

        poller.Stop();
        await WaitUntil(() => clock.ActiveTimers == 0);

        clock.Advance(TimeSpan.FromMinutes(1));
        await Task.Delay(30);
        Assert.Equal(1, Volatile.Read(ref polls));
        Assert.False(poller.IsRunning);
    }

    [Fact]
    public async Task Restart_TakesANewBaseline()
    {
        var clock = new FakeClock();
        var round = 0;
        var arrived = new List<uint>();
        var poller = new NotificationPoller((_, _) =>
        {
            Interlocked.Increment(ref round);
            return Task.FromResult(Snap([Item(7)]));
        }, clock);
        poller.Arrived += i => arrived.Add(i.Id);

        poller.Start();
        await WaitUntil(() => Volatile.Read(ref round) == 1 && clock.ActiveTimers == 1);
        poller.Stop();
        await WaitUntil(() => clock.ActiveTimers == 0);

        poller.Start();
        await WaitUntil(() => Volatile.Read(ref round) == 2 && clock.ActiveTimers == 1);
        poller.Stop();

        // Durdurulup yeniden başlayınca mevcut bildirimler yeni sayılmaz.
        Assert.Empty(arrived);
    }

    [Fact]
    public async Task ReadFailure_BacksOffToTheFailureInterval()
    {
        var clock = new FakeClock();
        var polls = 0;
        var poller = new NotificationPoller((_, _) =>
        {
            Interlocked.Increment(ref polls);
            throw new InvalidOperationException("liste okunamadı");
        }, clock);

        poller.Start();
        await WaitUntil(() => Volatile.Read(ref polls) == 1 && clock.ActiveTimers == 1);

        clock.Advance(NotificationPoller.Interval);
        await Task.Delay(30);
        Assert.Equal(1, Volatile.Read(ref polls));

        clock.Advance(NotificationPoller.FailureInterval - NotificationPoller.Interval);
        await WaitUntil(() => Volatile.Read(ref polls) == 2);
        poller.Stop();
    }
}
