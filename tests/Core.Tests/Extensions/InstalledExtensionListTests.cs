using Koy.Core.Extensions;

namespace Koy.Core.Tests.Extensions;

public class InstalledExtensionListTests
{
    private static InstalledExtension Installed(string id, bool enabled = true)
    {
        ExtensionManifestValidator.TryCreate(
            id, "Ad " + id, "1.0.0", "Ada", null, "data", out var manifest, out _);
        return new InstalledExtension(manifest!, enabled);
    }

    [Fact]
    public void Build_orders_by_id_regardless_of_input_order()
    {
        var ok = InstalledExtensionList.TryBuild(
            [Installed("zeta"), Installed("alpha"), Installed("mid")],
            out var list, out var error);

        Assert.True(ok);
        Assert.Equal(InstalledExtensionListError.None, error);
        Assert.Equal(new[] { "alpha", "mid", "zeta" }, list.Select(e => e.Manifest.Id));
    }

    [Fact]
    public void Build_keeps_enabled_state()
    {
        InstalledExtensionList.TryBuild(
            [Installed("beta", enabled: false), Installed("alpha", enabled: true)],
            out var list, out _);

        Assert.False(list[1].Enabled);
        Assert.True(list[0].Enabled);
    }

    [Fact]
    public void Build_accepts_empty_list()
    {
        var ok = InstalledExtensionList.TryBuild([], out var list, out var error);

        Assert.True(ok);
        Assert.Empty(list);
        Assert.Equal(InstalledExtensionListError.None, error);
    }

    [Fact]
    public void Build_rejects_duplicate_ids()
    {
        var ok = InstalledExtensionList.TryBuild(
            [Installed("alpha"), Installed("alpha", enabled: false)],
            out _, out var error);

        Assert.False(ok);
        Assert.Equal(InstalledExtensionListError.DuplicateId, error);
    }

    [Fact]
    public void Build_accepts_exactly_the_maximum_count()
    {
        var entries = Enumerable.Range(0, InstalledExtensionList.MaxInstalled)
            .Select(i => Installed($"ext-{i:D2}"))
            .ToList();

        var ok = InstalledExtensionList.TryBuild(entries, out var list, out _);

        Assert.True(ok);
        Assert.Equal(InstalledExtensionList.MaxInstalled, list.Count);
    }

    [Fact]
    public void Build_rejects_more_than_the_maximum_count()
    {
        var entries = Enumerable.Range(0, InstalledExtensionList.MaxInstalled + 1)
            .Select(i => Installed($"ext-{i:D2}"))
            .ToList();

        var ok = InstalledExtensionList.TryBuild(entries, out var list, out var error);

        Assert.False(ok);
        Assert.Empty(list);
        Assert.Equal(InstalledExtensionListError.TooManyExtensions, error);
    }

    [Fact]
    public void SetEnabled_changes_only_the_named_extension()
    {
        InstalledExtensionList.TryBuild([Installed("alpha"), Installed("beta")], out var list, out _);

        var ok = InstalledExtensionList.TrySetEnabled(list, "beta", false, out var result, out var error);

        Assert.True(ok);
        Assert.Equal(InstalledExtensionListError.None, error);
        Assert.True(result[0].Enabled);
        Assert.False(result[1].Enabled);
        Assert.Equal(new[] { "alpha", "beta" }, result.Select(e => e.Manifest.Id));
    }

    [Fact]
    public void SetEnabled_does_not_modify_the_input_list()
    {
        InstalledExtensionList.TryBuild([Installed("alpha")], out var list, out _);

        InstalledExtensionList.TrySetEnabled(list, "alpha", false, out _, out _);

        Assert.True(list[0].Enabled);
    }

    [Fact]
    public void SetEnabled_on_unknown_id_fails_and_returns_the_same_list()
    {
        InstalledExtensionList.TryBuild([Installed("alpha")], out var list, out _);

        var ok = InstalledExtensionList.TrySetEnabled(list, "missing", false, out var result, out var error);

        Assert.False(ok);
        Assert.Equal(InstalledExtensionListError.UnknownId, error);
        Assert.Same(list, result);
    }

    [Fact]
    public void SetEnabled_match_is_case_sensitive()
    {
        InstalledExtensionList.TryBuild([Installed("alpha")], out var list, out _);

        var ok = InstalledExtensionList.TrySetEnabled(list, "Alpha", false, out _, out var error);

        Assert.False(ok);
        Assert.Equal(InstalledExtensionListError.UnknownId, error);
    }

    [Fact]
    public void Build_stops_reading_once_limit_is_exceeded()
    {
        var pulled = 0;
        IEnumerable<InstalledExtension> Endless()
        {
            while (pulled < 10_000)
            {
                yield return Installed($"ext-{pulled++}".Replace('0', 'a').Replace('1', 'b').Replace('2', 'c')
                    .Replace('3', 'd').Replace('4', 'e').Replace('5', 'f').Replace('6', 'g').Replace('7', 'h')
                    .Replace('8', 'i').Replace('9', 'j'));
            }
        }

        var ok = InstalledExtensionList.TryBuild(Endless(), out _, out var error);

        Assert.False(ok);
        Assert.Equal(InstalledExtensionListError.TooManyExtensions, error);
        Assert.Equal(InstalledExtensionList.MaxInstalled + 1, pulled);
    }

    [Fact]
    public void Built_list_cannot_be_mutated_through_a_cast()
    {
        InstalledExtensionList.TryBuild([Installed("alpha")], out var list, out _);

        Assert.False(list is IList<InstalledExtension> { IsReadOnly: false });
    }
}
