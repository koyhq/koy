using Koy.Core.JumpList;

namespace Koy.Core.Tests.JumpList;

public class JumpListEntryFactoryTests
{
    [Fact]
    public void Link_with_title_and_target_becomes_a_task_entry()
    {
        var entry = JumpListEntryFactory.FromLink("Yeni Pencere", @"C:\app\app.exe", "--new-window", @"C:\app");

        Assert.NotNull(entry);
        Assert.Equal("Yeni Pencere", entry.Title);
        Assert.Equal(@"C:\app\app.exe", entry.Launch!.Target);
        Assert.Equal("--new-window", entry.Launch.Arguments);
        Assert.Equal(@"C:\app", entry.Launch.WorkingDirectory);
        Assert.False(entry.Launch.IsDocument);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  ", " ")]
    public void Empty_title_and_target_is_a_separator(string? title, string? target)
    {
        var entry = JumpListEntryFactory.FromLink(title, target, null, null);

        Assert.NotNull(entry);
        Assert.True(entry.IsSeparator);
    }

    [Fact]
    public void Marked_separator_wins_even_with_text()
    {
        Assert.True(JumpListEntryFactory.FromLink("x", @"C:\a.exe", null, null, markedSeparator: true)!.IsSeparator);
    }

    [Fact]
    public void Empty_title_with_a_target_is_skipped_not_invented()
    {
        // Spotify gibi: PKEY_Title boş ama hedef var. Başlık uydurulmaz.
        Assert.Null(JumpListEntryFactory.FromLink("", @"C:\Users\x\spotify.exe", "--uri=spotify:", null));
        Assert.Null(JumpListEntryFactory.FromLink("   ", @"C:\Users\x\spotify.exe", null, null));
    }

    [Fact]
    public void Title_without_target_is_skipped()
    {
        Assert.Null(JumpListEntryFactory.FromLink("Başlık", "", null, null));
    }

    [Fact]
    public void Oversized_target_or_arguments_are_skipped()
    {
        Assert.Null(JumpListEntryFactory.FromLink("a", new string('x', JumpListLimits.MaxTargetChars + 1), null, null));
        Assert.Null(JumpListEntryFactory.FromLink("a", @"C:\a.exe", new string('x', JumpListLimits.MaxArgumentsChars + 1), null));
    }

    [Fact]
    public void Blank_arguments_and_directory_become_null()
    {
        var entry = JumpListEntryFactory.FromLink("a", @"C:\a.exe", "  ", "");

        Assert.Null(entry!.Launch!.Arguments);
        Assert.Null(entry.Launch.WorkingDirectory);
    }

    [Fact]
    public void Document_entry_is_marked_as_document()
    {
        var entry = JumpListEntryFactory.FromDocument("proje", @"C:\Dev\proje");

        Assert.True(entry!.Launch!.IsDocument);
        Assert.Null(JumpListEntryFactory.FromDocument("", @"C:\Dev\proje"));
        Assert.Null(JumpListEntryFactory.FromDocument("x", null));
    }

    [Fact]
    public void Title_is_sanitised_and_truncated()
    {
        var entry = JumpListEntryFactory.FromLink("  Yeni\r\n  pencere\u0000  ", @"C:\a.exe", null, null);
        Assert.Equal("Yeni pencere", entry!.Title);

        var long_ = JumpListEntryFactory.FromLink(new string('a', 500), @"C:\a.exe", null, null);
        Assert.Equal(JumpListLimits.MaxTitleChars, long_!.Title.Length);
        Assert.EndsWith("…", long_.Title);
    }

    [Fact]
    public void Truncation_does_not_split_a_surrogate_pair()
    {
        var title = new string('a', JumpListLimits.MaxTitleChars - 2) + "😀😀😀";

        var entry = JumpListEntryFactory.FromLink(title, @"C:\a.exe", null, null);

        Assert.All(entry!.Title.Select((c, i) => (c, i)), p =>
        {
            if (char.IsHighSurrogate(p.c))
                Assert.True(char.IsLowSurrogate(entry.Title[p.i + 1]));
        });
        Assert.True(entry.Title.Length <= JumpListLimits.MaxTitleChars);
    }

    [Theory]
    [InlineData("@%SystemRoot%\\system32\\shell32.dll,-22067", true)]
    [InlineData("@{Pkg?ms-resource://x}", true)]
    [InlineData("@", false)]
    [InlineData("Normal", false)]
    [InlineData(null, false)]
    public void Indirect_strings_are_detected(string? text, bool expected)
    {
        Assert.Equal(expected, JumpListText.IsIndirect(text));
    }
}

public class JumpListLaunchPlanTests
{
    private const string Code = @"C:\Apps\Code\Code.exe";

    [Fact]
    public void Task_runs_as_is()
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch(@"C:\a\a.exe", "--x", @"C:\a"), Code);

        Assert.Equal(@"C:\a\a.exe", plan.File);
        Assert.Equal("--x", plan.Arguments);
        Assert.Equal(@"C:\a", plan.WorkingDirectory);
    }

    [Fact]
    public void Document_opens_with_the_app_exe()
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch(@"C:\Dev\proje", IsDocument: true), Code);

        Assert.Equal(Code, plan.File);
        Assert.Equal("\"C:\\Dev\\proje\"", plan.Arguments);
    }

    [Theory]
    [InlineData(@"C:\Oyunlar\Oyun.url")]
    [InlineData(@"C:\Kisayol\a.lnk")]
    [InlineData(@"C:\Araclar\arac.exe")]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("shell:AppsFolder\\Pkg!App")]
    public void Shortcuts_programs_and_shell_names_use_the_default_association(string target)
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch(target, IsDocument: true), Code);

        Assert.Equal(target, plan.File);
        Assert.Null(plan.Arguments);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(@"C:\Apps\run.bat")]
    [InlineData(@"C:\Program Files\WindowsApps\Pkg\app.exe")]
    [InlineData(@"C:\Windows\System32\ApplicationFrameHost.exe")]
    public void Unknown_or_hosted_app_exe_uses_the_default_association(string? exe)
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch(@"C:\Dev\proje", IsDocument: true), exe);

        Assert.Equal(@"C:\Dev\proje", plan.File);
    }

    [Fact]
    public void Url_documents_open_with_the_app_exe()
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch("https://example.com/a", IsDocument: true), @"C:\Apps\chrome.exe");

        Assert.Equal(@"C:\Apps\chrome.exe", plan.File);
        Assert.Equal("\"https://example.com/a\"", plan.Arguments);
    }

    [Fact]
    public void Quote_in_target_uses_the_default_association()
    {
        var plan = JumpListLaunchPlan.For(new JumpListLaunch("a\" --evil", IsDocument: true), Code);

        Assert.Equal("a\" --evil", plan.File);
    }
}

public class JumpListCacheTests
{
    private static readonly FileStamp A = new(10, 100);
    private static readonly FileStamp B = new(20, 200);

    private static IReadOnlyList<JumpListSection> Sections(string name) =>
        [new JumpListSection(JumpListSectionKind.Custom, name, [])];

    [Fact]
    public void Hit_requires_equal_stamps()
    {
        var cache = new JumpListCache();
        cache.Set("app", A, B, Sections("1"));

        Assert.True(cache.TryGet("APP", A, B, out var hit));
        Assert.Equal("1", hit[0].Name);
        Assert.False(cache.TryGet("app", A, A, out _));
    }

    [Fact]
    public void Stale_entry_is_removed_on_miss()
    {
        var cache = new JumpListCache();
        cache.Set("app", A, B, Sections("1"));

        Assert.False(cache.TryGet("app", B, B, out _));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Capacity_is_bounded_and_least_recently_used_goes_first()
    {
        var cache = new JumpListCache(capacity: 3);
        cache.Set("a", A, A, Sections("a"));
        cache.Set("b", A, A, Sections("b"));
        cache.Set("c", A, A, Sections("c"));
        Assert.True(cache.TryGet("a", A, A, out _)); // a yeniden kullanıldı
        cache.Set("d", A, A, Sections("d"));         // b atılmalı

        Assert.Equal(3, cache.Count);
        Assert.False(cache.TryGet("b", A, A, out _));
        Assert.True(cache.TryGet("a", A, A, out _));
        Assert.True(cache.TryGet("c", A, A, out _));
        Assert.True(cache.TryGet("d", A, A, out _));
    }

    [Fact]
    public void Set_replaces_existing_app_and_clear_empties()
    {
        var cache = new JumpListCache();
        cache.Set("app", A, A, Sections("old"));
        cache.Set("app", B, B, Sections("new"));

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("app", B, B, out var hit));
        Assert.Equal("new", hit[0].Name);

        cache.Clear();
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Missing_stamp_is_distinct_from_real_files()
    {
        Assert.NotEqual(FileStamp.Missing, new FileStamp(0, 0));
    }
}
