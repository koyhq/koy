using Koy.Core.Notch;

namespace Koy.Core.Tests;

public class NotchCardTests
{
    [Fact]
    public void HasPrimaryAction_OnlyWhenLabelGiven()
    {
        Assert.True(new NotchCard(NotchCardKind.Update, "t", "s", "Update").HasPrimaryAction);
        Assert.False(new NotchCard(NotchCardKind.Notification, "t", "s").HasPrimaryAction);
    }

    [Fact]
    public void CardSize_FitsInsideCanvasAndDiffersFromHud()
    {
        Assert.True(NotchLayout.CardWidth < NotchLayout.CanvasWidth);
        Assert.True(NotchLayout.CardHeight < NotchLayout.CanvasHeight);
        Assert.True(NotchLayout.CardWidth > 260 && NotchLayout.CardHeight > 40);
    }
}
