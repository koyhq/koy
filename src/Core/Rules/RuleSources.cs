namespace Koy.Core.Rules;

/// <summary>Kuralların hangi bağlam ölçümlerine ihtiyaç duyduğu. Ölçüm yalnız gerekince yapılır (AGENTS #5).</summary>
[Flags]
public enum RuleSource
{
    None = 0,
    Process = 1,
    Time = 2,
    DisplayCount = 4,
    Fullscreen = 8,
}

public static class RuleSources
{
    /// <summary>Etkin kuralların koşullarının birleşimi. Etkin kural yoksa <see cref="RuleSource.None"/> (hiçbir şey izlenmez).</summary>
    public static RuleSource Needed(IReadOnlyList<AutoRule> rules)
    {
        var needed = RuleSource.None;
        foreach (var rule in rules)
        {
            if (!rule.Enabled)
                continue;
            foreach (var condition in rule.Conditions)
            {
                needed |= condition switch
                {
                    ProcessCondition => RuleSource.Process,
                    TimeWindowCondition => RuleSource.Time,
                    DisplayCountCondition => RuleSource.DisplayCount,
                    FullscreenCondition => RuleSource.Fullscreen,
                    _ => RuleSource.None,
                };
            }
        }
        return needed;
    }
}
