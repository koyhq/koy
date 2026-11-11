using Koy.Core.Notch;
using Koy.Core.Notifications;

namespace Koy.Core.Tests;

public class IconBlankCheckTests
{
    [Fact]
    public void EmptyDataIsBlank() => Assert.True(IconBlankCheck.IsBlank([]));

    [Fact]
    public void FullyTransparentIsBlank() => Assert.True(IconBlankCheck.IsBlank(new byte[64]));

    [Fact]
    public void OneVisiblePixelIsNotBlank()
    {
        var pixels = new byte[64];
        pixels[4 * 5 + 3] = 255;
        Assert.False(IconBlankCheck.IsBlank(pixels));
    }

    [Fact]
    public void NearlyInvisibleAlphaIsBlank()
    {
        var pixels = new byte[16];
        pixels[3] = IconBlankCheck.VisibleAlpha;
        Assert.True(IconBlankCheck.IsBlank(pixels));
    }

    [Fact]
    public void EachCardKeepsItsOwnIconsAndFallback()
    {
        var labels = new RelativeTimeLabels("now", "{0}m", "{0}h");
        var now = DateTimeOffset.UtcNow;
        byte[] a = [1], b = [2], fa = [3];

        var cardA = NotificationPresentation.ToCard(new NotificationItem(1, "A", "t", "b", now, a, fa), now, false, labels);
        var cardB = NotificationPresentation.ToCard(new NotificationItem(2, "B", "t", "b", now, b), now, false, labels);

        Assert.Same(a, cardA.Icon);
        Assert.Same(fa, cardA.FallbackIcon);
        Assert.Same(b, cardB.Icon);
        Assert.Null(cardB.FallbackIcon);
    }
}
