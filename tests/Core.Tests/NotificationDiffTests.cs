using Koy.Core.Notifications;

namespace Koy.Core.Tests;

public class NotificationDiffTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    internal static NotificationItem Item(uint id, string app = "Mail", string title = "Hi", string body = "Body", int secondsAfter = 0) =>
        new(id, app, title, body, T0.AddSeconds(secondsAfter));

    private static NotificationSnapshot Snap(params NotificationItem[] items) =>
        new(items.Select(i => i.Id).ToList(), items);

    [Fact]
    public void FirstReadIsBaselineAndYieldsNothing()
    {
        var diff = new NotificationDiff();
        Assert.False(diff.HasBaseline);

        var arrived = diff.Apply(Snap(Item(1), Item(2)));

        Assert.Empty(arrived);
        Assert.True(diff.HasBaseline);
        Assert.Contains(1u, diff.Known);
        Assert.Contains(2u, diff.Known);
    }

    [Fact]
    public void NewIdAfterBaselineIsReported()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap(Item(1)));

        var arrived = diff.Apply(Snap(Item(1), Item(2, title: "New")));

        var item = Assert.Single(arrived);
        Assert.Equal(2u, item.Id);
        Assert.Equal("New", item.Title);
    }

    [Fact]
    public void SameIdIsReportedOnlyOnce()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap());
        Assert.Single(diff.Apply(Snap(Item(5))));
        Assert.Empty(diff.Apply(Snap(Item(5))));
    }

    [Fact]
    public void ArrivalsComeInTimeOrder()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap());

        var arrived = diff.Apply(Snap(Item(9, secondsAfter: 5), Item(7, secondsAfter: 1), Item(8, secondsAfter: 3)));

        Assert.Equal([7u, 8u, 9u], arrived.Select(i => i.Id));
    }

    [Fact]
    public void DismissedIdsAreForgottenSoTheSetStaysBounded()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap(Item(1), Item(2)));
        diff.Apply(Snap(Item(2)));

        Assert.DoesNotContain(1u, diff.Known);
        Assert.Contains(2u, diff.Known);
    }

    [Fact]
    public void ItemMissingFromIdsIsIgnored()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap());

        // Ayrıntısı gelmiş ama artık listede olmayan (okuma sırasında kapanmış) bildirim sayılmaz.
        var arrived = diff.Apply(new NotificationSnapshot([], [Item(3)]));

        Assert.Empty(arrived);
    }

    [Fact]
    public void IdWithoutDetailIsKnownButNotReported()
    {
        var diff = new NotificationDiff();
        diff.Apply(Snap());

        var arrived = diff.Apply(new NotificationSnapshot([4], []));

        Assert.Empty(arrived);
        Assert.Contains(4u, diff.Known);
    }
}
