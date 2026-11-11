namespace Koy.Core.Rules;

/// <summary>Bir kuralın koşulu. Kuraldaki bütün koşullar birlikte sağlanmalıdır (VE).</summary>
public abstract record RuleCondition;

/// <summary>Ön plandaki süreç adı. Değer <see cref="RuleSetValidator.NormalizeProcessName"/> ile normalleştirilmiş olmalıdır.</summary>
public sealed record ProcessCondition(string ProcessName) : RuleCondition;

/// <summary>Yerel saat aralığı: From dahil, To hariç. From, To'dan büyükse aralık gece yarısını aşar.</summary>
public sealed record TimeWindowCondition(TimeOnly From, TimeOnly To) : RuleCondition;

/// <summary>Bağlı ekran sayısı tam olarak bu değere eşitse.</summary>
public sealed record DisplayCountCondition(int Count) : RuleCondition;

/// <summary>Ön plandaki pencerenin tam ekran durumu bu değere eşitse.</summary>
public sealed record FullscreenCondition(bool IsFullscreen) : RuleCondition;
