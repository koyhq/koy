using Koy.Core.Shell;

namespace Koy.Core.Tests;

public class TaskbarSafetyTests
{
    private static TaskbarStateRead Valid(bool hidden, int previous)
        => new(TaskbarStateFileStatus.Valid, new TaskbarSavedState(hidden, previous));

    private static TaskbarStateRead Corrupt => new(TaskbarStateFileStatus.Corrupt, null);

    // --- Durum dosyası sınıflaması ---

    [Fact]
    public void Read_NullDosyaYok()
    {
        Assert.Equal(TaskbarStateFileStatus.Missing, TaskbarStatePolicy.Read(null).Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{\"taskbarHidden\": tr")]
    [InlineData("bozuk")]
    public void Read_BosKesikYaDaBozukIcerikCorrupt(string content)
    {
        var read = TaskbarStatePolicy.Read(content);

        Assert.Equal(TaskbarStateFileStatus.Corrupt, read.Status);
        Assert.Null(read.State);
    }

    [Fact]
    public void Read_GecerliIcerikValid()
    {
        var json = TaskbarStatePolicy.Serialize(new TaskbarSavedState(true, 2), DateTime.UtcNow);

        var read = TaskbarStatePolicy.Read(json);

        Assert.Equal(TaskbarStateFileStatus.Valid, read.Status);
        Assert.Equal(new TaskbarSavedState(true, 2), read.State);
    }

    // --- Geri yükleme kararı ---

    [Fact]
    public void DecideRestore_GizliKayitOncekiDurumuYazar()
    {
        var d = TaskbarStatePolicy.DecideRestore(Valid(true, TaskbarStatePolicy.AutoHide), TaskbarStatePolicy.AutoHide);

        Assert.Equal(new TaskbarRestoreDecision(true, TaskbarStatePolicy.AutoHide), d);
    }

    [Fact]
    public void DecideRestore_DosyaYoksaDokunmaz()
    {
        var d = TaskbarStatePolicy.DecideRestore(TaskbarStateRead.Missing, TaskbarStatePolicy.AutoHide);

        Assert.False(d.SendState);
    }

    [Fact]
    public void DecideRestore_GizliOlmayanKayitDokunmaz()
    {
        Assert.False(TaskbarStatePolicy.DecideRestore(Valid(false, 2), TaskbarStatePolicy.AutoHide).SendState);
    }

    [Fact]
    public void DecideRestore_BozukDosyaVeAutoHideGuvenliTarafaGeriYukler()
    {
        var d = TaskbarStatePolicy.DecideRestore(Corrupt, TaskbarStatePolicy.AutoHide);

        Assert.Equal(new TaskbarRestoreDecision(true, TaskbarStatePolicy.AlwaysOnTop), d);
    }

    [Fact]
    public void DecideRestore_BozukDosyaAmaAutoHideDegilseDokunmaz()
    {
        Assert.False(TaskbarStatePolicy.DecideRestore(Corrupt, TaskbarStatePolicy.AlwaysOnTop).SendState);
    }

    [Fact]
    public void DecideRestore_ZorlamaKayitYoksaAlwaysOnTop()
    {
        var d = TaskbarStatePolicy.DecideRestore(TaskbarStateRead.Missing, TaskbarStatePolicy.AutoHide, force: true);

        Assert.Equal(new TaskbarRestoreDecision(true, TaskbarStatePolicy.AlwaysOnTop), d);
    }

    [Fact]
    public void DecideRestore_ZorlamaKayitliTercihiKorur()
    {
        var d = TaskbarStatePolicy.DecideRestore(Valid(true, TaskbarStatePolicy.AutoHide), TaskbarStatePolicy.AutoHide, force: true);

        Assert.Equal(TaskbarStatePolicy.AutoHide, d.State);
    }

    [Fact]
    public void HasPendingRestore_GizliKayitVeBozukDosyaIcinTrue()
    {
        Assert.True(TaskbarStatePolicy.HasPendingRestore(Valid(true, 2)));
        Assert.True(TaskbarStatePolicy.HasPendingRestore(Corrupt));
        Assert.False(TaskbarStatePolicy.HasPendingRestore(Valid(false, 2)));
        Assert.False(TaskbarStatePolicy.HasPendingRestore(TaskbarStateRead.Missing));
    }

    [Fact]
    public void ChoosePreviousState_BozukDosyaVeAutoHideAlwaysOnTopSayar()
    {
        Assert.Equal(TaskbarStatePolicy.AlwaysOnTop, TaskbarStatePolicy.ChoosePreviousState(Corrupt, TaskbarStatePolicy.AutoHide));
    }

    [Fact]
    public void ChoosePreviousState_DosyaYoksaKullanicininAutoHideTercihiniKorur()
    {
        // Dosya hiç yazılmadıysa AUTOHIDE'ı biz koymadık: kullanıcının gerçek tercihidir.
        Assert.Equal(TaskbarStatePolicy.AutoHide, TaskbarStatePolicy.ChoosePreviousState(TaskbarStateRead.Missing, TaskbarStatePolicy.AutoHide));
    }

    [Fact]
    public void ChoosePreviousState_GecerliGizliKayitKorunur()
    {
        Assert.Equal(TaskbarStatePolicy.AlwaysOnTop, TaskbarStatePolicy.ChoosePreviousState(Valid(true, TaskbarStatePolicy.AlwaysOnTop), TaskbarStatePolicy.AutoHide));
    }

    // --- Çok monitör: hangi çubuk gizlenir ---

    private const nint M1 = 100, M2 = 200, M3 = 300;

    [Fact]
    public void SelectBarsToHide_YalnizDockluMonitorlerinCubuklari()
    {
        var bars = new[] { new TaskbarBar(1, M1), new TaskbarBar(2, M2), new TaskbarBar(3, M3) };

        var hide = TaskbarBarSelector.SelectBarsToHide(bars, new HashSet<nint> { M1, M3 });

        Assert.Equal(new nint[] { 1, 3 }, hide);
    }

    [Fact]
    public void SelectBarsToHide_DockHerMonitordeyseHepsiGizlenir()
    {
        var bars = new[] { new TaskbarBar(1, M1), new TaskbarBar(2, M2) };

        Assert.Equal(2, TaskbarBarSelector.SelectBarsToHide(bars, new HashSet<nint> { M1, M2 }).Count);
    }

    [Fact]
    public void SelectBarsToHide_DockYoksaHicbiriGizlenmez()
    {
        var bars = new[] { new TaskbarBar(1, M1), new TaskbarBar(2, M2) };

        Assert.Empty(TaskbarBarSelector.SelectBarsToHide(bars, new HashSet<nint>()));
    }

    [Fact]
    public void SelectBarsToHide_MonitoruBilinmeyenCubukGorunurKalir()
    {
        var bars = new[] { new TaskbarBar(1, 0) };

        Assert.Empty(TaskbarBarSelector.SelectBarsToHide(bars, new HashSet<nint> { 0, M1 }));
    }

    [Fact]
    public void SelectBarsToHide_AyniMonitordeBirdenCokCubukHepsiGizlenir()
    {
        var bars = new[] { new TaskbarBar(1, M1), new TaskbarBar(2, M1) };

        Assert.Equal(new nint[] { 1, 2 }, TaskbarBarSelector.SelectBarsToHide(bars, new HashSet<nint> { M1 }));
    }

    // --- RunOnce yedeği ---

    [Fact]
    public void RunOnce_KomutTirnakliYolVeGeriGetirmeArgumani()
    {
        var cmd = TaskbarRunOncePolicy.BuildCommand(@"C:\Users\A B\AppData\Local\Koy\current\Koy.Watchdog.exe");

        Assert.Equal("\"C:\\Users\\A B\\AppData\\Local\\Koy\\current\\Koy.Watchdog.exe\" --restore-taskbar", cmd);
    }

    [Fact]
    public void RunOnce_KomutGozcununArgumaniylaCozulur()
    {
        var args = TaskbarRunOncePolicy.BuildCommand(@"C:\k\Koy.Watchdog.exe").Split(' ');

        Assert.True(WatchdogPolicy.IsRestoreOnly(args));
        Assert.False(WatchdogPolicy.TryParseProcessId(args, out _));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("\"C:\\eski\\Koy.Watchdog.exe\" --restore-taskbar", true)]
    [InlineData("\"C:\\k\\koy.watchdog.exe\" --restore-taskbar", false)]
    public void RunOnce_NeedsWrite(string? existing, bool expected)
    {
        var desired = TaskbarRunOncePolicy.BuildCommand(@"C:\k\Koy.Watchdog.exe");

        Assert.Equal(expected, TaskbarRunOncePolicy.NeedsWrite(existing, desired));
    }

    // --- Gözcü kipleri ---

    [Fact]
    public void IsRestoreOnly_BuyukKucukHarfDuyarsiz()
    {
        Assert.True(WatchdogPolicy.IsRestoreOnly(["--RESTORE-TASKBAR"]));
        Assert.False(WatchdogPolicy.IsRestoreOnly(["--pid", "5"]));
        Assert.False(WatchdogPolicy.IsRestoreOnly([]));
    }

    [Fact]
    public void WatchdogMutexAdlari_Ayrik()
    {
        Assert.NotEqual(WatchdogPolicy.SingletonMutexName, WatchdogPolicy.RestoreOnlySingletonMutexName);
        Assert.NotEqual(WatchdogPolicy.KoySingleInstanceMutexName, WatchdogPolicy.SingletonMutexName);
    }

    // --- Oturum kapanışı ---

    [Fact]
    public void ShutdownGuard_BaslamadanBastirmaz()
    {
        Assert.False(new TaskbarShutdownGuard().IsSuppressed(5_000));
    }

    [Fact]
    public void ShutdownGuard_SureBoyuncaBastirirSonraBirakir()
    {
        var guard = new TaskbarShutdownGuard(suppressMs: 1_000);
        guard.Begin(10_000);

        Assert.True(guard.IsSuppressed(10_500));
        Assert.True(guard.IsSuppressed(10_999));
        Assert.False(guard.IsSuppressed(11_000));
    }
}
