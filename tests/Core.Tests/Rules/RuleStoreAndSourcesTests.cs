using Koy.Core.Rules;

namespace Koy.Core.Tests.Rules;

public sealed class RuleStoreAndSourcesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-rules-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    [Fact]
    public void MissingFile_MeansNoRules()
    {
        var store = new RuleStore(Path.Combine(_dir, "rules.json"));
        Assert.Empty(store.LoadDrafts());
        Assert.Empty(store.LoadRules());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsConditionsInOrder()
    {
        var store = new RuleStore(Path.Combine(_dir, "rules.json"));
        var drafts = new List<RuleDraft>
        {
            new("one", "p1", true, [new RuleConditionDraft("process", ProcessName: "game")]),
            new("two", "p2", false,
            [
                new RuleConditionDraft("timeWindow", From: "22:00", To: "06:00"),
                new RuleConditionDraft("displayCount", DisplayCount: 2),
                new RuleConditionDraft("fullscreen", IsFullscreen: true),
            ]),
        };
        Assert.True(store.Save(drafts));

        var rules = store.LoadRules();
        Assert.Equal(["p1", "p2"], rules.Select(r => r.ProfileId));
        Assert.True(rules[0].Enabled);
        Assert.False(rules[1].Enabled);
        Assert.Equal(3, rules[1].Conditions.Count);
    }

    [Fact]
    public void CorruptFile_YieldsNoRulesWithoutThrowing()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "rules.json");
        File.WriteAllText(path, "{ not json");
        var store = new RuleStore(path);
        Assert.Empty(store.LoadRules());
    }

    [Fact]
    public void Sources_NoneWhenNoEnabledRules()
    {
        Assert.Equal(RuleSource.None, RuleSources.Needed([]));
        var disabled = new AutoRule("a", "p", false, [new ProcessCondition("game")]);
        Assert.Equal(RuleSource.None, RuleSources.Needed([disabled]));
    }

    [Fact]
    public void Sources_UnionOfEnabledRuleConditions()
    {
        var a = new AutoRule("a", "p", true, [new ProcessCondition("game"), new FullscreenCondition(true)]);
        var b = new AutoRule("b", "p", true, [new DisplayCountCondition(2)]);
        var c = new AutoRule("c", "p", false, [new TimeWindowCondition(new TimeOnly(1, 0), new TimeOnly(2, 0))]);
        Assert.Equal(RuleSource.Process | RuleSource.Fullscreen | RuleSource.DisplayCount, RuleSources.Needed([a, b, c]));
    }
}
