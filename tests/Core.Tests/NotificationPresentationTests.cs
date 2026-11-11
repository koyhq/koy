using Koy.Core.Notch;
using Koy.Core.Notifications;

namespace Koy.Core.Tests;

public class NotificationPresentationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly RelativeTimeLabels Labels = new("just now", "{0} min ago", "{0} h ago");

    private static NotificationItem Item(string app = "Mail", string title = "Title", string body = "Body", DateTimeOffset? time = null, byte[]? icon = null) =>
        new(1, app, title, body, time ?? Now, icon);

    [Fact]
    public void BuildsHeaderTitleAndBody()
    {
        var card = NotificationPresentation.ToCard(Item(), Now, hideBody: false, Labels);

        Assert.Equal(NotchCardKind.Notification, card.Kind);
        Assert.Equal("Mail · just now", card.Header);
        Assert.Equal("Title", card.Title);
        Assert.Equal("Body", card.Subtitle);
        Assert.False(card.HasPrimaryAction);
    }

    [Fact]
    public void CarriesAppNameTimeAndIcon()
    {
        byte[] icon = [1, 2, 3];
        var card = NotificationPresentation.ToCard(Item(icon: icon), Now, false, Labels);

        Assert.Equal("Mail", card.AppName);
        Assert.Equal(Now, card.Timestamp);
        Assert.Same(icon, card.Icon);
    }

    [Fact]
    public void HideBodyKeepsOnlyAppAndTitle()
    {
        var card = NotificationPresentation.ToCard(Item(body: "secret"), Now, hideBody: true, Labels);

        Assert.Equal("Title", card.Title);
        Assert.Equal("", card.Subtitle);
        Assert.False(card.HasSubtitle);
    }

    [Fact]
    public void BodyOnlyNotificationUsesBodyAsTitle()
    {
        var card = NotificationPresentation.ToCard(Item(title: "", body: "Just text"), Now, false, Labels);

        Assert.Equal("Just text", card.Title);
        Assert.Equal("", card.Subtitle);
    }

    [Fact]
    public void MissingAppNameDropsTheSeparator()
    {
        var card = NotificationPresentation.ToCard(Item(app: ""), Now, false, Labels);
        Assert.Equal("just now", card.Header);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1 min ago")]
    [InlineData(5 * 60 + 30, "5 min ago")]
    [InlineData(59 * 60, "59 min ago")]
    [InlineData(3600, "1 h ago")]
    [InlineData(5 * 3600, "5 h ago")]
    public void FormatsRelativeTime(int secondsAgo, string expected) =>
        Assert.Equal(expected, NotificationPresentation.FormatAge(Now.AddSeconds(-secondsAgo), Now, Labels));

    [Fact]
    public void FutureTimestampCountsAsNow() =>
        Assert.Equal("just now", NotificationPresentation.FormatAge(Now.AddMinutes(3), Now, Labels));

    [Fact]
    public void TruncatesLongTextWithEllipsis()
    {
        var result = NotificationPresentation.Truncate(new string('a', 200), 20);

        Assert.Equal(20, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void LeavesShortTextAlone() =>
        Assert.Equal("short", NotificationPresentation.Truncate("short", 20));

    [Fact]
    public void CollapsesNewlinesAndRepeatedSpaces() =>
        Assert.Equal("a b c", NotificationPresentation.Truncate("  a\r\n\n b \t c  ", 20));

    [Fact]
    public void DoesNotSplitASurrogatePair()
    {
        // 9 harf + 3 emoji (her biri 2 char); 11'de kesince çift ortadan bölünmemeli.
        var result = NotificationPresentation.Truncate(new string('a', 9) + "😀😀😀", 11);

        Assert.True(result.Length <= 11);
        var withoutEllipsis = result[..^1];
        Assert.False(withoutEllipsis.Length > 0 && char.IsHighSurrogate(withoutEllipsis[^1]));
    }

    [Fact]
    public void LongTitleBodyAndAppFitTheCardLimits()
    {
        var item = Item(title: new string('t', 500), body: new string('b', 500), app: new string('x', 100));
        var card = NotificationPresentation.ToCard(item, Now, false, Labels);

        Assert.True(card.Title.Length <= NotificationPresentation.MaxTitleLength);
        Assert.True(card.Subtitle.Length <= NotificationPresentation.MaxBodyLength);
        Assert.True(card.AppName.Length <= NotificationPresentation.MaxAppNameLength);
    }

    [Fact]
    public void NullOrBlankTextBecomesEmpty()
    {
        Assert.Equal("", NotificationPresentation.Truncate(null, 10));
        Assert.Equal("", NotificationPresentation.Truncate("   ", 10));
    }
}
