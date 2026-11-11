namespace Koy.Core.Rules;

/// <summary>
/// Doğrulanmış bir kural. Listedeki sıra önceliktir: değerlendirmede ilk eşleşen kazanır.
/// <paramref name="ProfileId"/> yalnızca metin kimliğidir; profilin varlığı burada denetlenmez.
/// </summary>
public sealed record AutoRule(string Name, string ProfileId, bool Enabled, IReadOnlyList<RuleCondition> Conditions);

/// <summary>Kural kümesi sınırları.</summary>
public static class RuleLimits
{
    public const int MaxRules = 50;
    public const int MaxNameLength = 64;
    public const int MaxDisplayCount = 16;
}
