using Koy.Core.Power;
using WinPower = Windows.System.Power.PowerManager;
using WinBatteryStatus = Windows.System.Power.BatteryStatus;
using WinEnergySaver = Windows.System.Power.EnergySaverStatus;
using WinPowerSupply = Windows.System.Power.PowerSupplyStatus;

namespace Koy.Platform.Power;

/// <summary>
/// WinRT PowerManager olaylarından pil, güç kaynağı ve pil tasarrufu bilgisini verir. Yoklama yok: her bilgi kendi olayıyla tazelenir.
/// </summary>
public sealed class PowerService : IPowerService
{
    private readonly object _gate = new();
    private bool _disposed;

    public bool HasBattery { get; private set; }
    public int RemainingPercent { get; private set; } = 100;
    public bool IsCharging { get; private set; }
    public bool IsAcConnected { get; private set; }
    public bool IsEnergySaverOn { get; private set; }
    public TimeSpan? RemainingTime { get; private set; }
    public BatteryStatus Status { get; private set; } = BatteryStatus.NotPresent;

    public event EventHandler<PowerChangedEventArgs>? PowerChanged;

    public PowerService()
    {
        Refresh();

        WinPower.BatteryStatusChanged += OnChanged;
        WinPower.RemainingChargePercentChanged += OnChanged;
        WinPower.PowerSupplyStatusChanged += OnChanged;
        WinPower.EnergySaverStatusChanged += OnChanged;
        WinPower.RemainingDischargeTimeChanged += OnChanged;
    }

    private void Refresh()
    {
        PowerChangedEventArgs args;
        lock (_gate)
        {
            if (_disposed)
                return;

            try
            {
                Status = WinPower.BatteryStatus switch
                {
                    WinBatteryStatus.Charging => BatteryStatus.Charging,
                    WinBatteryStatus.Discharging => BatteryStatus.Discharging,
                    WinBatteryStatus.Idle => BatteryStatus.Idle,
                    _ => BatteryStatus.NotPresent
                };

                HasBattery = Status != BatteryStatus.NotPresent;
                RemainingPercent = WinPower.RemainingChargePercent;

                // Şarj ile priz ayrı: prizde tam dolu (Idle) ya da şarj sınırında bekleyen pil şarj olmuyor ama prizde.
                IsCharging = Status == BatteryStatus.Charging;
                IsAcConnected = HasBattery && WinPower.PowerSupplyStatus != WinPowerSupply.NotPresent;
                IsEnergySaverOn = WinPower.EnergySaverStatus == WinEnergySaver.On;

                // Kalan süre yalnız pilde anlamlı; prizdeyken Windows çok büyük bir değer verir.
                RemainingTime = HasBattery && !IsAcConnected && WinPower.RemainingDischargeTime is { } left && left > TimeSpan.Zero && left < TimeSpan.FromHours(100)
                    ? left
                    : null;
            }
            catch
            {
                HasBattery = false;
                IsCharging = false;
                IsAcConnected = false;
                IsEnergySaverOn = false;
                RemainingTime = null;
                Status = BatteryStatus.NotPresent;
            }

            args = new PowerChangedEventArgs(HasBattery, RemainingPercent, IsCharging, Status, IsAcConnected, IsEnergySaverOn, RemainingTime);
        }

        PowerChanged?.Invoke(this, args);
    }

    private void OnChanged(object? sender, object e) => Refresh();

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        WinPower.BatteryStatusChanged -= OnChanged;
        WinPower.RemainingChargePercentChanged -= OnChanged;
        WinPower.PowerSupplyStatusChanged -= OnChanged;
        WinPower.EnergySaverStatusChanged -= OnChanged;
        WinPower.RemainingDischargeTimeChanged -= OnChanged;
    }
}
