using Koy.Core.Input;

namespace Koy.Core.Tests;

public class CustomActionParserTests
{
    private static CustomActionData Data(string? id, string? kind, string? target, string? args = null, string? dir = null) =>
        new(id, kind, target, args, dir);

    [Fact]
    public void Parse_GecerliKayitKabulEdilir()
    {
        var result = CustomActionParser.Parse([Data("build", "RunCommand", "dotnet", "build -c Release", @"C:\src")]);

        var action = Assert.Single(result.Accepted);
        Assert.Equal("build", action.Id);
        Assert.Equal(CustomActionKind.RunCommand, action.Kind);
        Assert.Equal("dotnet", action.Target);
        Assert.Equal("build -c Release", action.Arguments);
        Assert.Equal(@"C:\src", action.WorkingDirectory);
        Assert.Equal(0, result.Dropped);
    }

    [Fact]
    public void Parse_BosKimlikVeHedefDusurulur()
    {
        var result = CustomActionParser.Parse([Data(" ", "OpenPath", @"C:\x"), Data("a", "OpenPath", "  ")]);

        Assert.Empty(result.Accepted);
        Assert.Equal(2, result.Dropped);
    }

    [Fact]
    public void Parse_BilinmeyenTurDusurulur()
    {
        var result = CustomActionParser.Parse([Data("a", "RunPlugin", "x")]);

        Assert.Empty(result.Accepted);
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void Parse_KimlikTekilDir_IlkKalir()
    {
        var result = CustomActionParser.Parse([Data("Build", "OpenPath", @"C:\a"), Data("build", "OpenPath", @"C:\b")]);

        var action = Assert.Single(result.Accepted);
        Assert.Equal(@"C:\a", action.Target);
        Assert.Equal(1, result.Dropped);
    }

    [Fact]
    public void Parse_UrlYalnizHttpVeHttps()
    {
        var result = CustomActionParser.Parse(
        [
            Data("ok", "OpenUrl", "https://example.com/yol"),
            Data("ftp", "OpenUrl", "ftp://example.com"),
            Data("goreli", "OpenUrl", "/yol"),
            Data("dosya", "OpenUrl", @"C:\dosya.txt")
        ]);

        Assert.Equal(["ok"], result.Accepted.Select(a => a.Id).ToArray());
        Assert.Equal(3, result.Dropped);
    }

    [Fact]
    public void Parse_BosArgumanVeDizinNullOlur()
    {
        var action = Assert.Single(CustomActionParser.Parse([Data("a", "OpenPath", @"C:\x", "  ", "")]).Accepted);

        Assert.Null(action.Arguments);
        Assert.Null(action.WorkingDirectory);
    }

    [Fact]
    public void Parse_BosListe()
    {
        var result = CustomActionParser.Parse([]);

        Assert.Empty(result.Accepted);
        Assert.Equal(0, result.Dropped);
    }

    [Fact]
    public void Gecersiz_kayit_kimligi_rezerve_etmez_sonraki_gecerli_kayit_kabul_edilir()
    {
        var result = CustomActionParser.Parse([
            Data("link", "OpenUrl", "ftp://ornek.example/x"),
            Data("link", "OpenUrl", "https://ornek.example/x"),
        ]);

        var action = Assert.Single(result.Accepted);
        Assert.Equal("https://ornek.example/x", action.Target);
        Assert.Equal(1, result.Dropped);
    }
}
