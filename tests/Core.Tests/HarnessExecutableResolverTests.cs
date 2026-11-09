using Koy.Core.Voice;

namespace Koy.Core.Tests;

public class HarnessExecutableResolverTests
{
    private static readonly string NpmDir = Path.Combine("C:", "npm");
    private static readonly string ToolsDir = Path.Combine("C:", "tools");

    [Fact]
    public void Resolve_PrefersNativeExe_OverScriptShimInSameDirectory()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(NpmDir, "codex.cmd"),
            Path.Combine(NpmDir, "codex.exe")
        };

        var result = HarnessExecutableResolver.Resolve("codex", [NpmDir], files.Contains);

        Assert.Equal(HarnessExecutableKind.Found, result.Kind);
        Assert.Equal(Path.Combine(NpmDir, "codex.exe"), result.Path);
    }

    [Fact]
    public void Resolve_OnlyScriptShim_ReportsScriptNotSupported()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(NpmDir, "codex.cmd")
        };

        var result = HarnessExecutableResolver.Resolve("codex", [NpmDir], files.Contains);

        Assert.Equal(HarnessExecutableKind.ScriptNotSupported, result.Kind);
        Assert.Equal(Path.Combine(NpmDir, "codex.cmd"), result.Path);
    }

    [Fact]
    public void Resolve_FirstPathDirectoryWins()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(NpmDir, "claude.cmd"),
            Path.Combine(ToolsDir, "claude.exe")
        };

        var result = HarnessExecutableResolver.Resolve("claude", [NpmDir, ToolsDir], files.Contains);

        Assert.Equal(HarnessExecutableKind.ScriptNotSupported, result.Kind);
    }

    [Fact]
    public void Resolve_ExplicitScriptExtension_ReportsScriptNotSupported()
    {
        var path = Path.Combine(ToolsDir, "run.bat");

        var result = HarnessExecutableResolver.Resolve(path, [], p => p == path);

        Assert.Equal(HarnessExecutableKind.ScriptNotSupported, result.Kind);
        Assert.Equal(path, result.Path);
    }

    [Fact]
    public void Resolve_RootedExePath_IsUsedDirectly()
    {
        var path = Path.Combine(ToolsDir, "agy.exe");

        var result = HarnessExecutableResolver.Resolve(path, [NpmDir], p => p == path);

        Assert.Equal(HarnessExecutableKind.Found, result.Kind);
        Assert.Equal(path, result.Path);
    }

    [Fact]
    public void Resolve_ExtensionlessShellScript_IsNotTreatedAsRunnable()
    {
        // npm, aynı klasöre uzantısız sh betiği de koyar; Windows bunu çalıştıramaz.
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(NpmDir, "codex")
        };

        var result = HarnessExecutableResolver.Resolve("codex", [NpmDir], files.Contains);

        Assert.Equal(HarnessExecutableKind.NotFound, result.Kind);
    }

    [Fact]
    public void Resolve_Missing_ReturnsNotFound()
    {
        var result = HarnessExecutableResolver.Resolve("yok", [NpmDir, ToolsDir], _ => false);

        Assert.Equal(HarnessExecutableKind.NotFound, result.Kind);
        Assert.Equal("yok", result.Path);
    }
}
