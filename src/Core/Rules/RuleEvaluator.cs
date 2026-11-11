namespace Koy.Core.Rules;

/// <summary>
/// Bir andaki bağlam görüntüsü. Ölçüm (süreç izleme, saat, ekran sayımı) Core dışında yapılır.
/// <paramref name="ProcessName"/> yalnız dosya adı olmalı (yol değil; yol içeren ad hiçbir süreç koşuluyla eşleşmez).
/// <paramref name="LocalTime"/> UTC değil, makinenin yerel saatidir; saat dilimi ve yaz saati dönüşümü çağıranın işi.
/// </summary>
public sealed record RuleContext(string? ProcessName, TimeOnly LocalTime, int DisplayCount, bool IsFullscreen);

/// <summary>
/// Saf karar mantığı: kuralları listedeki sırayla dener. Etkin ve bütün koşulları sağlayan
/// ilk kuralın profil kimliğini döner; hiçbiri eşleşmezse null. Sıra belirlidir, aynı girdi
/// her zaman aynı sonucu verir. Çelişen kurallarda öncelik yalnız liste sırasıdır.
/// Koşulsuz kural ve tanınmayan koşul türü eşleşmez (fail-closed).
/// </summary>
public static class RuleEvaluator
{
    public static string? Evaluate(IReadOnlyList<AutoRule> rules, RuleContext context)
    {
        var process = RuleSetValidator.NormalizeProcessName(context.ProcessName);
        foreach (var rule in rules)
        {
            if (!rule.Enabled || rule.Conditions.Count == 0)
                continue;
            if (rule.Conditions.All(condition => Matches(condition, context, process)))
                return rule.ProfileId;
        }
        return null;
    }

    private static bool Matches(RuleCondition condition, RuleContext context, string? process) => condition switch
    {
        ProcessCondition p => process is not null && RuleSetValidator.NormalizeProcessName(p.ProcessName) == process,
        TimeWindowCondition w => InWindow(context.LocalTime, w.From, w.To),
        DisplayCountCondition d => context.DisplayCount == d.Count,
        FullscreenCondition f => context.IsFullscreen == f.IsFullscreen,
        _ => false,
    };

    /// <summary>From dahil, To hariç. From > To ise aralık gece yarısını aşar (ör. 22:00-06:00). From == To belirsizdir, eşleşmez.</summary>
    private static bool InWindow(TimeOnly now, TimeOnly from, TimeOnly to)
    {
        if (from == to)
            return false;
        return from < to ? now >= from && now < to : now >= from || now < to;
    }
}
