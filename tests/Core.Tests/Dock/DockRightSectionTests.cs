using Koy.Core.Dock;

namespace Koy.Core.Tests.Dock;

public class DockRightSectionTests
{
    [Fact]
    public void Nothing_visible_hides_separator_and_tray()
    {
        var s = DockRightSection.Resolve(false, false, false, true);
        Assert.False(s.ShowFirstSeparator);
        Assert.False(s.ShowFullTray);
        Assert.False(s.ShowSmallTray);
    }

    [Fact]
    public void Small_tray_does_not_need_the_first_separator()
    {
        var s = DockRightSection.Resolve(false, false, true, smallTray: true);
        Assert.False(s.ShowFirstSeparator);
        Assert.False(s.ShowFullTray);
        Assert.True(s.ShowSmallTray);
    }

    [Fact]
    public void Full_tray_alone_shows_the_separator()
    {
        var s = DockRightSection.Resolve(false, false, true, smallTray: false);
        Assert.True(s.ShowFirstSeparator);
        Assert.True(s.ShowFullTray);
        Assert.False(s.ShowSmallTray);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Folders_or_recents_show_the_separator(bool folders, bool recents)
    {
        Assert.True(DockRightSection.Resolve(folders, recents, false, true).ShowFirstSeparator);
    }

    [Fact]
    public void Missing_tray_service_hides_both_tray_buttons()
    {
        var s = DockRightSection.Resolve(true, true, hasTray: false, smallTray: false);
        Assert.False(s.ShowFullTray);
        Assert.False(s.ShowSmallTray);
    }
}
