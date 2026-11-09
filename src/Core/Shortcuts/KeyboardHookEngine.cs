namespace Koy.Core.Shortcuts;

/// <summary>Bir klavye olayının sonucu.</summary>
/// <param name="Swallow">Olay Windows'a iletilmesin.</param>
/// <param name="MaskModifier">Win ya da Alt bırakılmadan önce boş tuş gönderilsin; yoksa Başlat ya da menü çubuğu açılır.</param>
/// <param name="FiredId">Tetiklenen kısayolun kimliği.</param>
/// <param name="Captured">Yakalama sırasında basılan kombinasyon.</param>
public readonly record struct HookOutcome(bool Swallow, bool MaskModifier, int? FiredId, Hotkey? Captured)
{
    public static readonly HookOutcome Pass = new(false, false, null, null);
    public static readonly HookOutcome Eat = new(true, false, null, null);
}

/// <summary>
/// Düşük düzey klavye kancasının karar mantığı (Win32 yok, testli). Yalnız değiştiricilerden oluşan kısayolları
/// (tek başına Win) ve Windows'un kendi kısayollarını yakalar; Ayarlar'daki kısayol yakalamayı da yürütür.
/// <see cref="Process"/> yalnız kanca iş parçacığından çağrılır; ayar ve yakalama değişimleri başka iş parçacığından gelebilir.
/// </summary>
public sealed class KeyboardHookEngine
{
    // Kullanıcı yakalamayı yarıda bırakırsa klavye süresiz yutulmasın.
    private const long CaptureTimeoutMs = 15_000;

    private readonly HashSet<int> _heldModifiers = [];
    private readonly HashSet<int> _swallowedKeys = [];

    private IReadOnlyDictionary<Hotkey, int> _bindings = new Dictionary<Hotkey, int>();
    private long _captureStartedMs = -1;

    private HotkeyModifiers _chordPeak;
    private bool _chordHadOtherKey;
    private bool _firedInChord;
    private bool _swallowingChord;

    public void SetBindings(IReadOnlyDictionary<int, Hotkey> bindings)
    {
        var map = new Dictionary<Hotkey, int>();
        foreach (var (id, hotkey) in bindings)
            map[hotkey] = id;

        Volatile.Write(ref _bindings, map);
    }

    public void BeginCapture(long nowMs) => Volatile.Write(ref _captureStartedMs, nowMs);

    public void EndCapture() => Volatile.Write(ref _captureStartedMs, -1);

    /// <param name="vk">Sanal tuş kodu.</param>
    /// <param name="isDown">Basıldı mı, bırakıldı mı.</param>
    /// <param name="nowMs">Geçen süre (ms).</param>
    /// <param name="physicallyDown">
    /// Tuş gerçekten basılı mı. Bırakılma olayı kaçarsa (kilit ekranı, UAC) değiştirici basılı sanılmasın diye.
    /// </param>
    public HookOutcome Process(int vk, bool isDown, long nowMs, Func<int, bool>? physicallyDown = null)
    {
        var capturing = IsCapturing(nowMs);

        if (isDown && !_swallowingChord && physicallyDown is not null)
            _heldModifiers.RemoveWhere(held => held != vk && !physicallyDown(held));

        return ModifierOf(vk) is { } flag
            ? ProcessModifier(vk, flag, isDown, capturing)
            : ProcessKey(vk, isDown, capturing);
    }

    private bool IsCapturing(long nowMs)
    {
        var started = Volatile.Read(ref _captureStartedMs);
        if (started < 0)
            return false;

        if (nowMs - started <= CaptureTimeoutMs)
            return true;

        EndCapture();
        return false;
    }

    private HookOutcome ProcessModifier(int vk, HotkeyModifiers flag, bool isDown, bool capturing)
    {
        if (isDown)
        {
            // Basılı tutunca gelen tekrar.
            if (!_heldModifiers.Add(vk))
                return _swallowingChord ? HookOutcome.Eat : HookOutcome.Pass;

            if (_heldModifiers.Count == 1 && _swallowedKeys.Count == 0)
            {
                _chordPeak = HotkeyModifiers.None;
                _chordHadOtherKey = false;
                _firedInChord = false;
                _swallowingChord = capturing;
            }

            _chordPeak |= CurrentModifiers();
            return _swallowingChord ? HookOutcome.Eat : HookOutcome.Pass;
        }

        // Basılışı kancadan önce olan tuşun bırakılışı.
        if (!_heldModifiers.Remove(vk))
            return HookOutcome.Pass;

        var chordEnded = _heldModifiers.Count == 0;
        var modifierOnly = new Hotkey(_chordPeak, 0);

        if (_swallowingChord)
        {
            Hotkey? captured = chordEnded && capturing && !_chordHadOtherKey ? modifierOnly : null;
            FinishSwallowIfDone();
            return new HookOutcome(true, false, null, captured);
        }

        int? modifierOnlyId = !_chordHadOtherKey && Volatile.Read(ref _bindings).TryGetValue(modifierOnly, out var id) ? id : null;
        var ours = _firedInChord || modifierOnlyId is not null;
        var mask = ours && (flag == HotkeyModifiers.Win || flag == HotkeyModifiers.Alt);
        return new HookOutcome(false, mask, chordEnded ? modifierOnlyId : null, null);
    }

    private HookOutcome ProcessKey(int vk, bool isDown, bool capturing)
    {
        if (!isDown)
        {
            if (!_swallowedKeys.Remove(vk))
                return HookOutcome.Pass;

            FinishSwallowIfDone();
            return HookOutcome.Eat;
        }

        // Basılı tutunca gelen tekrar: eylem bir kez çalışır, tuş Windows'a sızmaz.
        if (_swallowedKeys.Contains(vk))
            return HookOutcome.Eat;

        _chordHadOtherKey = true;
        var hotkey = new Hotkey(CurrentModifiers(), vk);

        if (capturing || _swallowingChord)
        {
            _swallowingChord = true;
            _swallowedKeys.Add(vk);
            return new HookOutcome(true, false, null, capturing ? hotkey : null);
        }

        if (!Volatile.Read(ref _bindings).TryGetValue(hotkey, out var id))
            return HookOutcome.Pass;

        _firedInChord = true;
        _swallowedKeys.Add(vk);
        return new HookOutcome(true, false, id, null);
    }

    private void FinishSwallowIfDone()
    {
        if (_heldModifiers.Count == 0 && _swallowedKeys.Count == 0)
            _swallowingChord = false;
    }

    private HotkeyModifiers CurrentModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        foreach (var vk in _heldModifiers)
            modifiers |= ModifierOf(vk) ?? HotkeyModifiers.None;

        return modifiers;
    }

    private static HotkeyModifiers? ModifierOf(int vk) => vk switch
    {
        0x5B or 0x5C => HotkeyModifiers.Win,
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,
        _ => null
    };
}
