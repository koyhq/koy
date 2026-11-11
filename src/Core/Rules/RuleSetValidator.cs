using System.Globalization;

namespace Koy.Core.Rules;

/// <summary>Doğrulama sonucu: geçerli kurallar (sırası korunmuş) ve atılan öğelerin sayısı.</summary>
public sealed record RuleSetValidation(IReadOnlyList<AutoRule> Rules, int DroppedRules, int DroppedConditions);

/// <summary>
/// Ham kuralları doğrular. Kurala fail-closed davranılır: bir kuralın koşullarından biri bile geçersiz
/// ya da bilinmeyen türdeyse kural tamamen atılır, kalan koşullarıyla tutulmaz. Geçerli koşullar
/// kalsa da kural ancak tam hâliyle anlamlıdır. Koşulsuz kural her şeyle eşleşeceği için de kabul
/// edilmez. Fazla kurallar (üst sınır) listenin sonundan atılır ve yine raporlanır.
/// </summary>
public static class RuleSetValidator
{
    public static RuleSetValidation Validate(IReadOnlyList<RuleDraft> drafts)
    {
        var rules = new List<AutoRule>();
        var droppedRules = 0;
        var droppedConditions = 0;

        foreach (var draft in drafts)
        {
            var name = draft.Name?.Trim();
            var profileId = draft.ProfileId?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > RuleLimits.MaxNameLength || string.IsNullOrEmpty(profileId))
            {
                droppedRules++;
                continue;
            }

            var conditions = new List<RuleCondition>();
            var hasInvalidCondition = false;
            foreach (var conditionDraft in draft.Conditions)
            {
                var condition = ParseCondition(conditionDraft);
                if (condition is null)
                {
                    droppedConditions++;
                    hasInvalidCondition = true;
                }
                else
                {
                    conditions.Add(condition);
                }
            }

            // Bir koşul bile geçersizse kalanlarla kurmak yanlış tetiklemeye yol açar:
            // "süreç=oyun VE saat=25:00-06:00" yalnız "süreç=oyun" olursa gün boyu uygulanır.
            if (hasInvalidCondition || conditions.Count == 0 || rules.Count >= RuleLimits.MaxRules)
            {
                droppedRules++;
                continue;
            }

            rules.Add(new AutoRule(name, profileId, draft.Enabled, conditions));
        }

        return new RuleSetValidation(rules, droppedRules, droppedConditions);
    }

    /// <summary>
    /// Süreç adını karşılaştırılabilir hâle getirir: kırpılmış, küçük harf, sonundaki .exe atılmış.
    /// Yol ayırıcı içeren ya da boş ad geçersizdir (süreç adı bir dosya adıdır).
    /// </summary>
    public static string? NormalizeProcessName(string? raw)
    {
        var name = raw?.Trim().ToLowerInvariant();
        if (name is null)
            return null;
        if (name.EndsWith(".exe", StringComparison.Ordinal))
            name = name[..^4];
        if (name.Length == 0 || name.IndexOfAny(['\\', '/', ':']) >= 0)
            return null;
        return name;
    }

    private static RuleCondition? ParseCondition(RuleConditionDraft draft)
    {
        switch (draft.Kind?.Trim().ToLowerInvariant())
        {
            case "process":
            {
                var process = NormalizeProcessName(draft.ProcessName);
                return process is null ? null : new ProcessCondition(process);
            }
            case "timewindow":
            {
                // Başlangıç ve bitiş aynıysa aralık boştur ya da tüm gün olur; belirsiz olduğu için atılır.
                if (TryParseTime(draft.From, out var from) && TryParseTime(draft.To, out var to) && from != to)
                    return new TimeWindowCondition(from, to);
                return null;
            }
            case "displaycount":
            {
                if (draft.DisplayCount is >= 1 and <= RuleLimits.MaxDisplayCount)
                    return new DisplayCountCondition(draft.DisplayCount.Value);
                return null;
            }
            case "fullscreen":
            {
                return draft.IsFullscreen is { } fullscreen ? new FullscreenCondition(fullscreen) : null;
            }
            default:
                return null;
        }
    }

    private static bool TryParseTime(string? raw, out TimeOnly time) =>
        TimeOnly.TryParseExact(raw, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
