namespace Koy.Core.Rules;

/// <summary>
/// Ham kural: arayüzden ya da dosyadan gelir, doğrulanmadan kullanılmaz.
/// Alanlar eksik ya da bozuk olabilir; <see cref="RuleSetValidator"/> bunları ayıklar.
/// </summary>
public sealed record RuleDraft(string? Name, string? ProfileId, bool Enabled, IReadOnlyList<RuleConditionDraft> Conditions);

/// <summary>
/// Ham koşul. <paramref name="Kind"/> bilinmeyen bir metin olabilir (ileri sürümden kalan dosya);
/// tanınmayan tür sessizce atılır, uygulama açılışını bozmaz.
/// Tür adları: process, timeWindow, displayCount, fullscreen (büyük-küçük harf duyarsız).
/// </summary>
public sealed record RuleConditionDraft(
    string? Kind,
    string? ProcessName = null,
    string? From = null,
    string? To = null,
    int? DisplayCount = null,
    bool? IsFullscreen = null);
