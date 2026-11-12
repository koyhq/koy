using Koy.Core.Rules;

namespace Koy.Core.Tests.Rules;

public class RuleListEditorTests
{
    [Fact]
    public void AddProcessRule_NormalizesAndAddsDisabled()
    {
        var result = RuleListEditor.AddProcessRule([], " Game.EXE ", " p1 ");
        Assert.True(result.IsSuccess);
        var rule = Assert.Single(result.Rules);
        Assert.False(rule.Enabled);
        Assert.Equal("p1", rule.ProfileId);
        Assert.Equal("game", RuleListEditor.ProcessOf(rule));

        var validated = RuleSetValidator.Validate(result.Rules);
        Assert.Single(validated.Rules);
        Assert.Equal(0, validated.DroppedRules);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C:\\x\\game.exe")]
    public void AddProcessRule_RejectsBadProcess(string? process)
    {
        Assert.Equal(RuleEditError.InvalidProcess, RuleListEditor.AddProcessRule([], process, "p").Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void AddProcessRule_RequiresProfile(string? profile)
    {
        Assert.Equal(RuleEditError.NoProfile, RuleListEditor.AddProcessRule([], "game", profile).Error);
    }

    [Fact]
    public void AddProcessRule_RejectsDuplicateProcess()
    {
        var first = RuleListEditor.AddProcessRule([], "game", "p1");
        var second = RuleListEditor.AddProcessRule(first.Rules, "GAME.exe", "p2");
        Assert.Equal(RuleEditError.Duplicate, second.Error);
        Assert.Single(second.Rules);
    }

    [Fact]
    public void AddProcessRule_RespectsLimit()
    {
        IReadOnlyList<RuleDraft> rules = [];
        for (var i = 0; i < RuleLimits.MaxRules; i++)
            rules = RuleListEditor.AddProcessRule(rules, "app" + i, "p").Rules;
        Assert.Equal(RuleEditError.LimitReached, RuleListEditor.AddProcessRule(rules, "extra", "p").Error);
    }

    [Fact]
    public void SetEnabledAndDelete_WorkByIndex_AndRejectBadIndex()
    {
        var rules = RuleListEditor.AddProcessRule([], "a", "p").Rules;
        rules = RuleListEditor.AddProcessRule(rules, "b", "p").Rules;

        var enabled = RuleListEditor.SetEnabled(rules, 1, true);
        Assert.True(enabled.Rules[1].Enabled);
        Assert.False(enabled.Rules[0].Enabled);

        var deleted = RuleListEditor.Delete(enabled.Rules, 0);
        Assert.Equal("b", RuleListEditor.ProcessOf(Assert.Single(deleted.Rules)));

        Assert.Equal(RuleEditError.NotFound, RuleListEditor.SetEnabled(rules, 5, true).Error);
        Assert.Equal(RuleEditError.NotFound, RuleListEditor.Delete(rules, -1).Error);
    }

    [Fact]
    public void ProcessOf_IsNullForAdvancedRules()
    {
        var advanced = new RuleDraft("x", "p", true,
        [
            new RuleConditionDraft("process", ProcessName: "game"),
            new RuleConditionDraft("fullscreen", IsFullscreen: true),
        ]);
        Assert.Null(RuleListEditor.ProcessOf(advanced));
    }
}
