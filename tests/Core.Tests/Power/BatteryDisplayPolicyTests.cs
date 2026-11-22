using Koy.Core.Power;

namespace Koy.Core.Tests.Power;

public class BatteryDisplayPolicyTests
{
    private static BatteryDisplay D(int percent, bool ac = false, bool charging = false, bool saver = false, TimeSpan? time = null, bool hasBattery = true) =>
        BatteryDisplayPolicy.Decide(hasBattery, percent, ac, charging, saver, time);

    [Fact]
    public void Prizde_sarj_olurken_sarj_durumu_ve_yildirim()
    {
        var d = D(80, ac: true, charging: true);
        Assert.Equal(BatteryState.Charging, d.State);
        Assert.Equal(BatteryOverlay.Bolt, d.Overlay);
        Assert.Equal(BatteryTone.Normal, d.Tone);
        Assert.Equal(BatteryTooltipKind.Charging, d.Tooltip);
        Assert.True(d.IsPluggedIn);
    }

    [Fact]
    public void Prizde_yuzde_yuzde_yildirim_gosterir_ve_tam_dolu_der()
    {
        // Eski hata: %100'de şarj işareti kayboluyordu.
        var d = D(100, ac: true);
        Assert.Equal(BatteryState.PluggedFull, d.State);
        Assert.Equal(BatteryOverlay.Bolt, d.Overlay);
        Assert.Equal(BatteryTooltipKind.Full, d.Tooltip);
        Assert.Equal(1.0, d.FillRatio);
    }

    [Fact]
    public void Sarj_bildirse_bile_yuzde_yuzde_tam_dolu_sayilir()
    {
        var d = D(100, ac: true, charging: true);
        Assert.Equal(BatteryState.PluggedFull, d.State);
        Assert.Equal(BatteryTooltipKind.Full, d.Tooltip);
    }

    [Fact]
    public void Prizde_sarj_olmuyorsa_fis_ve_ozel_ipucu()
    {
        var d = D(80, ac: true);
        Assert.Equal(BatteryState.PluggedNotCharging, d.State);
        Assert.Equal(BatteryOverlay.Plug, d.Overlay);
        Assert.Equal(BatteryTooltipKind.PluggedNotCharging, d.Tooltip);
    }

    [Fact]
    public void Sarj_bayragi_priz_bildirilmese_de_prizde_sayar()
    {
        var d = D(50, ac: false, charging: true);
        Assert.Equal(BatteryState.Charging, d.State);
    }

    [Fact]
    public void Pilde_normal_isaretsiz_ve_kaldi_ipucu()
    {
        var d = D(54);
        Assert.Equal(BatteryState.OnBattery, d.State);
        Assert.Equal(BatteryOverlay.None, d.Overlay);
        Assert.Equal(BatteryTone.Normal, d.Tone);
        Assert.Equal(BatteryTooltipKind.Remaining, d.Tooltip);
        Assert.False(d.IsPluggedIn);
        Assert.Equal(0.54, d.FillRatio, 3);
    }

    [Theory]
    [InlineData(21, BatteryState.OnBattery, BatteryTone.Normal)]
    [InlineData(20, BatteryState.Low, BatteryTone.Warning)]
    [InlineData(11, BatteryState.Low, BatteryTone.Warning)]
    [InlineData(10, BatteryState.Critical, BatteryTone.Critical)]
    [InlineData(0, BatteryState.Critical, BatteryTone.Critical)]
    public void Pilde_esikler_dusuk_ve_kritik(int percent, BatteryState state, BatteryTone tone)
    {
        var d = D(percent);
        Assert.Equal(state, d.State);
        Assert.Equal(tone, d.Tone);
    }

    [Theory]
    [InlineData(5, true, false)]
    [InlineData(5, false, true)]
    public void Sarjdayken_dusuk_uyari_yok(int percent, bool charging, bool warns)
    {
        var d = D(percent, ac: charging, charging: charging);
        Assert.Equal(warns, d.Tone != BatteryTone.Normal);
    }

    [Fact]
    public void Pil_tasarrufu_pilde_yaprak_gosterir()
    {
        var d = D(60, saver: true);
        Assert.Equal(BatteryOverlay.Leaf, d.Overlay);
        Assert.True(d.EnergySaver);
        Assert.Equal(BatteryState.OnBattery, d.State);
    }

    [Fact]
    public void Pil_tasarrufu_dusuk_pilde_de_yaprak_ve_uyari_rengi_kalir()
    {
        var d = D(15, saver: true);
        Assert.Equal(BatteryOverlay.Leaf, d.Overlay);
        Assert.Equal(BatteryTone.Warning, d.Tone);
    }

    [Fact]
    public void Prizdeyken_sarj_isareti_pil_tasarrufu_yapragindan_onceliklidir()
    {
        Assert.Equal(BatteryOverlay.Bolt, D(60, ac: true, charging: true, saver: true).Overlay);
        Assert.Equal(BatteryOverlay.Plug, D(60, ac: true, saver: true).Overlay);
        Assert.Equal(BatteryOverlay.Bolt, D(100, ac: true, saver: true).Overlay);
    }

    [Fact]
    public void Kalan_sure_varsa_ipucu_sureli_olur()
    {
        var onBattery = D(54, time: new TimeSpan(3, 25, 40));
        Assert.Equal(BatteryTooltipKind.RemainingWithTime, onBattery.Tooltip);
        Assert.Equal(3, onBattery.TimeHours);
        Assert.Equal(25, onBattery.TimeMinutes);
        Assert.True(onBattery.HasTime);

        var charging = D(54, ac: true, charging: true, time: TimeSpan.FromMinutes(45));
        Assert.Equal(BatteryTooltipKind.ChargingWithTime, charging.Tooltip);
        Assert.Equal(0, charging.TimeHours);
        Assert.Equal(45, charging.TimeMinutes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(30)]       // 30 sn: dakikaya ulaşmaz
    public void Kalan_sure_bilinmiyorsa_ya_da_anlamsizsa_sure_yok(int? seconds)
    {
        TimeSpan? time = seconds is null ? null : TimeSpan.FromSeconds(seconds.Value);
        var d = D(54, time: time);
        Assert.Equal(BatteryTooltipKind.Remaining, d.Tooltip);
        Assert.False(d.HasTime);
    }

    [Fact]
    public void Ucuk_buyuk_sure_yok_sayilir()
    {
        Assert.False(D(54, time: TimeSpan.MaxValue).HasTime);
        Assert.False(D(54, time: TimeSpan.FromHours(500)).HasTime);
    }

    [Fact]
    public void Tam_dolu_ve_prizde_olmayan_durumda_sure_ipucu_sureyi_gostermez()
    {
        var d = D(100, ac: true, time: TimeSpan.FromHours(2));
        Assert.Equal(BatteryTooltipKind.Full, d.Tooltip);
        Assert.Equal(BatteryTooltipKind.PluggedNotCharging, D(70, ac: true, time: TimeSpan.FromHours(2)).Tooltip);
    }

    [Theory]
    [InlineData(82, "82%")]
    [InlineData(0, "0%")]
    [InlineData(100, "100%")]
    [InlineData(-5, "0%")]
    [InlineData(140, "100%")]
    public void FormatPercent_sinirlar_ve_bicim(int percent, string expected) =>
        Assert.Equal(expected, BatteryDisplayPolicy.FormatPercent(percent));

    [Fact]
    public void FormatPercent_turkce_kulturde_de_yuzde_sonda()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("100%", BatteryDisplayPolicy.FormatPercent(100));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Yuzde_kiskaclanir_ve_pil_yoksa_alan_tasinir()
    {
        var d = D(250, hasBattery: false);
        Assert.False(d.HasBattery);
        Assert.Equal(100, d.Percent);
        Assert.Equal("100%", d.PercentText);
        Assert.Equal(100, BatteryDisplay.None.Percent);
        Assert.False(BatteryDisplay.None.HasBattery);
    }

    [Fact]
    public void IPowerService_Display_olay_ve_hizmet_ayni_karari_verir()
    {
        var e = new PowerChangedEventArgs(true, 100, false, BatteryStatus.Idle, IsAcConnected: true);
        var fromEvent = e.Display();
        Assert.Equal(BatteryState.PluggedFull, fromEvent.State);
        Assert.Equal(BatteryOverlay.Bolt, fromEvent.Overlay);
    }

    [Fact]
    public void Bildirim_politikasi_prizdeki_sarj_olmayan_pili_prizde_sayar()
    {
        var policy = new BatteryNotificationPolicy();
        policy.Update(new PowerChangedEventArgs(true, 80, false, BatteryStatus.Discharging));
        var r = policy.Update(new PowerChangedEventArgs(true, 80, false, BatteryStatus.Discharging, IsAcConnected: true));
        Assert.Equal(BatteryAlertKind.PluggedIn, r.Kind);
    }
}
