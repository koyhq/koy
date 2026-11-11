using Koy.Core.Settings;

namespace Koy.Core.Tests.Settings;

public class ApplyRevertPolicyTests
{
    private static readonly DateTimeOffset AppliedAt = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Timeout = ApplyRevertPolicy.DefaultTimeout;

    [Fact]
    public void Varsayilan_zaman_asimi_15_saniye()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), ApplyRevertPolicy.DefaultTimeout);
    }

    [Fact]
    public void Zaman_asimi_dolmadan_yanit_yoksa_beklenir()
    {
        var decision = ApplyRevertPolicy.Decide(AppliedAt, AppliedAt.AddSeconds(14.9), ApplyRevertChoice.None, Timeout);

        Assert.Equal(ApplyRevertDecision.Wait, decision);
    }

    [Fact]
    public void Zaman_asimi_dolunca_yanit_yoksa_onceki_deger_geri_alinir()
    {
        var atTimeout = ApplyRevertPolicy.Decide(AppliedAt, AppliedAt.Add(Timeout), ApplyRevertChoice.None, Timeout);
        var afterTimeout = ApplyRevertPolicy.Decide(AppliedAt, AppliedAt.AddMinutes(1), ApplyRevertChoice.None, Timeout);

        Assert.Equal(ApplyRevertDecision.Revert, atTimeout);
        Assert.Equal(ApplyRevertDecision.Revert, afterTimeout);
    }

    [Fact]
    public void Onay_zaman_asiminda_bile_degeri_tutar()
    {
        var decision = ApplyRevertPolicy.Decide(AppliedAt, AppliedAt.AddHours(1), ApplyRevertChoice.Confirm, Timeout);

        Assert.Equal(ApplyRevertDecision.Keep, decision);
    }

    [Fact]
    public void Acik_geri_alma_zaman_dolmadan_geri_alir()
    {
        var decision = ApplyRevertPolicy.Decide(AppliedAt, AppliedAt.AddSeconds(1), ApplyRevertChoice.Revert, Timeout);

        Assert.Equal(ApplyRevertDecision.Revert, decision);
    }

    [Fact]
    public void Kalan_sure_azalir_ve_sifirda_durur()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), ApplyRevertPolicy.Remaining(AppliedAt, AppliedAt, Timeout));
        Assert.Equal(TimeSpan.FromSeconds(10), ApplyRevertPolicy.Remaining(AppliedAt, AppliedAt.AddSeconds(5), Timeout));
        Assert.Equal(TimeSpan.Zero, ApplyRevertPolicy.Remaining(AppliedAt, AppliedAt.AddSeconds(30), Timeout));
    }

    [Fact]
    public void Sifir_veya_negatif_zaman_asimi_reddedilir()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyRevertPolicy.Decide(AppliedAt, AppliedAt, ApplyRevertChoice.None, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ApplyRevertPolicy.Remaining(AppliedAt, AppliedAt, TimeSpan.FromSeconds(-1)));
    }
}
