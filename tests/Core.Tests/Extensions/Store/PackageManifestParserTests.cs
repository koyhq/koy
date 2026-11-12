using Koy.Core.Extensions;
using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class PackageManifestParserTests
{
    [Fact]
    public void Valid_manifest_is_parsed_with_kind_and_license()
    {
        var result = PackageManifestParser.Parse(Manifest("koyhq.nord-theme", "theme"));

        Assert.True(result.IsValid);
        Assert.Equal("koyhq.nord-theme", result.Manifest!.Manifest.Id);
        Assert.Equal(ExtensionKind.Theme, result.Manifest.Kind);
        Assert.Equal("MIT", result.Manifest.License);
        Assert.Equal(ExtensionEntryKind.Data, result.Manifest.Manifest.EntryKind);
    }

    [Fact]
    public void Missing_entry_kind_and_capabilities_default_to_data_with_none()
    {
        var json = "{ \"id\": \"ab\", \"name\": \"N\", \"version\": \"1.0.0\", \"author\": \"A\", \"kind\": \"profile\", \"license\": \"MIT\" }";

        Assert.True(PackageManifestParser.Parse(json).IsValid);
    }

    [Fact]
    public void Capabilities_are_refused_for_data_packages()
    {
        var json = Manifest("ab", "theme").Replace("\"capabilities\": []", "\"capabilities\": [\"network\"]");

        Assert.Equal(ExtensionInstallError.CapabilitiesNotAllowed, PackageManifestParser.Parse(json).Error);
    }

    [Fact]
    public void Unknown_capability_is_an_invalid_manifest()
    {
        var json = Manifest("ab", "theme").Replace("\"capabilities\": []", "\"capabilities\": [\"shell\"]");

        var result = PackageManifestParser.Parse(json);

        Assert.Equal(ExtensionInstallError.InvalidManifest, result.Error);
        Assert.Equal(ExtensionManifestError.UnknownCapability, result.ManifestError);
    }

    [Theory]
    [InlineData("\"id\": \"ab\"", "\"id\": \"Bad Id\"")]
    [InlineData("\"version\": \"1.0.0\"", "\"version\": \"1.0\"")]
    [InlineData("\"kind\": \"theme\"", "\"kind\": \"widget\"")]
    [InlineData("\"license\": \"MIT\"", "\"license\": \"\"")]
    [InlineData("\"entryKind\": \"data\"", "\"entryKind\": \"process\"")]
    [InlineData("\"capabilities\": []", "\"capabilities\": \"network\"")]
    public void Invalid_fields_make_manifest_invalid(string from, string to)
    {
        var json = Manifest("ab", "theme", extra: ", \"entryKind\": \"data\"").Replace(from, to);

        Assert.Equal(ExtensionInstallError.InvalidManifest, PackageManifestParser.Parse(json).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{}")]
    public void Garbage_is_an_invalid_manifest(string json)
    {
        Assert.Equal(ExtensionInstallError.InvalidManifest, PackageManifestParser.Parse(json).Error);
    }

    [Fact]
    public void Null_text_does_not_throw()
    {
        Assert.False(PackageManifestParser.Parse((string?)null).IsValid);
    }

    [Fact]
    public void Oversized_manifest_is_refused()
    {
        var json = Manifest("ab", "theme") + new string(' ', (int)PackageEntryPolicy.MaxManifestBytes);

        Assert.Equal(ExtensionInstallError.FileTooLarge, PackageManifestParser.Parse(json).Error);
    }

    [Fact]
    public void Match_requires_same_id_version_and_kind()
    {
        var package = PackageManifestParser.Parse(Manifest("koyhq.ornek", "theme", "1.0.0")).Manifest!;

        Assert.Equal(ExtensionInstallError.None, PackageManifestParser.Match(package, Item()));
        Assert.Equal(ExtensionInstallError.ManifestMismatch, PackageManifestParser.Match(package, Item(id: "koyhq.baska")));
        Assert.Equal(ExtensionInstallError.ManifestMismatch, PackageManifestParser.Match(package, Item(version: "1.0.1")));
        Assert.Equal(ExtensionInstallError.ManifestMismatch, PackageManifestParser.Match(package, Item(kind: ExtensionKind.Profile)));
    }

    [Fact]
    public void Name_and_author_differences_are_not_a_mismatch()
    {
        var package = PackageManifestParser.Parse(Manifest("koyhq.ornek", "theme")).Manifest!;

        Assert.Equal(ExtensionInstallError.None, PackageManifestParser.Match(package, Item(name: "Baska ad", author: "Baska")));
    }
}
