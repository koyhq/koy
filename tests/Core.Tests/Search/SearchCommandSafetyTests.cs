using Koy.Core.Search;

namespace Koy.Core.Tests.Search;

public class SearchCommandSafetyTests
{
    private sealed class FakeShell : ISearchShell
    {
        public List<string> Log { get; } = [];

        public bool FileExists(string path) => path == @"C:\Tools\build.CMD";
        public bool DirectoryExists(string path) => false;
        public string? GetEnvironmentVariable(string name) => name == "PATH" ? @"C:\Tools" : name == "PATHEXT" ? ".CMD" : null;
        public void Open(string target) => Log.Add("open " + target);
        public void Run(string executable, string? arguments) => Log.Add($"run {executable} {arguments}".TrimEnd());
        public void RunWithArguments(string executable, IReadOnlyList<string> arguments) => Log.Add($"run {executable} [{string.Join("|", arguments)}]");
        public void RunInTerminal(string commandLine) => Log.Add("term " + commandLine);
        public void OpenContainingFolder(string path) => Log.Add("folder " + path);
        public void CopyText(string text) => Log.Add("copy " + text);
    }

    private static string T(string key) => key + "|{0}";

    [Theory]
    [InlineData("build -x", false)]
    [InlineData("build\t-x", false)]
    [InlineData("build -x\ncalc", true)]
    [InlineData("build\r\n", false)] // sondaki satır sonu kırpılır
    [InlineData("build -x\u0007", true)]
    public void Control_characters_are_detected(string text, bool expected) =>
        Assert.Equal(expected, new SearchQuery(text).NeedsCommandConfirmation);

    [Fact]
    public void Clipboard_query_always_needs_confirmation() =>
        Assert.True(new SearchQuery("build -x", fromClipboard: true).NeedsCommandConfirmation);

    [Fact]
    public void Display_marks_newlines() =>
        Assert.Equal("a⏎b⏎c d", SearchCommandSafety.ForDisplay("a\r\nb\rc\u0001d"));

    [Fact]
    public void Single_line_has_no_line_breaks()
    {
        var line = SearchCommandSafety.ToSingleLine("build -x\r\ncalc\n\nformat");
        Assert.DoesNotContain('\n', line);
        Assert.DoesNotContain('\r', line);
    }

    [Fact]
    public void Typed_single_line_command_runs_without_confirmation()
    {
        var result = new ShellCommandSearchSource(new FakeShell(), T).Search(new SearchQuery("build -x")).Single();

        Assert.False(result.RequiresConfirmation);
        Assert.False(result.Actions[0].RequiresConfirmation);
    }

    [Fact]
    public void Multiline_command_needs_confirmation_shows_all_text_and_sends_no_newline()
    {
        var shell = new FakeShell();
        var result = new ShellCommandSearchSource(shell, T).Search(new SearchQuery("build -x\ncalc")).Single();

        Assert.True(result.RequiresConfirmation);
        Assert.True(result.Actions[0].RequiresConfirmation);
        Assert.Equal("build -x⏎calc", result.ConfirmationDetail);
        Assert.Contains("⏎", result.Title);

        result.Execute();
        result.Actions[0].Execute();

        Assert.NotEmpty(shell.Log);
        Assert.All(shell.Log, entry => Assert.DoesNotContain('\n', entry));
    }

    [Fact]
    public void Clipboard_command_needs_confirmation_for_both_enter_and_terminal()
    {
        var result = new ShellCommandSearchSource(new FakeShell(), T).Search(new SearchQuery("build -x", fromClipboard: true)).Single();

        Assert.True(result.RequiresConfirmation);
        Assert.True(result.Actions[0].RequiresConfirmation);
        Assert.Equal("build -x", result.ConfirmationDetail);
    }

    // --- Denetleyici: pano kaynağı ve eylem onayı ---

    private sealed class ShellSource(ISearchShell shell) : SearchSource("shell", "Shell")
    {
        private readonly ShellCommandSearchSource _inner = new(shell, T);

        public override IEnumerable<SearchResult> Search(SearchQuery query) => _inner.Search(query);
    }

    [Fact]
    public void Clipboard_text_does_not_run_on_first_enter()
    {
        var shell = new FakeShell();
        var panel = new SearchPanelController([new ShellSource(shell)]);
        panel.Open();
        panel.ReplaceQuery("build -x", fromClipboard: true);

        Assert.Equal(SearchActivation.NeedsConfirmation, panel.Activate().Outcome);
        Assert.Empty(shell.Log);
        Assert.Equal(SearchActivation.Executed, panel.Activate().Outcome);
        Assert.Single(shell.Log);
    }

    [Fact]
    public void Terminal_action_confirms_separately_from_the_row()
    {
        var shell = new FakeShell();
        var panel = new SearchPanelController([new ShellSource(shell)]);
        panel.Open();
        panel.ReplaceQuery("build -x", fromClipboard: true);

        panel.Activate(); // satır için onay bekler
        Assert.Equal(SearchActivation.NeedsConfirmation, panel.Activate(firstAction: true).Outcome);
        Assert.Empty(shell.Log);
        Assert.Equal(SearchActivation.Executed, panel.Activate(firstAction: true).Outcome);
        Assert.Equal(["term build -x"], shell.Log);
    }

    [Fact]
    public void Clearing_the_box_ends_clipboard_origin_and_typed_text_runs_directly()
    {
        var shell = new FakeShell();
        var panel = new SearchPanelController([new ShellSource(shell)]);
        panel.Open();
        panel.ReplaceQuery("build -x", fromClipboard: true);
        panel.SetQuery("");
        panel.SetQuery("build -y");

        Assert.False(panel.QueryFromClipboard);
        Assert.Equal(SearchActivation.Executed, panel.Activate().Outcome);
    }

    [Fact]
    public void Clipboard_text_is_not_kept_for_next_open()
    {
        var panel = new SearchPanelController([new ShellSource(new FakeShell())]);
        panel.Open();
        panel.ReplaceQuery("build -x", fromClipboard: true);
        panel.Close();
        panel.Open();

        Assert.Equal("", panel.Query);
    }

    // --- PATH çözümleme önbelleği ---

    private sealed class CountingShell : ISearchShell
    {
        public int FileChecks { get; private set; }
        public List<string> Checked { get; } = [];

        public bool FileExists(string path)
        {
            FileChecks++;
            Checked.Add(path);
            return path == @"C:\Tools\build.CMD";
        }

        public bool DirectoryExists(string path) => false;
        public string? GetEnvironmentVariable(string name) => name == "PATH" ? @"\\nas\share;C:\Tools" : name == "PATHEXT" ? ".CMD" : null;
        public void Open(string target)
        {
        }

        public void Run(string executable, string? arguments)
        {
        }

        public void RunWithArguments(string executable, IReadOnlyList<string> arguments)
        {
        }

        public void RunInTerminal(string commandLine)
        {
        }

        public void OpenContainingFolder(string path)
        {
        }

        public void CopyText(string text)
        {
        }
    }

    [Fact]
    public void Path_lookup_is_cached_until_release_and_skips_network_directories()
    {
        var shell = new CountingShell();
        var source = new ShellCommandSearchSource(shell, T);

        _ = source.Search(new SearchQuery("build a")).Single();
        var afterFirst = shell.FileChecks;
        _ = source.Search(new SearchQuery("build a b")).Single();
        Assert.Equal(afterFirst, shell.FileChecks);

        _ = source.Search(new SearchQuery("nope")).ToList();
        var afterMiss = shell.FileChecks;
        Assert.True(afterMiss > afterFirst);
        _ = source.Search(new SearchQuery("nope x")).ToList();
        Assert.Equal(afterMiss, shell.FileChecks);
        Assert.DoesNotContain(shell.Checked, c => c.StartsWith(@"\\", StringComparison.Ordinal));

        source.Release();
        _ = source.Search(new SearchQuery("build a")).Single();
        Assert.True(shell.FileChecks > afterMiss);
    }

    [Theory]
    [InlineData(@"\\nas\share\x.exe", true)]
    [InlineData("//nas/share", true)]
    [InlineData(@"C:\Tools", false)]
    public void Network_paths_are_recognised(string path, bool expected) =>
        Assert.Equal(expected, ShellCommandSearchSource.IsNetworkPath(path));
}
