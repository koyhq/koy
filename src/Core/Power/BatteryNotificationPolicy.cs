namespace Koy.Core.Power;

/// <summary>
/// Pil bildirim türü.
/// </summary>
public enum BatteryAlertKind
{
    None,
    PluggedIn,
    Unplugged,
    LowBattery,
    FullyCharged
}

/// <summary>
/// Pil bildirim sonucu: bildirim türü ve o anki pil yüzdesi.
/// </summary>
public readonly record struct BatteryAlertResult(BatteryAlertKind Kind, int Percent);

/// <summary>
/// Pil durumu geçişlerini inceleyip OSD bildirimi üretilip üretilmeyeceğini belirleyen saf mantık.
/// Açılışta tohumlama (seeding) bastırılır; histerezis ile düşük pil uyarısı döngü başına bir kez verilir.
/// </summary>
public sealed class BatteryNotificationPolicy
{
    private bool _isInitialized;
    private bool _lastHasBattery;
    private bool _lastIsPlugged;
    private int _lastPercent;

    private bool _warnedLow20;
    private bool _warnedLow10;

    /// <summary>
    /// Güç durumu değişikliğini işleyip bildirim sonucunu döner.
    /// </summary>
    public BatteryAlertResult Update(PowerChangedEventArgs current)
    {
        if (!current.HasBattery)
        {
            _warnedLow20 = false;
            _warnedLow10 = false;
            _lastHasBattery = false;
            _lastIsPlugged = false;
            _isInitialized = true;
            return new BatteryAlertResult(BatteryAlertKind.None, current.RemainingPercent);
        }

        var isPlugged = IsPlugged(current);

        if (!_isInitialized)
        {
            _isInitialized = true;
            _lastHasBattery = current.HasBattery;
            _lastIsPlugged = isPlugged;
            _lastPercent = current.RemainingPercent;

            // Açılışta düşük pildeyse başlangıç uyarısını bastır, ancak daha alt eşik için izin ver.
            if (!isPlugged)
            {
                if (current.RemainingPercent <= 20)
                    _warnedLow20 = true;
                if (current.RemainingPercent <= 10)
                    _warnedLow10 = true;
            }

            return new BatteryAlertResult(BatteryAlertKind.None, current.RemainingPercent);
        }

        // Histerezis sıfırlama: şarja takıldığında veya pil %25 ve üstüne çıktığında
        if (isPlugged || current.RemainingPercent >= 25)
        {
            _warnedLow20 = false;
            _warnedLow10 = false;
        }

        var kind = BatteryAlertKind.None;

        // 1. Şarja takılma geçişi
        if (!_lastIsPlugged && isPlugged)
        {
            kind = BatteryAlertKind.PluggedIn;
            _warnedLow20 = false;
            _warnedLow10 = false;
        }
        // 2. Şarjdan çıkarılma geçişi
        else if (_lastIsPlugged && !isPlugged)
        {
            kind = BatteryAlertKind.Unplugged;
        }
        // 3. Şarj tamamlandı (%100'e ulaşıldı)
        else if (isPlugged && current.RemainingPercent == 100 && _lastPercent < 100)
        {
            kind = BatteryAlertKind.FullyCharged;
        }
        // 4. Düşük pil eşikleri (yalnız pildeyken)
        else if (!isPlugged)
        {
            if (current.RemainingPercent <= 10 && !_warnedLow10)
            {
                kind = BatteryAlertKind.LowBattery;
                _warnedLow10 = true;
                _warnedLow20 = true;
            }
            else if (current.RemainingPercent <= 20 && !_warnedLow20)
            {
                kind = BatteryAlertKind.LowBattery;
                _warnedLow20 = true;
            }
        }

        _lastHasBattery = current.HasBattery;
        _lastIsPlugged = isPlugged;
        _lastPercent = current.RemainingPercent;

        return new BatteryAlertResult(kind, current.RemainingPercent);
    }

    /// <summary>
    /// Update ile aynı işlevi görür; alternatif adlandırma için.
    /// </summary>
    public BatteryAlertResult ProcessChange(PowerChangedEventArgs current) => Update(current);

    private static bool IsPlugged(PowerChangedEventArgs current) =>
        current.IsCharging || current.Status is BatteryStatus.Charging or BatteryStatus.Idle;
}
