namespace Koy.Core.Rules;

public enum RuleEditError
{
    None,
    InvalidProcess,
    NoProfile,
    LimitReached,
    Duplicate,
    NotFound,
}

public sealed record RuleEditResult(RuleEditError Error, IReadOnlyList<RuleDraft> Rules)
{
    public bool IsSuccess => Error == RuleEditError.None;
}

/// <summary>
/// Kural listesi düzenlemesi (arayüz için): ekle, aç/kapat, sil. Liste sırası önceliktir, yeni kural sona eklenir.
/// Yeni kural KAPALI eklenir; kullanıcı açana kadar hiçbir şey uygulanmaz. Dosyada elle yazılmış başka koşullu
/// kurallara dokunulmaz, yalnız gösterilir / açılıp kapatılır / silinir.
/// </summary>
public static class RuleListEditor
{
    public static RuleEditResult AddProcessRule(IReadOnlyList<RuleDraft> rules, string? processName, string? profileId)
    {
        var process = RuleSetValidator.NormalizeProcessName(processName);
        if (process is null)
            return Fail(RuleEditError.InvalidProcess, rules);
        var profile = profileId?.Trim();
        if (string.IsNullOrEmpty(profile) || profile.Length > RuleLimits.MaxProfileIdLength)
            return Fail(RuleEditError.NoProfile, rules);
        if (rules.Count >= RuleLimits.MaxRules)
            return Fail(RuleEditError.LimitReached, rules);
        // Aynı uygulama için ikinci kural hiç tetiklenmezdi (ilk eşleşen kazanır).
        if (rules.Any(r => ProcessOf(r) == process))
            return Fail(RuleEditError.Duplicate, rules);

        var draft = new RuleDraft(process, profile, false, [new RuleConditionDraft("process", ProcessName: process)]);
        return new RuleEditResult(RuleEditError.None, [.. rules, draft]);
    }

    public static RuleEditResult SetEnabled(IReadOnlyList<RuleDraft> rules, int index, bool enabled)
    {
        if (index < 0 || index >= rules.Count)
            return Fail(RuleEditError.NotFound, rules);
        var copy = rules.ToList();
        copy[index] = copy[index] with { Enabled = enabled };
        return new RuleEditResult(RuleEditError.None, copy);
    }

    public static RuleEditResult Delete(IReadOnlyList<RuleDraft> rules, int index)
    {
        if (index < 0 || index >= rules.Count)
            return Fail(RuleEditError.NotFound, rules);
        var copy = rules.ToList();
        copy.RemoveAt(index);
        return new RuleEditResult(RuleEditError.None, copy);
    }

    /// <summary>Kuralın tek süreç koşulu varsa normalleştirilmiş süreç adı; aksi hâlde null (gelişmiş kural).</summary>
    public static string? ProcessOf(RuleDraft rule)
    {
        if (rule.Conditions is not { Count: 1 } conditions || conditions[0] is not { } c
            || !string.Equals(c.Kind?.Trim(), "process", StringComparison.OrdinalIgnoreCase))
            return null;
        return RuleSetValidator.NormalizeProcessName(c.ProcessName);
    }

    private static RuleEditResult Fail(RuleEditError error, IReadOnlyList<RuleDraft> rules) => new(error, rules);
}
