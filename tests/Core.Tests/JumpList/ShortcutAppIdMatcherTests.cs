using Koy.Core.JumpList;

namespace Koy.Core.Tests.JumpList;

public class ShortcutAppIdMatcherTests
{
    private const string DiscordExe = @"C:\Users\u\AppData\Local\Discord\app-1.0.9267\Discord.exe";
    private const string Updater = @"C:\Users\u\AppData\Local\Discord\Update.exe";

    [Fact]
    public void Exact_IgnoresCaseAndQuotes()
    {
        Assert.True(ShortcutAppIdMatcher.MatchesExact(@"C:\Apps\Foo\foo.exe", "\"c:\\apps\\FOO\\Foo.exe\""));
        Assert.False(ShortcutAppIdMatcher.MatchesExact(@"C:\Apps\Foo\foo.exe", @"C:\Apps\Foo\bar.exe"));
        Assert.False(ShortcutAppIdMatcher.MatchesExact(@"C:\Apps\Foo\foo.exe", null));
        Assert.False(ShortcutAppIdMatcher.MatchesExact(@"C:\Apps\Foo\foo.exe", ""));
    }

    [Fact]
    public void Squirrel_MatchesAcrossVersions()
    {
        Assert.True(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, Updater, "--processStart Discord.exe"));
        Assert.True(ShortcutAppIdMatcher.MatchesSquirrel(
            @"C:\Users\u\AppData\Local\Discord\app-2.0.1\Discord.exe", Updater, "--processStart \"discord.exe\""));
        Assert.True(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, Updater, "--processStartAndWait Discord.exe"));
    }

    [Fact]
    public void Squirrel_RejectsOtherRootExeOrMissingFlag()
    {
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, @"C:\Other\Update.exe", "--processStart Discord.exe"));
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, Updater, "--processStart Other.exe"));
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, Updater, "Discord.exe"));
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(DiscordExe, Updater, null));
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(
            DiscordExe, @"C:\Users\u\AppData\Local\Discord\app-1.0.9267\Update.exe", "--processStart Discord.exe"));
    }

    [Fact]
    public void Squirrel_AcceptsRootStubLauncher()
    {
        // Discord'un gerçek kısayolu: hedef kökteki Discord.exe, bağımsız değişken --processStart Discord.exe.
        Assert.True(ShortcutAppIdMatcher.MatchesSquirrel(
            DiscordExe, @"C:\Users\u\AppData\Local\Discord\Discord.exe", "--processStart Discord.exe"));
    }

    [Fact]
    public void Squirrel_RequiresAppFolderLayout()
    {
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(
            @"C:\Users\u\AppData\Local\Discord\bin\Discord.exe", Updater, "--processStart Discord.exe"));
        Assert.False(ShortcutAppIdMatcher.MatchesSquirrel(
            @"C:\Users\u\AppData\Local\Discord\Discord.exe", Updater, "--processStart Discord.exe"));
    }

    [Fact]
    public void ExeAppIdCache_KeepsNegativeResultsAndIsBounded()
    {
        var cache = new ExeAppIdCache(2);
        cache.Set(@"C:\a.exe", null);
        cache.Set(@"C:\b.exe", "B");
        Assert.True(cache.TryGet(@"C:\A.EXE", out var a));
        Assert.Null(a);
        cache.Set(@"C:\c.exe", "C");
        Assert.Equal(2, cache.Count);
        Assert.False(cache.TryGet(@"C:\a.exe", out _));
        Assert.True(cache.TryGet(@"C:\b.exe", out var b));
        Assert.Equal("B", b);
        cache.Clear();
        Assert.Equal(0, cache.Count);
    }
}
