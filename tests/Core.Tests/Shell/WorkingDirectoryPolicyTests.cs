using Koy.Core.Shell;

namespace Koy.Core.Tests.Shell;

public class WorkingDirectoryPolicyTests
{
    [Fact]
    public void ResolveAppWorkingDirectory_KullaniciProfiliVarsaDonerVeBosluklariTemizler()
    {
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveAppWorkingDirectory(
            $"  {profile}  ",
            p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(profile, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveAppWorkingDirectory_BosVeyaNullProfildeNullDoner(string? profile)
    {
        Assert.Null(WorkingDirectoryPolicy.ResolveAppWorkingDirectory(profile, _ => true));
    }

    [Fact]
    public void ResolveAppWorkingDirectory_ProfilKlasoruYoksaNullDoner()
    {
        Assert.Null(WorkingDirectoryPolicy.ResolveAppWorkingDirectory(@"C:\Users\yok", _ => false));
    }

    [Fact]
    public void ResolveLaunchWorkingDirectory_AcikDizinGecerliyseOncelikliSecer()
    {
        var explicitDir = @"D:\Projects\app";
        var target = @"C:\Apps\tool\tool.exe";
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            target,
            $" {explicitDir} ",
            profile,
            p => string.Equals(p, explicitDir, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(p, @"C:\Apps\tool", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(explicitDir, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"D:\Missing\Dir")]
    public void ResolveLaunchWorkingDirectory_AcikDizinYokVeyaGecersizseKokluHedefinUstKlasoruneDuser(string? explicitDir)
    {
        var targetParent = @"C:\Apps\Antigravity";
        var target = @"C:\Apps\Antigravity\Antigravity.exe";
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            target,
            explicitDir,
            profile,
            p => string.Equals(p, targetParent, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(targetParent, resolved);
    }

    [Fact]
    public void ResolveLaunchWorkingDirectory_TirnakliHedefYolununUstKlasorunuCozumler()
    {
        var targetParent = @"C:\Program Files\App";
        var quotedTarget = "\"C:\\Program Files\\App\\app.exe\"";
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            quotedTarget,
            explicitWorkingDirectory: null,
            profile,
            p => string.Equals(p, targetParent, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(targetParent, resolved);
    }

    [Theory]
    [InlineData("taskmgr.exe")]
    [InlineData("cmd.exe")]
    [InlineData("https://github.com/koyhq/koy")]
    [InlineData("ms-settings:bluetooth")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsStore_8wekyb3d8bbwe!App")]
    [InlineData(@"C:\Missing\Folder\app.exe")]
    [InlineData(@"C:\")]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveLaunchWorkingDirectory_YalınKomutUriVeyaOlmayanUstKlasordeKullaniciProfilineDuser(string? target)
    {
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            target,
            explicitWorkingDirectory: null,
            profile,
            p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(profile, resolved);
    }

    [Theory]
    [InlineData("C:\\bad\0path\\app.exe")]
    [InlineData("<>|*?")]
    [InlineData("\"\"")]
    public void ResolveLaunchWorkingDirectory_GecersizKarakterlerdeFirlatmaz(string invalidTarget)
    {
        var profile = @"C:\Users\burhan";

        var resolved = WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            invalidTarget,
            explicitWorkingDirectory: "D:\\bad\0dir",
            profile,
            p => string.Equals(p, profile, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(profile, resolved);
    }

    [Fact]
    public void ResolveLaunchWorkingDirectory_HicbirDizinYoksaNullDoner()
    {
        Assert.Null(WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(
            @"C:\Apps\app.exe",
            @"D:\Work",
            @"C:\Users\burhan",
            _ => false));
    }

    [Fact]
    public void NullDirectoryExists_ArgumentNullExceptionFirlatir()
    {
        Assert.Throws<ArgumentNullException>(() => WorkingDirectoryPolicy.ResolveAppWorkingDirectory(@"C:\Users\u", null!));
        Assert.Throws<ArgumentNullException>(() => WorkingDirectoryPolicy.ResolveLaunchWorkingDirectory(@"C:\a.exe", null, @"C:\Users\u", null!));
    }
}
