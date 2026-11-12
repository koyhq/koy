using Koy.Core.Rules;

namespace Koy.Core.Tests.Rules;

public class ContextProfileSwitcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Stable = TimeSpan.FromSeconds(1);

    private static RuleContext Ctx(string? process) => new(process, new TimeOnly(12, 0), 1, false);

    private static AutoRule Rule(string process, string profile, bool enabled = true) =>
        new("r-" + process, profile, enabled, [new ProcessCondition(process)]);

    private static ContextProfileSwitcher Make(params AutoRule[] rules)
    {
        var s = new ContextProfileSwitcher(Stable);
        s.SetRules(rules);
        return s;
    }

    [Fact]
    public void NoRules_NeverActs()
    {
        var s = Make();
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("code"), T0).Kind);
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + TimeSpan.FromMinutes(1)).Kind);
    }

    [Fact]
    public void Match_WaitsForStability_ThenApplies()
    {
        var s = Make(Rule("game", "p1"));
        var first = s.Observe(Ctx("game"), T0);
        Assert.Equal(SwitchActionKind.Wait, first.Kind);
        Assert.Equal(Stable, first.Delay);

        var mid = s.Tick(T0 + TimeSpan.FromMilliseconds(400));
        Assert.Equal(SwitchActionKind.Wait, mid.Kind);
        Assert.Equal(TimeSpan.FromMilliseconds(600), mid.Delay);

        var done = s.Tick(T0 + Stable);
        Assert.Equal(new SwitchDecision(SwitchActionKind.Apply, "p1"), done);
    }

    [Fact]
    public void QuickAltTab_AwayAndBack_NeverApplies()
    {
        var s = Make(Rule("game", "p1"));
        Assert.Equal(SwitchActionKind.Wait, s.Observe(Ctx("game"), T0).Kind);
        // Eşleşmeyen uygulamaya geçildi: bekleyen eylem düşer.
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("notepad"), T0 + TimeSpan.FromMilliseconds(300)).Kind);
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + Stable).Kind);
    }

    [Fact]
    public void Flapping_RestartsTheStabilityWindow()
    {
        var s = Make(Rule("a", "pa"), Rule("b", "pb"));
        s.Observe(Ctx("a"), T0);
        s.Observe(Ctx("b"), T0 + TimeSpan.FromMilliseconds(900));
        // a'nın süresi dolsa da artık b bekliyor; b için tam süre geçmeden uygulanmaz.
        var early = s.Tick(T0 + TimeSpan.FromMilliseconds(1500));
        Assert.Equal(SwitchActionKind.Wait, early.Kind);
        Assert.Equal(new SwitchDecision(SwitchActionKind.Apply, "pb"), s.Tick(T0 + TimeSpan.FromMilliseconds(1900)));
    }

    [Fact]
    public void SameTargetAgain_DoesNotRestartTheWindow()
    {
        var s = Make(Rule("game", "p1"));
        s.Observe(Ctx("game"), T0);
        var again = s.Observe(Ctx("game"), T0 + TimeSpan.FromMilliseconds(700));
        Assert.Equal(TimeSpan.FromMilliseconds(300), again.Delay);
    }

    [Fact]
    public void ObserveAfterWindowElapsed_AppliesImmediately()
    {
        var s = Make(Rule("game", "p1"));
        s.Observe(Ctx("game"), T0);
        Assert.Equal(new SwitchDecision(SwitchActionKind.Apply, "p1"), s.Observe(Ctx("game"), T0 + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void AlreadyActiveProfile_IsNotReapplied()
    {
        var s = Make(Rule("game", "p1"));
        s.NoteApplied("p1");
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("game"), T0).Kind);
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + Stable).Kind);
    }

    [Fact]
    public void NoMatch_AfterRuleApplied_RevertsOnceStable()
    {
        var s = Make(Rule("game", "p1"));
        s.NoteApplied("p1");
        Assert.Equal(SwitchActionKind.Wait, s.Observe(Ctx("notepad"), T0).Kind);
        Assert.Equal(new SwitchDecision(SwitchActionKind.Revert), s.Tick(T0 + Stable));
        s.NoteReverted();
        Assert.False(s.IsRuleActive);
        // Geri dönüldükten sonra bir daha dönülmez.
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("notepad"), T0 + TimeSpan.FromSeconds(5)).Kind);
    }

    [Fact]
    public void NoMatch_WithoutRuleProfileActive_NeverReverts()
    {
        var s = Make(Rule("game", "p1"));
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("notepad"), T0).Kind);
    }

    [Fact]
    public void RuleToRule_AppliesSecondDirectlyWithoutRevert()
    {
        var s = Make(Rule("a", "pa"), Rule("b", "pb"));
        s.NoteApplied("pa");
        s.Observe(Ctx("b"), T0);
        Assert.Equal(new SwitchDecision(SwitchActionKind.Apply, "pb"), s.Tick(T0 + Stable));
    }

    [Fact]
    public void RevertCancelled_WhenMatchingAppReturnsBeforeStable()
    {
        var s = Make(Rule("game", "p1"));
        s.NoteApplied("p1");
        Assert.Equal(SwitchActionKind.Wait, s.Observe(Ctx("notepad"), T0).Kind);
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("game"), T0 + TimeSpan.FromMilliseconds(200)).Kind);
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + Stable).Kind);
        Assert.True(s.IsRuleActive);
    }

    [Fact]
    public void DisabledRules_AreIgnored()
    {
        var s = Make(Rule("game", "p1", enabled: false));
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("game"), T0).Kind);
    }

    [Fact]
    public void Reset_AfterManualApply_DoesNotRevert()
    {
        var s = Make(Rule("game", "p1"));
        s.NoteApplied("p1");
        s.Reset();
        Assert.Equal(SwitchActionKind.None, s.Observe(Ctx("notepad"), T0).Kind);
        Assert.Equal(SwitchActionKind.Wait, s.Observe(Ctx("game"), T0).Kind);
    }

    [Fact]
    public void SetRules_DropsPendingButKeepsActiveRuleProfile()
    {
        var s = Make(Rule("game", "p1"));
        s.Observe(Ctx("game"), T0);
        s.SetRules([Rule("game", "p1")]);
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + Stable).Kind);

        s.NoteApplied("p1");
        s.SetRules([]);
        Assert.True(s.IsRuleActive);
        // Kurallar silindi ve kural profili yürürlükte: kararlı olunca geri dön.
        s.Observe(Ctx("game"), T0);
        Assert.Equal(SwitchActionKind.Revert, s.Tick(T0 + Stable).Kind);
    }

    [Fact]
    public void NoteCalls_NeverProduceActions()
    {
        // Uygulama sonucu bildirimi yeniden değerlendirme tetiklemez: döngü yok.
        var s = Make(Rule("game", "p1"));
        s.NoteApplied("p1");
        s.NoteReverted();
        s.Reset();
        Assert.Equal(SwitchActionKind.None, s.Tick(T0 + TimeSpan.FromHours(1)).Kind);
    }

    [Fact]
    public void ApplyThenRevertCycle_ProducesExactlyOneActionEach()
    {
        var s = Make(Rule("game", "p1"));
        var actions = new List<SwitchActionKind>();
        void Feed(string process, double seconds)
        {
            var now = T0 + TimeSpan.FromSeconds(seconds);
            var d = s.Observe(Ctx(process), now);
            if (d.Kind == SwitchActionKind.Wait)
                d = s.Tick(now + d.Delay);
            if (d.Kind == SwitchActionKind.Apply)
                s.NoteApplied(d.ProfileId!);
            else if (d.Kind == SwitchActionKind.Revert)
                s.NoteReverted();
            if (d.Kind is SwitchActionKind.Apply or SwitchActionKind.Revert)
                actions.Add(d.Kind);
        }

        Feed("game", 0);
        Feed("game", 10);
        Feed("game", 20);
        Feed("notepad", 30);
        Feed("notepad", 40);
        Assert.Equal([SwitchActionKind.Apply, SwitchActionKind.Revert], actions);
    }
}
