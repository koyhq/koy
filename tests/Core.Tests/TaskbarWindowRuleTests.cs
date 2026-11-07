using Koy.Core.Shell;

namespace Koy.Core.Tests;

public class TaskbarWindowRuleTests
{
    private static TaskbarWindowFacts Plain() => new(
        IsVisible: true, IsCloaked: false, IsShellWindow: false,
        HasOwner: false, IsAppWindow: false, IsToolWindow: false, IsNoActivate: false);

    [Fact]
    public void Plain_visible_unowned_window_is_shown()
        => Assert.True(TaskbarWindowRule.ShouldShow(Plain()));

    [Fact]
    public void Invisible_window_is_hidden_even_with_appwindow()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { IsVisible = false, IsAppWindow = true }));

    [Fact]
    public void Cloaked_window_is_hidden_even_with_appwindow()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { IsCloaked = true, IsAppWindow = true }));

    [Fact]
    public void Shell_window_is_hidden()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { IsShellWindow = true, IsAppWindow = true }));

    [Fact]
    public void Appwindow_style_wins_over_owner_toolwindow_and_noactivate()
    {
        var facts = Plain() with { IsAppWindow = true, HasOwner = true, IsToolWindow = true, IsNoActivate = true };
        Assert.True(TaskbarWindowRule.ShouldShow(facts));
    }

    [Fact]
    public void Owned_window_without_appwindow_is_hidden()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { HasOwner = true }));

    [Fact]
    public void Toolwindow_without_appwindow_is_hidden()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { IsToolWindow = true }));

    [Fact]
    public void Noactivate_without_appwindow_is_hidden()
        => Assert.False(TaskbarWindowRule.ShouldShow(Plain() with { IsNoActivate = true }));
}
