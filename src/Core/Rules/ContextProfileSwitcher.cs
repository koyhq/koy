namespace Koy.Core.Rules;

public enum SwitchActionKind
{
    /// <summary>Yapılacak bir şey yok.</summary>
    None,

    /// <summary>Karar henüz kararlı değil; <see cref="SwitchDecision.Delay"/> sonra <see cref="ContextProfileSwitcher.Tick"/> çağrılmalı.</summary>
    Wait,

    /// <summary>Kuralın profili uygulanmalı.</summary>
    Apply,

    /// <summary>Kural artık eşleşmiyor; kuraldan önceki duruma dönülmeli.</summary>
    Revert,
}

public readonly record struct SwitchDecision(SwitchActionKind Kind, string? ProfileId = null, TimeSpan Delay = default)
{
    public static SwitchDecision None { get; } = new(SwitchActionKind.None);
}

/// <summary>
/// Bağlam kurallarının "ne zaman profil değiştirilir" kararı. Saf ve durumlu; saat, zamanlayıcı ya da pencere yok,
/// zaman dışarıdan verilir. Üç güvence:
/// <list type="bullet">
/// <item>Titreşim önleme: bir eylem ancak aynı eylem <c>stableFor</c> süre kesintisiz istenirse verilir; hızlı alt-tab
/// bekleyen eylemi iptal eder (ön plan eski duruma dönünce eylem kalmaz), çentik ve dock boşuna yeniden kurulmaz.</item>
/// <item>Aynı profil iki kez uygulanmaz: kuralla uygulanmış profil yürürlükteyken aynı hedef için eylem üretilmez.</item>
/// <item>Geri dönüş: hiçbir kural eşleşmiyorsa ve bir kural profili yürürlükteyse <see cref="SwitchActionKind.Revert"/>;
/// kural yokken ya da kural profili yürürlükte değilken asla (kullanıcının kendi ayarına dokunulmaz).</item>
/// </list>
/// Döngü yok: yalnız <see cref="Observe"/> (dışarıdan gelen ön plan olayı) karar üretir, <c>Note*</c> çağrıları üretmez.
/// Bu sınıf kural listesini değiştirmez; profil uygulamak kuralları değiştiremez (kurallar ayrı dosyada).
/// "Neye dönülecek"in kendisi (ayar anlık görüntüsü) çağıranda tutulur; burada yalnız dönülüp dönülmeyeceği bilinir.
/// </summary>
public sealed class ContextProfileSwitcher(TimeSpan stableFor)
{
    public static readonly TimeSpan DefaultStableFor = TimeSpan.FromSeconds(1);

    private IReadOnlyList<AutoRule> _rules = [];
    private SwitchActionKind _pendingKind;
    private string? _pendingProfile;
    private DateTimeOffset _pendingSince;

    public ContextProfileSwitcher() : this(DefaultStableFor)
    {
    }

    /// <summary>Kural tarafından uygulanmış ve hâlâ yürürlükte olan profil; yoksa null.</summary>
    public string? ActiveRuleProfileId { get; private set; }

    public bool IsRuleActive => ActiveRuleProfileId is not null;

    /// <summary>Kural listesi değişti. Bekleyen eylem düşer; yürürlükteki kural profili bilgisi korunur.</summary>
    public void SetRules(IReadOnlyList<AutoRule> rules)
    {
        _rules = rules;
        ClearPending();
    }

    /// <summary>Yeni bir ön plan / bağlam görüntüsü. Eylem varsa ya hemen (kararlıysa) ya da bekleme olarak döner.</summary>
    public SwitchDecision Observe(RuleContext context, DateTimeOffset now)
    {
        var target = RuleEvaluator.Evaluate(_rules, context);

        SwitchActionKind kind;
        if (target is not null)
            kind = string.Equals(target, ActiveRuleProfileId, StringComparison.Ordinal) ? SwitchActionKind.None : SwitchActionKind.Apply;
        else
            kind = IsRuleActive ? SwitchActionKind.Revert : SwitchActionKind.None;

        if (kind == SwitchActionKind.None)
        {
            ClearPending();
            return SwitchDecision.None;
        }

        var profile = kind == SwitchActionKind.Apply ? target : null;
        if (_pendingKind != kind || !string.Equals(_pendingProfile, profile, StringComparison.Ordinal))
        {
            _pendingKind = kind;
            _pendingProfile = profile;
            _pendingSince = now;
        }

        return Resolve(now);
    }

    /// <summary>Bekleme süresi dolduğunda çağrılır. Hâlâ erkense yine bekleme, bekleyen yoksa None döner.</summary>
    public SwitchDecision Tick(DateTimeOffset now) => _pendingKind == SwitchActionKind.None ? SwitchDecision.None : Resolve(now);

    /// <summary>Kuralın profili uygulandı: yürürlüğe girdi.</summary>
    public void NoteApplied(string profileId)
    {
        ActiveRuleProfileId = profileId;
        ClearPending();
    }

    /// <summary>Kuraldan önceki duruma dönüldü.</summary>
    public void NoteReverted()
    {
        ActiveRuleProfileId = null;
        ClearPending();
    }

    /// <summary>
    /// Kullanıcı profili kendisi uyguladı (ya da kurallar kapandı): kural yürürlükte sayılmaz, geri dönüş yapılmaz,
    /// kullanıcının seçimi ezilmez.
    /// </summary>
    public void Reset()
    {
        ActiveRuleProfileId = null;
        ClearPending();
    }

    private SwitchDecision Resolve(DateTimeOffset now)
    {
        var remaining = stableFor - (now - _pendingSince);
        if (remaining > TimeSpan.Zero)
            return new SwitchDecision(SwitchActionKind.Wait, Delay: remaining);

        var decision = new SwitchDecision(_pendingKind, _pendingProfile);
        ClearPending();
        return decision;
    }

    private void ClearPending()
    {
        _pendingKind = SwitchActionKind.None;
        _pendingProfile = null;
    }
}
