using Koy.Core.Power;

namespace Koy.Core.Tests.Power;

public class BatteryNotificationPolicyTests
{
    private static PowerChangedEventArgs CreateArgs(
        bool hasBattery = true,
        int percent = 80,
        bool isCharging = false,
        BatteryStatus status = BatteryStatus.Discharging) =>
        new(hasBattery, percent, isCharging, status);

    [Fact]
    public void InitialCall_SeedsState_DoesNotEmitAlert()
    {
        var policy = new BatteryNotificationPolicy();

        var result = policy.Update(CreateArgs(hasBattery: true, percent: 75, isCharging: false, status: BatteryStatus.Discharging));

        Assert.Equal(BatteryAlertKind.None, result.Kind);
        Assert.Equal(75, result.Percent);
    }

    [Fact]
    public void Desktop_NoBattery_AlwaysReturnsNone()
    {
        var policy = new BatteryNotificationPolicy();

        var initial = policy.Update(CreateArgs(hasBattery: false, percent: 100, isCharging: false, status: BatteryStatus.NotPresent));
        var next = policy.Update(CreateArgs(hasBattery: false, percent: 100, isCharging: true, status: BatteryStatus.Idle));

        Assert.Equal(BatteryAlertKind.None, initial.Kind);
        Assert.Equal(BatteryAlertKind.None, next.Kind);
    }

    [Fact]
    public void PluggedIn_Transition_EmitsPluggedIn()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 50, isCharging: false, status: BatteryStatus.Discharging));

        var result = policy.Update(CreateArgs(percent: 50, isCharging: true, status: BatteryStatus.Charging));

        Assert.Equal(BatteryAlertKind.PluggedIn, result.Kind);
        Assert.Equal(50, result.Percent);
    }

    [Fact]
    public void Unplugged_Transition_EmitsUnplugged()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 60, isCharging: true, status: BatteryStatus.Charging));

        var result = policy.Update(CreateArgs(percent: 60, isCharging: false, status: BatteryStatus.Discharging));

        Assert.Equal(BatteryAlertKind.Unplugged, result.Kind);
        Assert.Equal(60, result.Percent);
    }

    [Fact]
    public void FullyCharged_Transition_EmitsFullyCharged_OnlyOnce()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 98, isCharging: true, status: BatteryStatus.Charging));

        var step1 = policy.Update(CreateArgs(percent: 99, isCharging: true, status: BatteryStatus.Charging));
        Assert.Equal(BatteryAlertKind.None, step1.Kind);

        var fullResult = policy.Update(CreateArgs(percent: 100, isCharging: false, status: BatteryStatus.Idle));
        Assert.Equal(BatteryAlertKind.FullyCharged, fullResult.Kind);
        Assert.Equal(100, fullResult.Percent);

        // Bir sonraki olayda hâlâ %100 ve şarjda iken tekrar uyarı vermez
        var nextResult = policy.Update(CreateArgs(percent: 100, isCharging: false, status: BatteryStatus.Idle));
        Assert.Equal(BatteryAlertKind.None, nextResult.Kind);
    }

    [Fact]
    public void LowBattery_20Percent_FiresOnce_And_HysteresisPreventsFluctuationBouncing()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 25, isCharging: false, status: BatteryStatus.Discharging));

        // 22%'ye düşer - henüz uyarı yok
        var res22 = policy.Update(CreateArgs(percent: 22, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res22.Kind);

        // 20%'ye düşer - LowBattery tetiklenmeli
        var res20 = policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res20.Kind);
        Assert.Equal(20, res20.Percent);

        // 19%'a düşer - aynı döngüde tekrar tetiklenmemeli
        var res19 = policy.Update(CreateArgs(percent: 19, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res19.Kind);

        // Dalgalanma: 21%'e çıkıp tekrar 20%'ye inerse (%25'e ulaşmadığı için) tekrar tetiklenmemeli
        var res21 = policy.Update(CreateArgs(percent: 21, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res21.Kind);

        var res20Again = policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res20Again.Kind);
    }

    [Fact]
    public void LowBattery_10Percent_FiresOnce_After20Percent()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 25, isCharging: false, status: BatteryStatus.Discharging));
        policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));

        // 15% - 20 uyarısı verilmişti, 10 henüz değil
        var res15 = policy.Update(CreateArgs(percent: 15, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res15.Kind);

        // 10% - LowBattery tetiklenmeli
        var res10 = policy.Update(CreateArgs(percent: 10, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res10.Kind);
        Assert.Equal(10, res10.Percent);

        // 9% - tekrar tetiklenmemeli
        var res9 = policy.Update(CreateArgs(percent: 9, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res9.Kind);
    }

    [Fact]
    public void LowBattery_DirectDropToUnder10Percent_FiresOnce()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 25, isCharging: false, status: BatteryStatus.Discharging));

        // Ani düşüş: 25'ten 8%'e
        var res8 = policy.Update(CreateArgs(percent: 8, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res8.Kind);
        Assert.Equal(8, res8.Percent);

        // 7%'ye devam edince tekrar tetiklenmemeli
        var res7 = policy.Update(CreateArgs(percent: 7, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res7.Kind);
    }

    [Fact]
    public void Hysteresis_Resets_WhenPluggedIn()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 25, isCharging: false, status: BatteryStatus.Discharging));

        // 20%'de uyarı verir
        var res20 = policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res20.Kind);

        // Şarja takılır
        var resPlug = policy.Update(CreateArgs(percent: 20, isCharging: true, status: BatteryStatus.Charging));
        Assert.Equal(BatteryAlertKind.PluggedIn, resPlug.Kind);

        // Şarjdan çıkarılır
        var resUnplug = policy.Update(CreateArgs(percent: 21, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.Unplugged, resUnplug.Kind);

        // Tekrar 20%'ye düşünce histerezis sıfırlandığı için yeniden uyarı vermelidir
        var res20New = policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res20New.Kind);
    }

    [Fact]
    public void Hysteresis_Resets_WhenChargeRecoversTo25Percent()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 22, isCharging: false, status: BatteryStatus.Discharging));

        // 20%'de uyarı verir
        policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));

        // Şarj %25'e yükselir (ör. kalibrasyon veya kısa şarj)
        policy.Update(CreateArgs(percent: 25, isCharging: false, status: BatteryStatus.Discharging));

        // Tekrar 20%'ye düşünce yeniden uyarı vermelidir
        var res20New = policy.Update(CreateArgs(percent: 20, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res20New.Kind);
    }

    [Fact]
    public void Startup_AlreadyLow_SuppressedUntilNextThreshold()
    {
        var policy = new BatteryNotificationPolicy();

        // 15% ile açılır - başlangıç uyarısı bastırılır
        var seed = policy.Update(CreateArgs(percent: 15, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, seed.Kind);

        // 14% - 20 uyarısı verilmez
        var res14 = policy.Update(CreateArgs(percent: 14, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res14.Kind);

        // 10%'a düşünce alt eşik uyarısı verilmelidir
        var res10 = policy.Update(CreateArgs(percent: 10, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.LowBattery, res10.Kind);
        Assert.Equal(10, res10.Percent);
    }

    [Fact]
    public void Startup_AlreadyCriticallyLow_SuppressedCompletely()
    {
        var policy = new BatteryNotificationPolicy();

        // 8% ile açılır - başlangıç uyarısı bastırılır
        var seed = policy.Update(CreateArgs(percent: 8, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, seed.Kind);

        // 7%'ye iner - zaten kritik açıldığı için yeniden uyarmaz
        var res7 = policy.Update(CreateArgs(percent: 7, isCharging: false, status: BatteryStatus.Discharging));
        Assert.Equal(BatteryAlertKind.None, res7.Kind);
    }

    [Fact]
    public void PluggedIn_WhileLow_DoesNotEmitLowBattery()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(CreateArgs(percent: 15, isCharging: true, status: BatteryStatus.Charging));

        // Şarjdayken 10%'a düşse bile LowBattery vermemeli
        var res = policy.Update(CreateArgs(percent: 10, isCharging: true, status: BatteryStatus.Charging));
        Assert.Equal(BatteryAlertKind.None, res.Kind);
    }
}
