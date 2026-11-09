namespace Koy.Core.Shortcuts;

public enum ShortcutStatus
{
    /// <summary>Atanmamış: kayıt yapılmadı, hata değil.</summary>
    Unassigned,
    Registered,

    /// <summary>Metin çözülemedi ya da tuş kısayol olamıyor.</summary>
    Invalid,

    /// <summary>Windows'un hiçbir uygulamaya bırakmadığı kısayol (Win+L, Ctrl+Alt+Del).</summary>
    ReservedBySystem,

    /// <summary>Aynı kombinasyon başka bir Koy eylemine atanmış; bu eylem kaydedilmedi.</summary>
    DuplicateOfOtherAction,

    /// <summary>Başka bir uygulama kısayolu tutuyor (RegisterHotKey reddetti).</summary>
    InUseByOtherApp,

    Failed
}

public sealed record ShortcutResult(ShortcutStatus Status, ShortcutAction? ConflictsWith = null)
{
    public bool IsError => Status is not (ShortcutStatus.Registered or ShortcutStatus.Unassigned);
}

/// <summary>Kısayol kombinasyonu için ön denetimler (kayıt denemeden önce).</summary>
public static class ShortcutConflicts
{
    /// <summary>Kancayla bile ezilemeyen kombinasyonlar: bunları Windows, uygulamalardan önce işler.</summary>
    public static bool IsReservedBySystem(Hotkey hotkey)
    {
        var m = hotkey.Modifiers;
        var ctrlAlt = (m & (HotkeyModifiers.Control | HotkeyModifiers.Alt)) == (HotkeyModifiers.Control | HotkeyModifiers.Alt);
        return (m == HotkeyModifiers.Win && hotkey.VirtualKey == 0x4C) || (ctrlAlt && hotkey.VirtualKey == 0x2E);
    }

    /// <summary>Windows'un kullandığı ama kancayla ezilebilen kombinasyonlar; RegisterHotKey bunları alamaz.</summary>
    public static bool IsWindowsOwned(Hotkey hotkey)
    {
        var m = hotkey.Modifiers;
        var vk = hotkey.VirtualKey;
        var win = (m & HotkeyModifiers.Win) != 0;
        var alt = (m & HotkeyModifiers.Alt) != 0;
        var ctrl = (m & HotkeyModifiers.Control) != 0;

        // Alt+Tab, Alt+Esc, Alt+F4, Alt+Space (pencere menüsü)
        if (alt && !ctrl && !win && vk is 0x09 or 0x1B or 0x73 or 0x20)
            return true;

        // Ctrl+Alt+Tab, Win+Tab
        if (vk == 0x09 && (win || (ctrl && alt)))
            return true;

        // Ctrl+Esc (Başlat), Ctrl+Shift+Esc (Görev Yöneticisi)
        if (ctrl && !alt && !win && vk == 0x1B)
            return true;

        // Yalnız Win + harf/rakam/boşluk: Windows neredeyse hepsini kullanır (D, E, L, R, K, A, S, V...).
        if (m == HotkeyModifiers.Win && vk is (>= 0x41 and <= 0x5A) or (>= 0x30 and <= 0x39) or 0x20)
            return true;

        // Win+Shift+S (ekran alıntısı), Win+Shift+M
        if (m == (HotkeyModifiers.Win | HotkeyModifiers.Shift) && vk is 0x53 or 0x4D)
            return true;

        return false;
    }

    /// <summary>
    /// Kullanıcının seçtiği kombinasyon kaydedilmeden önce denetlenir: sistem kısayolu ya da başka eylemle çakışma.
    /// Sorun yoksa null döner.
    /// </summary>
    public static ShortcutResult? Check(Hotkey hotkey, ShortcutAction forAction, IReadOnlyDictionary<ShortcutAction, string> current)
    {
        if (!hotkey.IsValid)
            return new ShortcutResult(ShortcutStatus.Invalid);
        if (IsReservedBySystem(hotkey))
            return new ShortcutResult(ShortcutStatus.ReservedBySystem);

        foreach (var (action, text) in current)
        {
            if (action != forAction && Hotkey.TryParse(text, out var other) && other == hotkey)
                return new ShortcutResult(ShortcutStatus.DuplicateOfOtherAction, action);
        }

        return null;
    }
}

/// <summary>
/// Ayarlardaki kısayolları <see cref="IHotkeyService"/> üzerinden kaydeder, basılınca eylemi bildirir.
/// Saf mantık: gerçek kayıt platform servisindedir, bu yüzden sahte servisle test edilir.
/// </summary>
public sealed class ShortcutManager : IDisposable
{
    private readonly IHotkeyService _service;
    private readonly IKeyboardHookService? _hook;
    private readonly HashSet<ShortcutAction> _registered = [];
    private readonly HashSet<ShortcutAction> _hooked = [];
    private bool _disposed;

    public event EventHandler<ShortcutAction>? Triggered;

    public ShortcutManager(IHotkeyService service, IKeyboardHookService? hook = null)
    {
        _service = service;
        _hook = hook;
        _service.HotkeyPressed += OnHotkeyPressed;
        if (_hook is not null)
            _hook.HotkeyPressed += OnHookPressed;
    }

    private static int IdOf(ShortcutAction action) => (int)action + 1;

    /// <summary>
    /// Önce bütün kayıtları kaldırır, sonra istenen kısayolları sırayla kaydeder.
    /// Aynı kombinasyonu iki eylem isterse ilk sıradaki kazanır, sonraki <see cref="ShortcutStatus.DuplicateOfOtherAction"/> olur.
    /// </summary>
    public IReadOnlyDictionary<ShortcutAction, ShortcutResult> Apply(IReadOnlyDictionary<ShortcutAction, string> desired)
    {
        UnregisterAll();

        var results = new Dictionary<ShortcutAction, ShortcutResult>();
        var taken = new Dictionary<Hotkey, ShortcutAction>();
        var hookBindings = new Dictionary<int, Hotkey>();

        foreach (var action in ShortcutBindings.All)
        {
            desired.TryGetValue(action, out var text);
            if (string.IsNullOrWhiteSpace(text))
            {
                results[action] = new ShortcutResult(ShortcutStatus.Unassigned);
                continue;
            }

            if (!Hotkey.TryParse(text, out var hotkey))
            {
                results[action] = new ShortcutResult(ShortcutStatus.Invalid);
                continue;
            }

            if (ShortcutConflicts.IsReservedBySystem(hotkey))
            {
                results[action] = new ShortcutResult(ShortcutStatus.ReservedBySystem);
                continue;
            }

            if (taken.TryGetValue(hotkey, out var owner))
            {
                results[action] = new ShortcutResult(ShortcutStatus.DuplicateOfOtherAction, owner);
                continue;
            }

            if (NeedsHook(hotkey))
            {
                results[action] = UseHook(action, hotkey, hookBindings, taken);
                continue;
            }

            var registration = _service.Register(IdOf(action), hotkey);

            // Win'li bir kombinasyonu tutan büyük olasılıkla Windows'tur: kullanıcının seçimi kazanır.
            if (registration == HotkeyRegistrationResult.AlreadyInUse && _hook is not null && (hotkey.Modifiers & HotkeyModifiers.Win) != 0)
            {
                results[action] = UseHook(action, hotkey, hookBindings, taken);
                continue;
            }

            results[action] = registration switch
            {
                HotkeyRegistrationResult.Success => new ShortcutResult(ShortcutStatus.Registered),
                HotkeyRegistrationResult.AlreadyInUse => new ShortcutResult(ShortcutStatus.InUseByOtherApp),
                _ => new ShortcutResult(ShortcutStatus.Failed)
            };

            if (registration == HotkeyRegistrationResult.Success)
            {
                taken[hotkey] = action;
                _registered.Add(action);
            }
        }

        if (_hook is not null && !_hook.SetBindings(hookBindings))
        {
            foreach (var action in _hooked)
                results[action] = new ShortcutResult(ShortcutStatus.Failed);
            _hooked.Clear();
        }

        return results;
    }

    private bool NeedsHook(Hotkey hotkey) =>
        hotkey.IsModifierOnly || (_hook is not null && ShortcutConflicts.IsWindowsOwned(hotkey));

    private ShortcutResult UseHook(ShortcutAction action, Hotkey hotkey, Dictionary<int, Hotkey> bindings, Dictionary<Hotkey, ShortcutAction> taken)
    {
        if (_hook is null)
            return new ShortcutResult(ShortcutStatus.Failed);

        bindings[IdOf(action)] = hotkey;
        taken[hotkey] = action;
        _hooked.Add(action);
        return new ShortcutResult(ShortcutStatus.Registered);
    }

    private void UnregisterAll()
    {
        foreach (var action in _registered)
            _service.Unregister(IdOf(action));
        _registered.Clear();
        _hooked.Clear();
    }

    private void OnHookPressed(object? sender, int id)
    {
        var action = (ShortcutAction)(id - 1);
        if (_hooked.Contains(action))
            Triggered?.Invoke(this, action);
    }

    private void OnHotkeyPressed(object? sender, int id)
    {
        var action = (ShortcutAction)(id - 1);
        if (_registered.Contains(action))
            Triggered?.Invoke(this, action);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _service.HotkeyPressed -= OnHotkeyPressed;
        UnregisterAll();
        _service.Dispose();
        if (_hook is not null)
        {
            _hook.HotkeyPressed -= OnHookPressed;
            _hook.Dispose();
        }
    }
}
