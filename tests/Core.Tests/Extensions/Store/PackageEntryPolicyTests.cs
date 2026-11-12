using Koy.Core.Extensions.Store;

namespace Koy.Core.Tests.Extensions.Store;

public class PackageEntryPolicyTests
{
    private static PackageEntry E(string name, long length = 10) => new(name, length);

    private static PackageCheck CheckTheme(params PackageEntry[] entries) =>
        PackageEntryPolicy.Check(entries, ExtensionKind.Theme, 1000);

    private static readonly PackageEntry[] ValidTheme = [E("manifest.json"), E("theme.json")];

    [Fact]
    public void Valid_packages_of_every_kind_pass()
    {
        Assert.True(PackageEntryPolicy.Check([E("manifest.json"), E("theme.json"), E("preview.png")], ExtensionKind.Theme, 1).IsOk);
        Assert.True(PackageEntryPolicy.Check([E("manifest.json"), E("profile.json")], ExtensionKind.Profile, 1).IsOk);
        Assert.True(PackageEntryPolicy.Check([E("manifest.json"), E("icons/Firefox.png"), E("icons/Code.ICO"), E("icons/x.svg")], ExtensionKind.IconPack, 1).IsOk);
        Assert.True(PackageEntryPolicy.Check([E("manifest.json"), E("background.webm")], ExtensionKind.Background, 1).IsOk);
    }

    [Theory]
    [InlineData("../evil.json")]
    [InlineData("icons/../../evil.png")]
    [InlineData("..")]
    [InlineData("/etc/passwd")]
    [InlineData("/theme.json")]
    [InlineData("C:/Windows/x.png")]
    [InlineData("C:\\Windows\\x.png")]
    [InlineData("c:evil.png")]
    [InlineData("icons\\a.png")]
    [InlineData("icons/a.png:stream")]
    [InlineData("theme.json:hidden")]
    [InlineData("")]
    [InlineData("./theme.json")]
    [InlineData("icons//a.png")]
    [InlineData("icons/a.png.")]
    [InlineData("icons/a.png ")]
    [InlineData("icons/a\0.png")]
    [InlineData("icons/a|b.png")]
    [InlineData("\\\\server\\share\\x.png")]
    public void Zip_slip_and_unsafe_names_are_rejected(string name)
    {
        var result = PackageEntryPolicy.CheckCommon([E("manifest.json"), E(name)], 1);

        Assert.Equal(ExtensionInstallError.UnsafePath, result.Error);
        Assert.Equal(name, result.Entry);
    }

    [Fact]
    public void Unsafe_name_in_full_check_is_also_rejected()
    {
        Assert.Equal(ExtensionInstallError.UnsafePath, CheckTheme(E("manifest.json"), E("theme.json"), E("../x")).Error);
    }

    [Fact]
    public void Download_size_limit_is_inclusive()
    {
        Assert.True(PackageEntryPolicy.Check(ValidTheme, ExtensionKind.Theme, PackageEntryPolicy.MaxDownloadBytes).IsOk);
        Assert.Equal(
            ExtensionInstallError.DownloadTooLarge,
            PackageEntryPolicy.Check(ValidTheme, ExtensionKind.Theme, PackageEntryPolicy.MaxDownloadBytes + 1).Error);
    }

    [Fact]
    public void Extracted_total_limit_is_inclusive()
    {
        PackageEntry[] atLimit = [E("manifest.json", 100), E("background.gif", PackageEntryPolicy.MaxExtractedBytes - 100)];
        PackageEntry[] over = [E("manifest.json", 101), E("background.gif", PackageEntryPolicy.MaxExtractedBytes - 100)];

        Assert.True(PackageEntryPolicy.Check(atLimit, ExtensionKind.Background, 1).IsOk);
        Assert.Equal(ExtensionInstallError.ExtractedTooLarge, PackageEntryPolicy.Check(over, ExtensionKind.Background, 1).Error);
    }

    [Fact]
    public void Total_does_not_overflow_with_huge_declared_lengths()
    {
        var result = PackageEntryPolicy.CheckCommon([E("manifest.json", long.MaxValue / 2 + 1), E("a.bin", long.MaxValue / 2 + 1)], 1);

        Assert.Equal(ExtensionInstallError.ExtractedTooLarge, result.Error);
    }

    [Fact]
    public void File_count_limit_is_inclusive()
    {
        PackageEntry[] Icons(int count) => [E("manifest.json"), .. Enumerable.Range(0, count - 1).Select(i => E($"icons/i{i}.png", 1))];

        Assert.True(PackageEntryPolicy.Check(Icons(PackageEntryPolicy.MaxFiles), ExtensionKind.IconPack, 1).IsOk);
        Assert.Equal(
            ExtensionInstallError.TooManyFiles,
            PackageEntryPolicy.Check(Icons(PackageEntryPolicy.MaxFiles + 1), ExtensionKind.IconPack, 1).Error);
    }

    [Fact]
    public void Negative_declared_length_is_invalid()
    {
        Assert.Equal(ExtensionInstallError.InvalidArchive, PackageEntryPolicy.CheckCommon([E("manifest.json", -1)], 1).Error);
    }

    [Fact]
    public void Duplicate_names_are_rejected_case_insensitively()
    {
        Assert.Equal(ExtensionInstallError.DuplicateEntry, CheckTheme(E("manifest.json"), E("theme.json"), E("THEME.JSON")).Error);
        Assert.Equal(
            ExtensionInstallError.DuplicateEntry,
            PackageEntryPolicy.Check([E("manifest.json"), E("icons/a.png"), E("icons/A.png")], ExtensionKind.IconPack, 1).Error);
    }

    [Fact]
    public void Missing_manifest_is_rejected()
    {
        Assert.Equal(ExtensionInstallError.MissingManifest, CheckTheme(E("theme.json")).Error);
        Assert.Equal(ExtensionInstallError.MissingManifest, CheckTheme(E("Manifest.json"), E("theme.json")).Error);
    }

    [Fact]
    public void Missing_payload_is_rejected_for_every_kind()
    {
        Assert.Equal(ExtensionInstallError.MissingPayload, PackageEntryPolicy.Check([E("manifest.json")], ExtensionKind.Theme, 1).Error);
        Assert.Equal(ExtensionInstallError.MissingPayload, PackageEntryPolicy.Check([E("manifest.json"), E("preview.png")], ExtensionKind.Profile, 1).Error);
        Assert.Equal(ExtensionInstallError.MissingPayload, PackageEntryPolicy.Check([E("manifest.json"), E("icons/")], ExtensionKind.IconPack, 1).Error);
        Assert.Equal(ExtensionInstallError.MissingPayload, PackageEntryPolicy.Check([E("manifest.json")], ExtensionKind.Background, 1).Error);
    }

    [Fact]
    public void Background_needs_exactly_one_file()
    {
        Assert.Equal(
            ExtensionInstallError.MultiplePayloads,
            PackageEntryPolicy.Check([E("manifest.json"), E("background.gif"), E("background.webm")], ExtensionKind.Background, 1).Error);
    }

    [Theory]
    [InlineData("background.png")]
    [InlineData("background.exe")]
    [InlineData("bg.gif")]
    [InlineData("Background.gif")]
    [InlineData("sub/background.gif")]
    [InlineData("background")]
    public void Wrong_background_files_are_disallowed(string name)
    {
        Assert.Equal(
            ExtensionInstallError.DisallowedFile,
            PackageEntryPolicy.Check([E("manifest.json"), E(name)], ExtensionKind.Background, 1).Error);
    }

    [Theory]
    [InlineData("icons/a.exe")]
    [InlineData("icons/a.dll")]
    [InlineData("icons/a")]
    [InlineData("icons/sub/a.png")]
    [InlineData("icons/NUL.png")]
    [InlineData("icons/con.ico")]
    [InlineData("a.png")]
    [InlineData("Icons/a.png")]
    public void Wrong_icon_files_are_disallowed(string name)
    {
        Assert.Equal(
            ExtensionInstallError.DisallowedFile,
            PackageEntryPolicy.Check([E("manifest.json"), E("icons/ok.png"), E(name)], ExtensionKind.IconPack, 1).Error);
    }

    [Fact]
    public void Files_of_another_kind_are_disallowed()
    {
        Assert.Equal(ExtensionInstallError.DisallowedFile, PackageEntryPolicy.Check([E("manifest.json"), E("theme.json"), E("profile.json")], ExtensionKind.Theme, 1).Error);
        Assert.Equal(ExtensionInstallError.DisallowedFile, PackageEntryPolicy.Check([E("manifest.json"), E("profile.json"), E("theme.json")], ExtensionKind.Profile, 1).Error);
        Assert.Equal(ExtensionInstallError.DisallowedFile, CheckTheme(E("manifest.json"), E("theme.json"), E("run.ps1")).Error);
        Assert.Equal(ExtensionInstallError.DisallowedFile, CheckTheme(E("manifest.json"), E("theme.json"), E("Preview.PNG")).Error);
    }

    [Fact]
    public void Directory_entries_are_allowed_only_as_icons_folder_of_iconpack()
    {
        Assert.True(PackageEntryPolicy.Check([E("manifest.json"), E("icons/", 0), E("icons/a.png")], ExtensionKind.IconPack, 1).IsOk);
        Assert.Equal(ExtensionInstallError.DisallowedFile, CheckTheme(E("manifest.json"), E("theme.json"), E("icons/", 0)).Error);
        Assert.Equal(
            ExtensionInstallError.DisallowedFile,
            PackageEntryPolicy.Check([E("manifest.json"), E("other/", 0), E("icons/a.png")], ExtensionKind.IconPack, 1).Error);
        Assert.Equal(ExtensionInstallError.UnsafePath, PackageEntryPolicy.CheckCommon([E("manifest.json"), E("../", 0)], 1).Error);
    }

    [Fact]
    public void Per_file_size_limits_are_enforced()
    {
        Assert.Equal(
            ExtensionInstallError.FileTooLarge,
            PackageEntryPolicy.Check([E("manifest.json", PackageEntryPolicy.MaxManifestBytes + 1), E("theme.json")], ExtensionKind.Theme, 1).Error);
        Assert.Equal(
            ExtensionInstallError.FileTooLarge,
            PackageEntryPolicy.Check([E("manifest.json"), E("theme.json", Koy.Core.Appearance.UserStyleParser.MaxBytes + 1)], ExtensionKind.Theme, 1).Error);
        Assert.Equal(
            ExtensionInstallError.FileTooLarge,
            PackageEntryPolicy.Check([E("manifest.json"), E("icons/a.png", PackageEntryPolicy.MaxIconBytes + 1)], ExtensionKind.IconPack, 1).Error);
        Assert.True(
            PackageEntryPolicy.Check([E("manifest.json", PackageEntryPolicy.MaxManifestBytes), E("theme.json", Koy.Core.Appearance.UserStyleParser.MaxBytes)], ExtensionKind.Theme, 1).IsOk);
    }

    [Fact]
    public void Name_length_limit_is_enforced()
    {
        var longName = "icons/" + new string('a', PackageEntryPolicy.MaxEntryNameLength) + ".png";

        Assert.Equal(ExtensionInstallError.UnsafePath, PackageEntryPolicy.CheckCommon([E("manifest.json"), E(longName)], 1).Error);
    }

    [Fact]
    public void Empty_entry_list_has_no_manifest()
    {
        Assert.Equal(ExtensionInstallError.MissingManifest, PackageEntryPolicy.CheckCommon([], 1).Error);
    }
}
