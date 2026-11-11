using Koy.Core.Rules;

namespace Koy.Core.Tests.Rules;

public class RuleEvaluatorTests
{
    private static readonly TimeOnly Noon = new(12, 0);

    private static RuleContext Context(
        string? process = "code",
        TimeOnly? time = null,
        int displays = 1,
        bool fullscreen = false) =>
        new(process, time ?? Noon, displays, fullscreen);

    private static AutoRule Rule(string profile, params RuleCondition[] conditions) =>
        new($"kural-{profile}", profile, true, conditions);

    [Fact]
    public void NoRules_ReturnsNull()
    {
        Assert.Null(RuleEvaluator.Evaluate([], Context()));
    }

    [Theory]
    [InlineData("code")]
    [InlineData("Code.exe")]
    [InlineData("  CODE  ")]
    public void ProcessCondition_MatchesCaseInsensitivelyIgnoringExeSuffix(string contextProcess)
    {
        var rules = new[] { Rule("work", new ProcessCondition("code")) };

        Assert.Equal("work", RuleEvaluator.Evaluate(rules, Context(process: contextProcess)));
    }

    [Fact]
    public void ProcessCondition_WithoutForegroundProcess_DoesNotMatch()
    {
        var rules = new[] { Rule("work", new ProcessCondition("code")) };

        Assert.Null(RuleEvaluator.Evaluate(rules, Context(process: null)));
    }

    [Fact]
    public void FirstMatchingRule_WinsByListOrder()
    {
        var rules = new[]
        {
            Rule("first", new ProcessCondition("code")),
            Rule("second", new ProcessCondition("code")),
        };

        Assert.Equal("first", RuleEvaluator.Evaluate(rules, Context()));
    }

    [Fact]
    public void DisabledRule_IsSkipped()
    {
        var disabled = new AutoRule("kapali", "first", false, [new ProcessCondition("code")]);
        var rules = new[] { disabled, Rule("second", new ProcessCondition("code")) };

        Assert.Equal("second", RuleEvaluator.Evaluate(rules, Context()));
    }

    [Fact]
    public void AllConditionsMustMatch()
    {
        var rules = new[]
        {
            Rule("work", new ProcessCondition("code"), new FullscreenCondition(true)),
        };

        Assert.Null(RuleEvaluator.Evaluate(rules, Context(fullscreen: false)));
        Assert.Equal("work", RuleEvaluator.Evaluate(rules, Context(fullscreen: true)));
    }

    [Theory]
    [InlineData(12, 0, true)]
    [InlineData(9, 0, true)]
    [InlineData(16, 59, true)]
    [InlineData(17, 0, false)]
    [InlineData(8, 59, false)]
    public void SameDayTimeWindow_StartInclusive_EndExclusive(int hour, int minute, bool expected)
    {
        var rules = new[] { Rule("mesai", new TimeWindowCondition(new TimeOnly(9, 0), new TimeOnly(17, 0))) };

        var result = RuleEvaluator.Evaluate(rules, Context(time: new TimeOnly(hour, minute)));

        Assert.Equal(expected ? "mesai" : null, result);
    }

    [Theory]
    [InlineData(23, 30, true)]
    [InlineData(22, 0, true)]
    [InlineData(3, 0, true)]
    [InlineData(6, 0, false)]
    [InlineData(12, 0, false)]
    public void MidnightSpanningTimeWindow_WrapsAround(int hour, int minute, bool expected)
    {
        var rules = new[] { Rule("uyku", new TimeWindowCondition(new TimeOnly(22, 0), new TimeOnly(6, 0))) };

        var result = RuleEvaluator.Evaluate(rules, Context(time: new TimeOnly(hour, minute)));

        Assert.Equal(expected ? "uyku" : null, result);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void DisplayCount_MatchesExactly(int displays, bool expected)
    {
        var rules = new[] { Rule("iki-ekran", new DisplayCountCondition(2)) };

        var result = RuleEvaluator.Evaluate(rules, Context(displays: displays));

        Assert.Equal(expected ? "iki-ekran" : null, result);
    }

    [Fact]
    public void FullscreenFalse_MatchesOnlyWhenNotFullscreen()
    {
        var rules = new[] { Rule("normal", new FullscreenCondition(false)) };

        Assert.Equal("normal", RuleEvaluator.Evaluate(rules, Context(fullscreen: false)));
        Assert.Null(RuleEvaluator.Evaluate(rules, Context(fullscreen: true)));
    }

    [Fact]
    public void RuleWithoutConditions_NeverMatches()
    {
        var rules = new[] { new AutoRule("bos", "work", true, []) };

        Assert.Null(RuleEvaluator.Evaluate(rules, Context()));
    }

    [Fact]
    public void Evaluation_IsDeterministic_ForSameInput()
    {
        var rules = new[]
        {
            Rule("a", new ProcessCondition("code")),
            Rule("b", new DisplayCountCondition(1)),
        };
        var context = Context();

        var results = Enumerable.Range(0, 5).Select(_ => RuleEvaluator.Evaluate(rules, context)).Distinct().ToList();

        Assert.Equal(["a"], results);
    }
}
