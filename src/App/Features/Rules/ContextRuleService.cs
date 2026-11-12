using System.Windows.Threading;
using Koy.App.Features.Settings;
using Koy.Core.Rules;
using Koy.Core.Settings.Profiles;
using Koy.Platform.Display;
using Koy.Platform.Shell;
using Koy.Core.Shell;

namespace Koy.App.Features.Rules;

/// <summary>
/// Bağlam kurallarının uygulama tarafı: ön plan uygulaması değişince kuralları değerlendirir ve profili
/// <see cref="ProfileService"/> üzerinden uygular. Karar mantığı Core'daki <see cref="ContextProfileSwitcher"/>
/// ve <see cref="RuleEvaluator"/> içindedir; burası yalnız olay, ölçüm ve bağlamadır.
/// <list type="bullet">
/// <item>Etkin kural yoksa hiçbir şeye abone olunmaz (ortak pencere izleyicisi bile alınmaz). Kurallar varsayılan KAPALI eklenir.</item>
/// <item>Olay tabanlı: <c>WindowTracker.ForegroundWindowChanged</c>. Yoklama yok. Titreşim önleme için yalnız bir
/// kez çalışıp duran tek atımlık zamanlayıcı kullanılır (bekleme sürerken; bitince durur).</item>
/// <item>Döngü yok: kurallar settings.json'da değil rules.json'dadır, profil uygulamak onları değiştiremez; kendi
/// uygulamamızın <c>Applied</c> olayı yok sayılır; Koy'un kendi pencereleri ve çözülemeyen pencereler değerlendirilmez.</item>
/// <item>Geri dönüş: kural profili uygulanmadan önce, o profilin dokunduğu bölümlerin anlık görüntüsü alınır;
/// hiçbir kural eşleşmeyince aynı yoldan geri uygulanır.</item>
/// </list>
/// Hepsi arayüz iş parçacığında çalışır (profil uygulamak çentik ve dock'u yeniden kurar).
/// </summary>
public sealed class ContextRuleService
{
    public static ContextRuleService Instance { get; } = new(new RuleStore(RuleStore.DefaultPath), ProfileService.Instance);

    private readonly RuleStore _store;
    private readonly ProfileService _profiles;
    private readonly ContextProfileSwitcher _switcher = new();
    private readonly DispatcherTimer _debounce = new();

    private SharedServices? _services;
    private WindowTracker? _tracker;
    private IReadOnlyList<AutoRule> _rules = [];
    private RuleSource _needed;
    private SavedProfile? _snapshot;
    private FullscreenDetector? _fullscreen;
    private bool _applying;

    public ContextRuleService(RuleStore store, ProfileService profiles)
    {
        _store = store;
        _profiles = profiles;
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Handle(_switcher.Tick(DateTimeOffset.UtcNow));
        };
    }

    /// <summary>Kuralların ham listesi (arayüz için).</summary>
    public IReadOnlyList<RuleDraft> LoadDrafts() => _store.LoadDrafts();

    /// <summary>Uygulama açılışında bir kez. Etkin kural yoksa hiçbir şey kurulmaz.</summary>
    public void Start(SharedServices services)
    {
        _services = services;
        Reload();
    }

    /// <summary>Uygulama çıkarken: abonelik ve zamanlayıcı bırakılır. Ayarlara dokunulmaz.</summary>
    public void Stop()
    {
        Unsubscribe();
        _services = null;
    }

    /// <summary>Listeyi düzenler, kaydeder ve değişikliği uygular. Kaydedilemezse <c>false</c>.</summary>
    public (RuleEditResult Result, bool Saved) Edit(Func<IReadOnlyList<RuleDraft>, RuleEditResult> edit)
    {
        var result = edit(_store.LoadDrafts());
        if (!result.IsSuccess)
            return (result, false);
        if (!_store.Save(result.Rules))
            return (result, false);
        Reload();
        return (result, true);
    }

    private void Reload()
    {
        _rules = _store.LoadRules();
        _needed = RuleSources.Needed(_rules);
        _switcher.SetRules(_rules);

        if (_needed == RuleSource.None || _services is null)
        {
            // Etkin kural kalmadı: kullanıcının yeni durumu geçerli, geri dönülecek bir şey yok.
            Unsubscribe();
            return;
        }

        if (_tracker is null)
        {
            _tracker = _services.AcquireWindowTracker();
            _tracker.ForegroundWindowChanged += OnForegroundWindowChanged;
            _profiles.Applied += OnProfileApplied;
            _tracker.Start();
        }
    }

    private void Unsubscribe()
    {
        _debounce.Stop();
        _switcher.Reset();
        _snapshot = null;
        _fullscreen = null;

        if (_tracker is null)
            return;
        _tracker.ForegroundWindowChanged -= OnForegroundWindowChanged;
        _profiles.Applied -= OnProfileApplied;
        _tracker = null;
        _services?.ReleaseWindowTracker();
    }

    private void OnForegroundWindowChanged(object? sender, WindowInfo info)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() => Observe(info));
    }

    // Kullanıcı profili kendisi uyguladı (kendi uygulamamız değil): kural yürürlükten düşer, kullanıcının seçimi ezilmez.
    private void OnProfileApplied(SavedProfile profile)
    {
        if (_applying)
            return;
        _debounce.Stop();
        _switcher.Reset();
        _snapshot = null;
    }

    private void Observe(WindowInfo info)
    {
        // Kapandıktan sonra gelen ya da kuralsız kalan olay; Koy'un kendi penceresi (ayarlar, menü) ve masaüstü/çözülemeyen
        // pencere "eşleşme yok" demek değildir, durum korunur.
        if (_tracker is null || info.Handle == 0 || WindowStyles.GetWindowProcessId(info.Handle) == (uint)Environment.ProcessId)
            return;
        var process = WindowStyles.GetWindowProcessName(info.Handle);
        if (process is null)
            return;

        var context = new RuleContext(
            process,
            TimeOnly.FromDateTime(DateTime.Now),
            _needed.HasFlag(RuleSource.DisplayCount) ? CountDisplays() : 1,
            _needed.HasFlag(RuleSource.Fullscreen) && IsFullscreen());

        Handle(_switcher.Observe(context, DateTimeOffset.UtcNow));
    }

    private void Handle(SwitchDecision decision)
    {
        switch (decision.Kind)
        {
            case SwitchActionKind.Wait:
                _debounce.Stop();
                _debounce.Interval = decision.Delay;
                _debounce.Start();
                break;
            case SwitchActionKind.Apply:
                _debounce.Stop();
                ApplyRule(decision.ProfileId!);
                break;
            case SwitchActionKind.Revert:
                _debounce.Stop();
                Revert();
                break;
            default:
                _debounce.Stop();
                break;
        }
    }

    private void ApplyRule(string profileId)
    {
        var profile = _profiles.Find(profileId);
        if (profile is null)
            return; // Profil silinmiş: kural etkisiz, durum değişmez.

        // Geri dönüş anlık görüntüsü yalnız kural dışı durumdan kurala geçerken alınır; kuraldan kurala geçişte ilk korunur.
        var newSnapshot = false;
        if (!_switcher.IsRuleActive)
        {
            _snapshot = _profiles.Snapshot(SectionsTouchedByRules());
            newSnapshot = true;
        }

        if (!ApplyGuarded(() => _profiles.Apply(profile.Id)))
        {
            if (newSnapshot)
                _snapshot = null;
            return;
        }

        _switcher.NoteApplied(profileId);
    }

    private void Revert()
    {
        var snapshot = _snapshot;
        _snapshot = null;
        if (snapshot is not null)
            ApplyGuarded(() => _profiles.ApplyUnsaved(snapshot));
        _switcher.NoteReverted();
    }

    private bool ApplyGuarded(Func<ProfileApplyResult> apply)
    {
        _applying = true;
        try
        {
            return apply().IsSuccess;
        }
        finally
        {
            _applying = false;
        }
    }

    // Etkin kuralların profillerinin dokunduğu bölümlerin birleşimi: kuraldan kurala geçişte de hepsi geri alınabilir.
    private IReadOnlyList<ProfileSection> SectionsTouchedByRules() =>
        _rules.Where(r => r.Enabled)
            .Select(r => _profiles.Find(r.ProfileId))
            .Where(p => p is not null)
            .SelectMany(p => ProfileLibrary.SectionsOf(p!))
            .Distinct()
            .ToList();

    private static int CountDisplays()
    {
        using var monitors = new MonitorService();
        return monitors.GetMonitors().Count;
    }

    private bool IsFullscreen() => (_fullscreen ??= new FullscreenDetector()).IsFullscreen();
}
