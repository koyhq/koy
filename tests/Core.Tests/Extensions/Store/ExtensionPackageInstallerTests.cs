using Koy.Core.Extensions.Store;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public sealed class ExtensionPackageInstallerTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly InstalledRegistryStore _registry;
    private readonly ExtensionPackageInstaller _installer;
    private readonly string _root;
    private DateTimeOffset _now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);

    public ExtensionPackageInstallerTests()
    {
        _root = _dir.Combine("extensions");
        _registry = new InstalledRegistryStore(_dir.Combine("extensions.json"));
        _installer = new ExtensionPackageInstaller(_root, _registry, () => _now);
    }

    public void Dispose() => _dir.Dispose();

    private InstallResult Install(MemoryStream zip, StoreItem? expected = null, ExtensionSource source = ExtensionSource.File) =>
        _installer.Install(zip, zip.Length, expected, source);

    private static MemoryStream ThemePackage(string id = "koyhq.ornek", string version = "1.0.0") =>
        Zip(F("manifest.json", Manifest(id, "theme", version)), F("theme.json", ThemeJson), F("preview.png", Png));

    private string[] LeftoverTemp() =>
        Directory.Exists(_root)
            ? Directory.GetFileSystemEntries(_root).Select(p => Path.GetFileName(p)).Where(n => n.StartsWith('.')).ToArray()
            : [];

    [Fact]
    public void Theme_installs_extracts_and_registers()
    {
        var result = Install(ThemePackage());

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("theme.json", result.Entry!.PayloadFile);
        Assert.Equal(ExtensionKind.Theme, result.Entry.Kind);
        Assert.Equal(_now, result.Entry.InstalledAt);
        Assert.True(File.Exists(Path.Combine(_root, "koyhq.ornek", "theme.json")));
        Assert.True(File.Exists(Path.Combine(_root, "koyhq.ornek", "manifest.json")));
        Assert.True(File.Exists(Path.Combine(_root, "koyhq.ornek", "preview.png")));
        Assert.Equal(result.Entry, _registry.Load().Find("koyhq.ornek"));
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Store_install_matches_the_index_item()
    {
        var item = Item("koyhq.ornek", ExtensionKind.Theme, "1.0.0");

        var result = Install(ThemePackage(), item, ExtensionSource.Store);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExtensionSource.Store, result.Entry!.Source);
    }

    [Theory]
    [InlineData("koyhq.baska", "1.0.0")]
    [InlineData("koyhq.ornek", "1.0.1")]
    public void Index_mismatch_installs_nothing(string id, string version)
    {
        var item = Item(id, ExtensionKind.Theme, version);

        var result = Install(ThemePackage(), item, ExtensionSource.Store);

        Assert.Equal(ExtensionInstallError.ManifestMismatch, result.Error);
        Assert.Empty(_registry.Load().Entries);
        Assert.False(Directory.Exists(Path.Combine(_root, "koyhq.ornek")));
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Kind_mismatch_with_index_is_rejected()
    {
        var result = Install(ThemePackage(), Item("koyhq.ornek", ExtensionKind.Profile), ExtensionSource.Store);

        Assert.Equal(ExtensionInstallError.ManifestMismatch, result.Error);
    }

    [Fact]
    public void Iconpack_installs_with_icons_folder_payload()
    {
        var zip = Zip(
            F("manifest.json", Manifest("koyhq.icons", "iconpack")),
            F("icons/Firefox.png", Png),
            F("icons/Code.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"));

        var result = Install(zip);

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("icons", result.Entry!.PayloadFile);
        Assert.True(File.Exists(Path.Combine(_root, "koyhq.icons", "icons", "Firefox.png")));
    }

    [Fact]
    public void Iconpack_with_wrong_signature_is_rejected_and_cleaned_up()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.icons", "iconpack")), F("icons/Firefox.png", "MZ not an image"));

        Assert.Equal(ExtensionInstallError.InvalidPayload, Install(zip).Error);
        Assert.False(Directory.Exists(Path.Combine(_root, "koyhq.icons")));
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Svg_with_script_is_rejected()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.icons", "iconpack")), F("icons/x.svg", "<svg><script>alert(1)</script></svg>"));

        Assert.Equal(ExtensionInstallError.InvalidPayload, Install(zip).Error);
    }

    [Fact]
    public void Background_installs_and_records_the_actual_file_name()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.bg", "background")), F("background.gif", Gif));

        var result = Install(zip);

        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("background.gif", result.Entry!.PayloadFile);
    }

    [Fact]
    public void Background_with_mismatching_signature_is_rejected()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.bg", "background")), F("background.mp4", Gif));

        Assert.Equal(ExtensionInstallError.InvalidPayload, Install(zip).Error);
    }

    [Fact]
    public void Profile_installs_when_codec_accepts_it_and_rejects_otherwise()
    {
        var good = Zip(F("manifest.json", Manifest("koyhq.prof", "profile")), F("profile.json", ProfileJson()));
        var bad = Zip(F("manifest.json", Manifest("koyhq.prof2", "profile")), F("profile.json", "{ \"Format\": \"x\" }"));

        Assert.True(Install(good).IsSuccess);
        Assert.Equal(ExtensionInstallError.InvalidPayload, Install(bad).Error);
        Assert.Null(_registry.Load().Find("koyhq.prof2"));
    }

    [Theory]
    [InlineData("{ \"NotAToken\": \"#112233\" }")]
    [InlineData("[]")]
    [InlineData("not json")]
    [InlineData("{}")]
    public void Theme_without_valid_entries_is_rejected(string themeJson)
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.ornek", "theme")), F("theme.json", themeJson));

        Assert.Equal(ExtensionInstallError.InvalidPayload, Install(zip).Error);
    }

    [Fact]
    public void Theme_with_utf8_bom_is_accepted()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes(ThemeJson)).ToArray();
        var zip = Zip(F("manifest.json", Manifest("koyhq.ornek", "theme")), F("theme.json", bytes));

        Assert.True(Install(zip).IsSuccess);
    }

    [Fact]
    public void Zip_slip_entry_is_rejected_and_nothing_is_written_outside()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.ornek", "theme")), F("theme.json", ThemeJson), F("../evil.txt", "x"));

        var result = Install(zip);

        Assert.Equal(ExtensionInstallError.UnsafePath, result.Error);
        Assert.False(File.Exists(_dir.Combine("evil.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Disallowed_executable_is_rejected()
    {
        var zip = Zip(F("manifest.json", Manifest("koyhq.ornek", "theme")), F("theme.json", ThemeJson), F("run.exe", "MZ"));

        Assert.Equal(ExtensionInstallError.DisallowedFile, Install(zip).Error);
    }

    [Fact]
    public void Package_requesting_capabilities_is_rejected()
    {
        var manifest = Manifest("koyhq.ornek", "theme").Replace("\"capabilities\": []", "\"capabilities\": [\"files\"]");
        var zip = Zip(F("manifest.json", manifest), F("theme.json", ThemeJson));

        Assert.Equal(ExtensionInstallError.CapabilitiesNotAllowed, Install(zip).Error);
    }

    [Fact]
    public void Missing_manifest_and_garbage_archives_fail_cleanly()
    {
        Assert.Equal(ExtensionInstallError.MissingManifest, Install(Zip(F("theme.json", ThemeJson))).Error);
        Assert.Equal(
            ExtensionInstallError.InvalidArchive,
            Install(new MemoryStream(System.Text.Encoding.UTF8.GetBytes("this is not a zip file at all"))).Error);
        Assert.Equal(ExtensionInstallError.InvalidArchive, Install(new MemoryStream()).Error);
        Assert.Empty(_registry.Load().Entries);
    }

    [Fact]
    public void Oversized_download_is_rejected_before_opening()
    {
        var zip = ThemePackage();

        Assert.Equal(
            ExtensionInstallError.DownloadTooLarge,
            _installer.Install(zip, PackageEntryPolicy.MaxDownloadBytes + 1, null, ExtensionSource.File).Error);
    }

    [Fact]
    public void Extracted_size_over_limit_is_rejected()
    {
        // 51 MB sıfır: zip'te çok küçük, açılınca sınırı aşar.
        var zip = Zip(F("manifest.json", Manifest("koyhq.bg", "background")), F("background.gif", [.. Gif, .. new byte[51 * 1024 * 1024]]));

        Assert.Equal(ExtensionInstallError.ExtractedTooLarge, Install(zip).Error);
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Update_replaces_old_files_and_keeps_applied_flag()
    {
        var first = Install(ThemePackage(version: "1.0.0"));
        _registry.Update(r => r.WithApplied("koyhq.ornek"));
        File.WriteAllText(Path.Combine(_root, "koyhq.ornek", "stale.txt"), "old");
        _now = _now.AddDays(1);

        var second = Install(ThemePackage(version: "1.1.0"));

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.True(second.PreviouslyApplied);
        Assert.Equal("1.1.0", _registry.Load().Find("koyhq.ornek")!.Version);
        Assert.True(_registry.Load().Find("koyhq.ornek")!.Applied);
        Assert.False(File.Exists(Path.Combine(_root, "koyhq.ornek", "stale.txt")));
        Assert.Single(_registry.Load().Entries);
        Assert.Empty(LeftoverTemp());
    }

    [Fact]
    public void Same_id_with_a_different_kind_is_a_conflict_and_keeps_the_old_install()
    {
        Install(ThemePackage());
        var profile = Zip(F("manifest.json", Manifest("koyhq.ornek", "profile")), F("profile.json", ProfileJson()));

        Assert.Equal(ExtensionInstallError.KindConflict, Install(profile).Error);
        Assert.Equal(ExtensionKind.Theme, _registry.Load().Find("koyhq.ornek")!.Kind);
        Assert.True(File.Exists(Path.Combine(_root, "koyhq.ornek", "theme.json")));
    }

    [Fact]
    public void Install_limit_of_64_is_enforced_for_new_ids_only()
    {
        for (var i = 0; i < 64; i++)
        {
            Assert.True(Install(ThemePackage("ext-" + i)).IsSuccess);
        }

        Assert.Equal(ExtensionInstallError.TooManyInstalled, Install(ThemePackage("one-more")).Error);
        Assert.False(Directory.Exists(Path.Combine(_root, "one-more")));
        Assert.True(Install(ThemePackage("ext-5", "2.0.0")).IsSuccess);
    }

    [Fact]
    public void Uninstall_removes_folder_and_registry_entry_only_for_registered_ids()
    {
        Install(ThemePackage("koyhq.one"));
        Install(ThemePackage("koyhq.two"));
        Directory.CreateDirectory(Path.Combine(_root, "unregistered"));

        Assert.True(_installer.Uninstall("koyhq.one"));

        Assert.False(Directory.Exists(Path.Combine(_root, "koyhq.one")));
        Assert.True(Directory.Exists(Path.Combine(_root, "koyhq.two")));
        Assert.Equal(["koyhq.two"], _registry.Load().Entries.Select(e => e.Id));

        Assert.False(_installer.Uninstall("unregistered"));
        Assert.True(Directory.Exists(Path.Combine(_root, "unregistered")));
        Assert.False(_installer.Uninstall("koyhq.one"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("..\\outside")]
    [InlineData("../outside")]
    [InlineData("")]
    [InlineData("C:\\Windows")]
    [InlineData(".")]
    public void Uninstall_refuses_ids_that_could_escape_the_root(string id)
    {
        Directory.CreateDirectory(_dir.Combine("outside"));

        Assert.False(_installer.Uninstall(id));
        Assert.True(Directory.Exists(_dir.Combine("outside")));
    }

    [Fact]
    public void Uninstall_of_registered_entry_with_missing_folder_still_clears_the_registry()
    {
        Install(ThemePackage());
        Directory.Delete(Path.Combine(_root, "koyhq.ornek"), recursive: true);

        Assert.True(_installer.Uninstall("koyhq.ornek"));
        Assert.Empty(_registry.Load().Entries);
    }

    [Fact]
    public void Stale_temp_folders_and_files_are_cleaned_but_fresh_ones_are_kept()
    {
        Directory.CreateDirectory(_root);
        var stale = Path.Combine(_root, ".tmp-old");
        var fresh = Path.Combine(_root, ".tmp-fresh");
        var staleFile = Path.Combine(_root, ".tmp-old.part");
        var keep = Path.Combine(_root, "koyhq.keep");
        foreach (var d in new[] { stale, fresh, keep })
        {
            Directory.CreateDirectory(d);
        }

        File.WriteAllText(staleFile, "x");
        var past = _now.UtcDateTime.AddHours(-3);
        Directory.SetLastWriteTimeUtc(stale, past);
        File.SetLastWriteTimeUtc(staleFile, past);
        Directory.SetLastWriteTimeUtc(fresh, _now.UtcDateTime);
        Directory.SetLastWriteTimeUtc(keep, past);

        _installer.CleanupStale();

        Assert.False(Directory.Exists(stale));
        Assert.False(File.Exists(staleFile));
        Assert.True(Directory.Exists(fresh));
        Assert.True(Directory.Exists(keep));
    }

    [Fact]
    public void Store_packages_in_the_repository_install_cleanly()
    {
        var root = RepoRoot();
        var packages = root is null ? null : Path.Combine(root, "store", "packages");
        if (packages is null || !Directory.Exists(packages))
        {
            return; // depo kökü yok: atla
        }

        var index = StoreIndexParser.Parse(File.ReadAllBytes(Path.Combine(root!, "store", "index.json")));
        Assert.True(index.IsSuccess);

        foreach (var item in index.Index!.Items)
        {
            var file = Path.Combine(packages, Path.GetFileName(new Uri(item.Package.Url).AbsolutePath));
            Assert.True(File.Exists(file), file);

            using var stream = File.OpenRead(file);
            Assert.Equal(item.Package.Sha256, Sha256(stream));
            Assert.Equal(item.Package.Size, stream.Length);

            var result = _installer.Install(stream, stream.Length, item, ExtensionSource.Store);

            Assert.True(result.IsSuccess, item.Id + ": " + result.Error);
            Assert.Equal(item.Kind, result.Entry!.Kind);
        }

        Assert.Equal(index.Index.Items.Count, _registry.Load().Entries.Count);
    }
}
