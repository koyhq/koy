using Koy.Core.Shell;

namespace Koy.Core.Tests.Shell;

public class StartupPathPolicyTests
{
    [Fact]
    public void ResolveTargetPath_KuruluSurumdeSaplamaVarsaKokSaplamayiSecer()
    {
        var rootDir = @"C:\Users\u\AppData\Local\Koy";
        var currentExe = @"C:\Users\u\AppData\Local\Koy\current\Koy.exe";
        var stubExe = @"C:\Users\u\AppData\Local\Koy\Koy.exe";

        var resolved = StartupPathPolicy.ResolveTargetPath(
            isInstalled: true,
            isPortable: false,
            rootAppDir: rootDir,
            processPath: currentExe,
            fileExists: p => string.Equals(p, stubExe, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(stubExe, resolved);
    }

    [Fact]
    public void ResolveTargetPath_SaplamaYoksaMevcutSurecYolunaDuser()
    {
        var rootDir = @"C:\Users\u\AppData\Local\Koy";
        var currentExe = @"C:\Users\u\AppData\Local\Koy\current\Koy.exe";

        var resolved = StartupPathPolicy.ResolveTargetPath(
            isInstalled: true,
            isPortable: false,
            rootAppDir: rootDir,
            processPath: currentExe,
            fileExists: _ => false);

        Assert.Equal(currentExe, resolved);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ResolveTargetPath_TasinabilirVeyaGelistirmedeSurecYolunuKorur(bool isInstalled, bool isPortable)
    {
        var devExe = @"C:\src\Koy\bin\Debug\net10.0\Koy.exe";

        var resolved = StartupPathPolicy.ResolveTargetPath(
            isInstalled: isInstalled,
            isPortable: isPortable,
            rootAppDir: @"C:\src\Koy\bin\Debug\net10.0",
            processPath: devExe,
            fileExists: _ => true);

        Assert.Equal(devExe, resolved);
    }

    [Theory]
    [InlineData(@"C:\Program Files\Koy\Koy.exe", null, "\"C:\\Program Files\\Koy\\Koy.exe\"")]
    [InlineData("\"C:\\Users\\Ad Soyad\\Koy.exe\"", "--autostart", "\"C:\\Users\\Ad Soyad\\Koy.exe\" --autostart")]
    public void FormatCommandLine_YoluTirnaklarVeArgumaniEkler(string path, string? args, string expected)
    {
        Assert.Equal(expected, StartupPathPolicy.FormatCommandLine(path, args));
    }

    [Theory]
    [InlineData("\"C:\\Users\\Ad Soyad\\AppData\\Local\\Koy\\Koy.exe\"", @"C:\Users\Ad Soyad\AppData\Local\Koy\Koy.exe")]
    [InlineData("\"C:\\Users\\u\\AppData\\Local\\Koy\\Koy.exe\" --autostart", @"C:\Users\u\AppData\Local\Koy\Koy.exe")]
    [InlineData(@"C:\Program Files\Koy\Koy.exe --minimized", @"C:\Program Files\Koy\Koy.exe")]
    [InlineData(@"C:\Program Files\Koy\Koy.exe", @"C:\Program Files\Koy\Koy.exe")]
    [InlineData("   ", null)]
    [InlineData("\"\"", null)]
    [InlineData("\"C:\\unclosed", null)]
    public void ExtractExecutablePath_CesitliKomutSatirlariniCozumler(string? cmd, string? expected)
    {
        Assert.Equal(expected, StartupPathPolicy.ExtractExecutablePath(cmd));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(new byte[0], true)]
    [InlineData(new byte[] { 0x02, 0x00, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0x06, 0x00, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0x03, 0x00, 0x00, 0x00 }, false)]
    [InlineData(new byte[] { 0x07, 0x00, 0x00, 0x00 }, false)]
    [InlineData(new byte[] { 0x01, 0x00, 0x00, 0x00 }, false)]
    public void IsStartupApprovedEnabled_GorevYoneticisiDurumunuDogruOkur(byte[]? bytes, bool expected)
    {
        Assert.Equal(expected, StartupPathPolicy.IsStartupApprovedEnabled(bytes));
    }

    [Fact]
    public void IsEntryValid_BeklenenSaplamaVeDosyaVarsaTrue()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var cmd = StartupPathPolicy.FormatCommandLine(stub);

        Assert.True(StartupPathPolicy.IsEntryValid(cmd, null, stub, _ => true));
    }

    [Fact]
    public void IsEntryValid_DosyaSilinmisseFalse()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var cmd = StartupPathPolicy.FormatCommandLine(stub);

        Assert.False(StartupPathPolicy.IsEntryValid(cmd, null, stub, _ => false));
    }

    [Fact]
    public void IsEntryValid_CurrentKlasoruneVeyaGelistirmeyeBakiyorsaFalse()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var currentCmd = StartupPathPolicy.FormatCommandLine(@"C:\Users\u\AppData\Local\Koy\current\Koy.exe");
        var devCmd = StartupPathPolicy.FormatCommandLine(@"C:\repo\koy\src\App\bin\Debug\net10.0\Koy.exe");

        Assert.False(StartupPathPolicy.IsEntryValid(currentCmd, null, stub, _ => true));
        Assert.False(StartupPathPolicy.IsEntryValid(devCmd, null, stub, _ => true));
    }

    [Fact]
    public void IsEntryValid_GorevYoneticisindeDevreDisiysaFalse()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var cmd = StartupPathPolicy.FormatCommandLine(stub);
        var disabled = new byte[] { 0x03, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

        Assert.False(StartupPathPolicy.IsEntryValid(cmd, disabled, stub, _ => true));
    }

    [Theory]
    [InlineData("\"C:\\Users\\u\\AppData\\Local\\Koy\\current\\Koy.exe\"", true)]
    [InlineData("\"C:\\repo\\src\\App\\bin\\Debug\\Koy.exe\"", true)]
    [InlineData("\"C:\\Users\\u\\Downloads\\Koy-Portable\\Koy.exe\"", true)]
    [InlineData("\"C:\\Users\\u\\AppData\\Local\\Koy\\Koy.exe\"", false)]
    public void ShouldMigrateInstalledEntry_EskiVeyaCurrentYolunuSaplamayaTasir(string regCmd, bool expected)
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";

        var result = StartupPathPolicy.ShouldMigrateInstalledEntry(
            regCmd,
            approvedBytes: null,
            expectedInstalledExePath: stub,
            fileExists: _ => true);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldMigrateInstalledEntry_SilinmisEskiYoluDaTasir()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var missingOld = "\"C:\\old\\OldName.exe\"";

        Assert.True(StartupPathPolicy.ShouldMigrateInstalledEntry(missingOld, null, stub, _ => false));
    }

    [Fact]
    public void ShouldMigrateInstalledEntry_GorevYoneticisindeKapatilmissaDokunmaz()
    {
        var stub = @"C:\Users\u\AppData\Local\Koy\Koy.exe";
        var currentCmd = "\"C:\\Users\\u\\AppData\\Local\\Koy\\current\\Koy.exe\"";
        var disabled = new byte[] { 0x03, 0, 0, 0 };

        Assert.False(StartupPathPolicy.ShouldMigrateInstalledEntry(currentCmd, disabled, stub, _ => true));
    }
}
