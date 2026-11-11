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

    private static byte[] Image(int w, int h, params (int x, int y)[] opaque)
    {
        var px = new byte[w * h * 4];
        foreach (var (x, y) in opaque)
            px[(y * w + x) * 4 + 3] = 255;
        return px;
    }

    [Fact]
    public void BoundsCoverOnlyVisiblePixels()
    {
        var bounds = IconBlankCheck.GetVisibleBounds(Image(10, 8, (2, 1), (6, 5), (4, 3)), 10, 8);
        Assert.Equal(new IconBounds(2, 1, 5, 5), bounds);
    }

    [Fact]
    public void BoundsOfFullyOpaqueImageAreTheWholeImage()
    {
        var px = Image(2, 2, (0, 0), (1, 0), (0, 1), (1, 1));
        Assert.Equal(new IconBounds(0, 0, 2, 2), IconBlankCheck.GetVisibleBounds(px, 2, 2));
    }

    [Fact]
    public void BoundsOfBlankOrMalformedImageAreNull()
    {
        Assert.Null(IconBlankCheck.GetVisibleBounds(new byte[16], 2, 2));
        Assert.Null(IconBlankCheck.GetVisibleBounds(new byte[4], 2, 2));
        Assert.Null(IconBlankCheck.GetVisibleBounds([], 0, 0));
    }

    [Fact]
    public void BoundsOfSinglePixel() =>
        Assert.Equal(new IconBounds(3, 2, 1, 1), IconBlankCheck.GetVisibleBounds(Image(5, 4, (3, 2)), 5, 4));
}
