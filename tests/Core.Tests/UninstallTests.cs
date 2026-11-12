using Koy.Core.Uninstall;

namespace Koy.Core.Tests;

public class UninstallTests
{
    [Fact]
    public void Evaluate_KuruluVeKaldiriciVarsa_Available()
    {
        var r = UninstallPlan.Evaluate(isInstalled: true, isPortable: false, @"C:\k\Update.exe", _ => true);
        Assert.Equal(UninstallAvailability.Available, r);
    }

    [Fact]
    public void Evaluate_KuruluDegilse_NotInstalled()
    {
        var r = UninstallPlan.Evaluate(false, false, null, _ => true);
        Assert.Equal(UninstallAvailability.NotInstalled, r);
    }

    [Fact]
    public void Evaluate_Tasinabilirse_KurulSanilmaz()
    {
        var r = UninstallPlan.Evaluate(isInstalled: true, isPortable: true, @"C:\k\Update.exe", _ => true);
        Assert.Equal(UninstallAvailability.Portable, r);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Evaluate_KaldiriciYoluYoksa_UpdaterMissing(string? path)
    {
        var r = UninstallPlan.Evaluate(true, false, path, _ => true);
        Assert.Equal(UninstallAvailability.UpdaterMissing, r);
    }

    [Fact]
    public void Evaluate_KaldiriciDosyasiYoksa_UpdaterMissing()
    {
        var r = UninstallPlan.Evaluate(true, false, @"C:\k\Update.exe", _ => false);
        Assert.Equal(UninstallAvailability.UpdaterMissing, r);
    }

    [Fact]
    public void FilesToDelete_AyarlarSaklaniyorsa_AyarDosyalariListedeYok()
    {
        var files = UninstallPlan.FilesToDelete("R", "L", keepSettings: true);

        Assert.DoesNotContain(files, f => f.EndsWith("settings.json", StringComparison.Ordinal));
        Assert.DoesNotContain(files, f => f.EndsWith("shelf.json", StringComparison.Ordinal));
        Assert.Contains(Path.Combine("R", "taskbar-state.json"), files);
        Assert.Contains(Path.Combine("L", "crash.log"), files);
    }

    [Fact]
    public void FilesToDelete_AyarlarSilinecekse_AyarVeRafListede()
    {
        var files = UninstallPlan.FilesToDelete("R", "L", keepSettings: false);

        Assert.Contains(Path.Combine("R", "settings.json"), files);
        Assert.Contains(Path.Combine("R", "shelf.json"), files);
        Assert.Contains(Path.Combine("L", "crash.log.old"), files);
    }

    [Fact]
    public void FilesToDelete_KaldiriciVeUygulamaDosyalariniIcermez()
    {
        // Velopack kök klasörü (yerel) bütünüyle silinmez; yalnız bilinen tanı dosyaları gider.
        var files = UninstallPlan.FilesToDelete("R", "L", false);
        Assert.DoesNotContain(files, f => f.EndsWith("Update.exe", StringComparison.Ordinal) || f.EndsWith("Koy.exe", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowsDanKaldirmada_AyarlarSilinmez()
    {
        Assert.True(UninstallPlan.KeepSettingsOnExternalUninstall);
    }

    [Fact]
    public void Run_AyarlarSaklaniyorsa_AyarKalirDurumGider()
    {
        using var t = new TempDirs();
        File.WriteAllText(Path.Combine(t.Roaming, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(t.Roaming, "taskbar-state.json"), "{}");
        File.WriteAllText(Path.Combine(t.Local, "crash.log"), "x");

        var result = UninstallCleaner.Run(t.Roaming, t.Local, keepSettings: true);

        Assert.True(File.Exists(Path.Combine(t.Roaming, "settings.json")));
        Assert.False(File.Exists(Path.Combine(t.Roaming, "taskbar-state.json")));
        Assert.False(File.Exists(Path.Combine(t.Local, "crash.log")));
        Assert.Equal(2, result.Deleted);
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void Run_AyarlarSilinecekse_BosKlasorDeGider()
    {
        using var t = new TempDirs();
        File.WriteAllText(Path.Combine(t.Roaming, "settings.json"), "{}");
        File.WriteAllText(Path.Combine(t.Roaming, "shelf.json"), "[]");

        UninstallCleaner.Run(t.Roaming, t.Local, keepSettings: false);

        Assert.False(Directory.Exists(t.Roaming));
    }

    [Fact]
    public void Run_KlasordeBaskaDosyaVarsa_KlasoreDokunmaz()
    {
        using var t = new TempDirs();
        File.WriteAllText(Path.Combine(t.Roaming, "settings.json"), "{}");
        var other = Path.Combine(t.Roaming, "baska.txt");
        File.WriteAllText(other, "k");

        UninstallCleaner.Run(t.Roaming, t.Local, keepSettings: false);

        Assert.True(File.Exists(other));
    }

    [Fact]
    public void Run_KilitliDosyada_IstisnaFirlatmazVeBasarisizOlarakBildirir()
    {
        using var t = new TempDirs();
        var settings = Path.Combine(t.Roaming, "settings.json");
        File.WriteAllText(settings, "{}");
        using var lockStream = new FileStream(settings, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = UninstallCleaner.Run(t.Roaming, t.Local, keepSettings: false);

        Assert.Contains(settings, result.Failed);
    }

    [Fact]
    public void Run_KlasorlerYoksaSorunsuzGecer()
    {
        var result = UninstallCleaner.Run(@"Z:\yok\r", @"Z:\yok\l", keepSettings: false);
        Assert.Equal(0, result.Deleted);
    }

    private sealed class TempDirs : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "koy-uninstall-" + Guid.NewGuid().ToString("N"));
        public string Roaming { get; }
        public string Local { get; }

        public TempDirs()
        {
            Roaming = Path.Combine(_root, "roaming");
            Local = Path.Combine(_root, "local");
            Directory.CreateDirectory(Roaming);
            Directory.CreateDirectory(Local);
        }

        public void Dispose()
        {
            try { Directory.Delete(_root, true); } catch { }
        }
    }
}
