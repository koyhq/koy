using Koy.Core.Rules;

namespace Koy.Core.Tests.Rules;

public class RuleSetValidatorTests
{
    private static RuleDraft Draft(string? name = "Kod", string? profile = "work", params RuleConditionDraft[] conditions) =>
        new(name, profile, true, conditions);

    private static RuleConditionDraft Process(string? name) => new("process", ProcessName: name);

    [Fact]
    public void ValidRule_IsKept_WithTrimmedNameAndProfile()
    {
        var result = RuleSetValidator.Validate([Draft("  Kod  ", " work ", Process("Code.exe"))]);

        var rule = Assert.Single(result.Rules);
        Assert.Equal("Kod", rule.Name);
        Assert.Equal("work", rule.ProfileId);
        Assert.Equal(0, result.DroppedRules);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_IsRejected(string? name)
    {
        var result = RuleSetValidator.Validate([Draft(name, "work", Process("code"))]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void NameLongerThanLimit_IsRejected()
    {
        var name = new string('a', RuleLimits.MaxNameLength + 1);

        var result = RuleSetValidator.Validate([Draft(name, "work", Process("code"))]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void EmptyProfileId_IsRejected(string? profile)
    {
        var result = RuleSetValidator.Validate([Draft("Kod", profile, Process("code"))]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void UnknownConditionKind_IsDropped_AndWholeRuleIsDropped_FailClosed()
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("wifiSsid"), Process("code")),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void InvalidTimeWindowAlongsideValidProcess_DropsWholeRule_NotOnlyProcessCondition()
    {
        // Yalnız "süreç=oyun" kalırsa kural gün boyu tetiklenir; bu yüzden tüm kural atılmalı.
        var result = RuleSetValidator.Validate([
            Draft("Oyun", "game", Process("game"), new RuleConditionDraft("timeWindow", From: "25:00", To: "06:00")),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void DroppedRule_DoesNotAffectOtherValidRules()
    {
        var result = RuleSetValidator.Validate([
            Draft("Birinci", "work", Process("code")),
            Draft("Bozuk", "game", Process("game"), new RuleConditionDraft("battery")),
            Draft("Üçüncü", "sleep", new RuleConditionDraft("timeWindow", From: "22:00", To: "06:30")),
        ]);

        Assert.Equal(new[] { "Birinci", "Üçüncü" }, result.Rules.Select(r => r.Name).ToArray());
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void RuleWhoseOnlyConditionsAreUnknown_IsDropped_NotMadeUnconditional()
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("battery")),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void RuleWithoutConditions_IsDropped()
    {
        var result = RuleSetValidator.Validate([Draft("Kod", "work")]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void ConditionKind_IsCaseInsensitive()
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("PROCESS", ProcessName: "code")),
        ]);

        Assert.Single(result.Rules);
        Assert.Equal(0, result.DroppedConditions);
    }

    [Theory]
    [InlineData(null, "17:00")]
    [InlineData("09:00", null)]
    [InlineData("9am", "17:00")]
    [InlineData("09:00", "09:00")]
    public void InvalidOrEmptyTimeWindow_IsDropped(string? from, string? to)
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("timeWindow", From: from, To: to)),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
    }

    [Fact]
    public void ValidTimeWindow_IsParsed()
    {
        var result = RuleSetValidator.Validate([
            Draft("Gece", "sleep", new RuleConditionDraft("timeWindow", From: "22:00", To: "06:30")),
        ]);

        var condition = Assert.IsType<TimeWindowCondition>(Assert.Single(Assert.Single(result.Rules).Conditions));
        Assert.Equal(new TimeOnly(22, 0), condition.From);
        Assert.Equal(new TimeOnly(6, 30), condition.To);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(17)]
    public void DisplayCountOutOfRange_IsDropped(int count)
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("displayCount", DisplayCount: count)),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
    }

    [Fact]
    public void FullscreenWithoutValue_IsDropped()
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", new RuleConditionDraft("fullscreen")),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
    }

    [Theory]
    [InlineData("  Code.EXE ", "code")]
    [InlineData("chrome", "chrome")]
    [InlineData(".exe", null)]
    [InlineData(@"C:\apps\code.exe", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeProcessName_HandlesCaseSuffixAndPaths(string? raw, string? expected)
    {
        Assert.Equal(expected, RuleSetValidator.NormalizeProcessName(raw));
    }

    [Fact]
    public void InvalidProcessName_IsDropped()
    {
        var result = RuleSetValidator.Validate([
            Draft("Kod", "work", Process(@"C:\apps\code.exe")),
        ]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
    }

    [Fact]
    public void RulesOverLimit_AreDroppedFromTheEnd_AndOrderIsKept()
    {
        var drafts = Enumerable.Range(0, RuleLimits.MaxRules + 3)
            .Select(i => Draft($"Kural {i}", $"profil-{i}", Process($"app{i}")))
            .ToList();

        var result = RuleSetValidator.Validate(drafts);

        Assert.Equal(RuleLimits.MaxRules, result.Rules.Count);
        Assert.Equal(3, result.DroppedRules);
        Assert.Equal("profil-0", result.Rules[0].ProfileId);
        Assert.Equal($"profil-{RuleLimits.MaxRules - 1}", result.Rules[^1].ProfileId);
    }

    [Fact]
    public void RulesOverLimit_DoNotParseTheirConditions()
    {
        var drafts = Enumerable.Range(0, RuleLimits.MaxRules)
            .Select(i => Draft($"Kural {i}", "p", Process($"app{i}")))
            .Append(Draft("Fazla", "p", new RuleConditionDraft("battery")))
            .ToList();

        var result = RuleSetValidator.Validate(drafts);

        Assert.Equal(RuleLimits.MaxRules, result.Rules.Count);
        Assert.Equal(1, result.DroppedRules);
        Assert.Equal(0, result.DroppedConditions);
    }

    [Fact]
    public void TooManyConditions_DropsRule()
    {
        var conditions = Enumerable.Range(0, RuleLimits.MaxConditionsPerRule + 1)
            .Select(i => Process($"app{i}"))
            .ToArray();

        var result = RuleSetValidator.Validate([Draft("Kod", "work", conditions)]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void ConditionsAtLimit_AreKept()
    {
        var conditions = Enumerable.Range(0, RuleLimits.MaxConditionsPerRule).Select(i => Process($"app{i}")).ToArray();

        var rule = Assert.Single(RuleSetValidator.Validate([Draft("Kod", "work", conditions)]).Rules);
        Assert.Equal(RuleLimits.MaxConditionsPerRule, rule.Conditions.Count);
    }

    [Fact]
    public void OverlongProfileId_IsRejected()
    {
        var profile = new string('p', RuleLimits.MaxProfileIdLength + 1);

        var result = RuleSetValidator.Validate([Draft("Kod", profile, Process("code"))]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedRules);
    }

    [Fact]
    public void OverlongProcessName_IsDropped()
    {
        var name = new string('a', RuleLimits.MaxProcessNameLength + 1);

        var result = RuleSetValidator.Validate([Draft("Kod", "work", Process(name))]);

        Assert.Empty(result.Rules);
        Assert.Equal(1, result.DroppedConditions);
    }

    [Fact]
    public void NullDraftNullConditionsAndNullCondition_AreDroppedWithoutThrowing()
    {
        // El ile düzenlenmiş JSON'dan null liste/öğe gelebilir; tür sistemi bunu engellemez.
        var result = RuleSetValidator.Validate([
            null!,
            new RuleDraft("A", "work", true, null!),
            new RuleDraft("B", "work", true, [null!]),
            Draft("C", "work", Process("code")),
        ]);

        Assert.Equal("C", Assert.Single(result.Rules).Name);
        Assert.Equal(3, result.DroppedRules);
    }

    [Fact]
    public void TimeWindow_ToleratesSurroundingWhitespace()
    {
        var result = RuleSetValidator.Validate([
            Draft("Gece", "sleep", new RuleConditionDraft("timeWindow", From: " 22:00 ", To: "06:00	")),
        ]);

        Assert.Single(result.Rules);
    }

    [Theory]
    [InlineData("24:00", "06:00")]
    [InlineData("22:00", "24:00")]
    [InlineData("22:00", "06:60")]
    public void NonexistentClockTimes_AreDropped(string from, string to)
    {
        var result = RuleSetValidator.Validate([
            Draft("Gece", "sleep", new RuleConditionDraft("timeWindow", From: from, To: to)),
        ]);

        Assert.Empty(result.Rules);
    }

    [Fact]
    public void DisabledRule_IsKept_WithEnabledFalse()
    {
        var draft = new RuleDraft("Kod", "work", false, [Process("code")]);

        var rule = Assert.Single(RuleSetValidator.Validate([draft]).Rules);
        Assert.False(rule.Enabled);
    }
}
