using Koy.Core.Extensions;

namespace Koy.Core.Tests.Extensions;

public class ExtensionManifestValidatorTests
{
    private static bool Create(
        string? id = "my-theme",
        string? name = "My Theme",
        string? version = "1.0.0",
        string? author = "Ada",
        IEnumerable<string?>? capabilities = null,
        string? entryKind = "data")
    {
        return ExtensionManifestValidator.TryCreate(
            id, name, version, author, capabilities, entryKind, out _, out _);
    }

    private static ExtensionManifestError ErrorOf(
        string? id = "my-theme",
        string? name = "My Theme",
        string? version = "1.0.0",
        string? author = "Ada",
        IEnumerable<string?>? capabilities = null,
        string? entryKind = "data")
    {
        ExtensionManifestValidator.TryCreate(
            id, name, version, author, capabilities, entryKind, out _, out var error);
        return error;
    }

    [Fact]
    public void Valid_manifest_keeps_fields_as_given()
    {
        var ok = ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "0.2.6", "Ada",
            ["network", "clipboard"], "data", out var manifest, out var error);

        Assert.True(ok);
        Assert.Equal(ExtensionManifestError.None, error);
        Assert.NotNull(manifest);
        Assert.Equal("my-theme", manifest!.Id);
        Assert.Equal("My Theme", manifest.Name);
        Assert.Equal("0.2.6", manifest.Version);
        Assert.Equal("Ada", manifest.Author);
        Assert.Equal(new[] { ExtensionCapability.Network, ExtensionCapability.Clipboard }, manifest.Capabilities);
        Assert.Equal(ExtensionEntryKind.Data, manifest.EntryKind);
    }

    [Fact]
    public void Failed_create_returns_null_manifest()
    {
        var ok = ExtensionManifestValidator.TryCreate(
            "A", "x", "1.0.0", "a", null, "data", out var manifest, out var error);

        Assert.False(ok);
        Assert.Null(manifest);
        Assert.Equal(ExtensionManifestError.InvalidId, error);
    }

    [Fact]
    public void Missing_capabilities_mean_no_capabilities()
    {
        var ok = ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "1.0.0", "Ada", null, "data", out var manifest, out _);

        Assert.True(ok);
        Assert.Empty(manifest!.Capabilities);
    }

    [Theory]
    [InlineData(" My Theme")]
    [InlineData("My Theme ")]
    [InlineData("\tMy Theme")]
    [InlineData("My Theme\n")]
    public void Name_with_surrounding_whitespace_is_rejected_not_trimmed(string name)
    {
        Assert.Equal(ExtensionManifestError.InvalidName, ErrorOf(name: name));
    }

    [Theory]
    [InlineData(" Ada")]
    [InlineData("Ada ")]
    [InlineData("\tAda")]
    public void Author_with_surrounding_whitespace_is_rejected_not_trimmed(string author)
    {
        Assert.Equal(ExtensionManifestError.InvalidAuthor, ErrorOf(author: author));
    }

    [Fact]
    public void Capabilities_read_stops_once_limit_is_exceeded()
    {
        // Sonsuz bir kaynak: sınır aşılınca okuma durmazsa bu test takılır ya da sayım yanlış olur.
        var pulled = 0;
        IEnumerable<string?> Endless()
        {
            while (pulled < 10_000)
            {
                pulled++;
                yield return "network";
            }
        }

        Assert.Equal(ExtensionManifestError.TooManyCapabilities, ErrorOf(capabilities: Endless()));
        Assert.Equal(ExtensionManifestValidator.MaxCapabilities + 1, pulled);
    }

    [Fact]
    public void Manifests_with_same_content_are_equal_and_hash_the_same()
    {
        ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "1.0.0", "Ada", ["network", "files"], "data", out var first, out _);
        ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "1.0.0", "Ada", ["network", "files"], "data", out var second, out _);

        Assert.Equal(first, second);
        Assert.Equal(first!.GetHashCode(), second!.GetHashCode());
    }

    [Fact]
    public void Manifests_with_different_capabilities_are_not_equal()
    {
        ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "1.0.0", "Ada", ["network"], "data", out var first, out _);
        ExtensionManifestValidator.TryCreate(
            "my-theme", "My Theme", "1.0.0", "Ada", ["files"], "data", out var second, out _);

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("my-theme")]
    [InlineData("a1-b2")]
    [InlineData("x9")]
    public void Valid_ids_are_accepted(string id)
    {
        Assert.True(Create(id: id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("Theme")]
    [InlineData("1abc")]
    [InlineData("-abc")]
    [InlineData("my-")]
    [InlineData("my_theme")]
    [InlineData("my theme")]
    [InlineData("my.theme")]
    [InlineData("tema-ğ")]
    public void Invalid_ids_are_rejected(string? id)
    {
        Assert.Equal(ExtensionManifestError.InvalidId, ErrorOf(id: id));
    }

    [Fact]
    public void Id_longer_than_limit_is_rejected_and_limit_itself_is_accepted()
    {
        var atLimit = "a" + new string('b', ExtensionManifestValidator.MaxIdLength - 1);
        var overLimit = atLimit + "b";

        Assert.True(Create(id: atLimit));
        Assert.Equal(ExtensionManifestError.InvalidId, ErrorOf(id: overLimit));
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.2.6")]
    [InlineData("10.20.300")]
    [InlineData("0.0.0")]
    public void Valid_versions_are_accepted(string version)
    {
        Assert.True(Create(version: version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.x")]
    [InlineData("1..0")]
    [InlineData("1.0.-1")]
    [InlineData("1.0.1234567")]
    [InlineData(" 1.0.0")]
    public void Invalid_versions_are_rejected(string? version)
    {
        Assert.Equal(ExtensionManifestError.InvalidVersion, ErrorOf(version: version));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\tname")]
    public void Invalid_names_are_rejected(string? name)
    {
        Assert.Equal(ExtensionManifestError.InvalidName, ErrorOf(name: name));
    }

    [Fact]
    public void Name_longer_than_limit_is_rejected()
    {
        var name = new string('n', ExtensionManifestValidator.MaxNameLength + 1);

        Assert.Equal(ExtensionManifestError.InvalidName, ErrorOf(name: name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Invalid_authors_are_rejected(string? author)
    {
        Assert.Equal(ExtensionManifestError.InvalidAuthor, ErrorOf(author: author));
    }

    [Fact]
    public void Author_longer_than_limit_is_rejected()
    {
        var author = new string('a', ExtensionManifestValidator.MaxAuthorLength + 1);

        Assert.Equal(ExtensionManifestError.InvalidAuthor, ErrorOf(author: author));
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("Network")]
    [InlineData("network ")]
    [InlineData("")]
    [InlineData(null)]
    public void Unknown_capabilities_are_rejected(string? capability)
    {
        Assert.Equal(
            ExtensionManifestError.UnknownCapability,
            ErrorOf(capabilities: ["clipboard", capability]));
    }

    [Fact]
    public void Duplicate_capability_is_rejected()
    {
        Assert.Equal(
            ExtensionManifestError.DuplicateCapability,
            ErrorOf(capabilities: ["files", "files"]));
    }

    [Fact]
    public void Count_limit_is_checked_before_capability_names()
    {
        // Hepsi bilinmeyen olsa bile sayı sınırı önce bakılır: tek tek çözümlemeye gerek kalmaz.
        var tooMany = Enumerable.Repeat<string?>("shell", ExtensionManifestValidator.MaxCapabilities + 1);

        Assert.Equal(ExtensionManifestError.TooManyCapabilities, ErrorOf(capabilities: tooMany));
    }

    [Fact]
    public void Count_at_limit_is_not_reported_as_too_many()
    {
        // Tam sınırdaki liste sayı denetimini geçer; sonra yetki adı bilinmediği için UnknownCapability döner.
        var atLimit = Enumerable.Repeat<string?>("shell", ExtensionManifestValidator.MaxCapabilities);

        Assert.Equal(ExtensionManifestError.UnknownCapability, ErrorOf(capabilities: atLimit));
    }

    [Fact]
    public void Full_closed_set_of_capabilities_is_accepted()
    {
        Assert.Equal(
            ExtensionManifestError.None,
            ErrorOf(capabilities: ["network", "files", "clipboard"]));
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("out-of-process")]
    [InlineData("Data")]
    [InlineData(null)]
    public void Unknown_entry_kinds_are_rejected(string? entryKind)
    {
        Assert.Equal(ExtensionManifestError.UnknownEntryKind, ErrorOf(entryKind: entryKind));
    }

    [Fact]
    public void First_bad_field_is_reported()
    {
        // Kimlik ve sürüm birlikte bozuk: kimlik önce bildirilir.
        Assert.Equal(
            ExtensionManifestError.InvalidId,
            ErrorOf(id: "X", version: "bad"));
    }
}
