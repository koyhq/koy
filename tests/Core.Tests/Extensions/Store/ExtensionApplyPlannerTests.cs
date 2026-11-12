using Koy.Core.Appearance;
using Koy.Core.Extensions.Store;
using Koy.Core.Notch;
using Koy.Core.Settings;
using static Koy.Core.Tests.Extensions.Store.StoreTestKit;

namespace Koy.Core.Tests.Extensions.Store;

public class ExtensionApplyPlannerTests
{
    private const string Folder = "C:\\Users\\u\\AppData\\Roaming\\Koy\\extensions\\koyhq.ornek";

    [Fact]
    public void Theme_apply_backs_up_copies_and_enables_overrides()
    {
        var current = new AppSettings();

        var plan = ExtensionApplyPlanner.Apply(current, Installed(kind: ExtensionKind.Theme, payload: "theme.json"), Folder);

        Assert.True(plan.IsSuccess);
        Assert.True(plan.Settings!.Appearance.UserStyleOverridesEnabled);
        Assert.Equal(ExtensionFileActionKind.BackupUserStyle, plan.FileActions[0].Kind);
        Assert.Equal(ExtensionFileActionKind.CopyThemeToUserStyle, plan.FileActions[1].Kind);
        Assert.Equal(Folder + "\\theme.json", plan.FileActions[1].SourcePath);
        Assert.False(current.Appearance.UserStyleOverridesEnabled);
    }

    [Fact]
    public void Iconpack_apply_points_to_icons_folder()
    {
        var plan = ExtensionApplyPlanner.Apply(new AppSettings(), Installed(kind: ExtensionKind.IconPack, payload: "icons"), Folder);

        Assert.Equal(IconPack.Folder, plan.Settings!.Appearance.IconPack);
        Assert.Equal(Folder + "\\icons", plan.Settings.Appearance.IconPackFolder);
        Assert.Empty(plan.FileActions);
    }

    [Fact]
    public void Background_apply_sets_local_file_and_defaults_target_to_notch_when_off()
    {
        var entry = Installed(kind: ExtensionKind.Background, payload: "background.gif");

        var plan = ExtensionApplyPlanner.Apply(new AppSettings(), entry, Folder);

        Assert.Equal(LiveBackgroundSourceKind.LocalFile, plan.Settings!.Notch.LiveBackgroundSource);
        Assert.Equal(Folder + "\\background.gif", plan.Settings.Notch.LiveBackgroundFilePath);
        Assert.Equal(LiveBackgroundTarget.Notch, plan.Settings.Notch.LiveBackgroundTarget);
    }

    [Fact]
    public void Background_apply_keeps_an_existing_target()
    {
        var current = new AppSettings();
        current.Notch.LiveBackgroundTarget = LiveBackgroundTarget.MusicCard;

        var plan = ExtensionApplyPlanner.Apply(current, Installed(kind: ExtensionKind.Background, payload: "background.webm"), Folder);

        Assert.Equal(LiveBackgroundTarget.MusicCard, plan.Settings!.Notch.LiveBackgroundTarget);
    }

    [Theory]
    [InlineData("")]
    [InlineData("background.exe")]
    [InlineData("..\\background.gif")]
    public void Background_apply_rejects_a_bad_payload_name(string payload)
    {
        var plan = ExtensionApplyPlanner.Apply(new AppSettings(), Installed(kind: ExtensionKind.Background, payload: payload), Folder);

        Assert.Equal(ExtensionApplyError.InvalidPath, plan.Error);
        Assert.Null(plan.Settings);
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative\\folder")]
    [InlineData("\\\\server\\share\\x")]
    [InlineData("C:\\a\\..\\b")]
    public void Apply_rejects_unsafe_install_folders(string folder)
    {
        Assert.Equal(
            ExtensionApplyError.InvalidPath,
            ExtensionApplyPlanner.Apply(new AppSettings(), Installed(kind: ExtensionKind.IconPack, payload: "icons"), folder).Error);
    }

    [Fact]
    public void Profile_apply_only_imports_and_leaves_settings_unchanged()
    {
        var current = new AppSettings();

        var plan = ExtensionApplyPlanner.Apply(current, Installed(kind: ExtensionKind.Profile, payload: "profile.json"), Folder);

        Assert.True(plan.IsSuccess);
        Assert.Equal(current, plan.Settings);
        var action = Assert.Single(plan.FileActions);
        Assert.Equal(ExtensionFileActionKind.ImportProfile, action.Kind);
        Assert.Equal(Folder + "\\profile.json", action.SourcePath);
    }

    [Fact]
    public void Apply_does_not_mutate_the_given_settings()
    {
        var current = new AppSettings();

        ExtensionApplyPlanner.Apply(current, Installed(kind: ExtensionKind.IconPack, payload: "icons"), Folder);
        ExtensionApplyPlanner.Apply(current, Installed(kind: ExtensionKind.Background, payload: "background.gif"), Folder);

        Assert.Equal(IconPack.Builtin, current.Appearance.IconPack);
        Assert.Equal(LiveBackgroundTarget.None, current.Notch.LiveBackgroundTarget);
        Assert.Equal("", current.Notch.LiveBackgroundFilePath);
    }

    // Kaldırma

    [Fact]
    public void Uninstalling_an_applied_iconpack_returns_to_builtin()
    {
        var entry = Installed(kind: ExtensionKind.IconPack, payload: "icons");
        var applied = ExtensionApplyPlanner.Apply(new AppSettings(), entry, Folder).Settings!;

        var plan = ExtensionApplyPlanner.RevertOnUninstall(applied, entry, Folder);

        Assert.Equal(IconPack.Builtin, plan.Settings!.Appearance.IconPack);
        Assert.Equal("", plan.Settings.Appearance.IconPackFolder);
    }

    [Fact]
    public void Uninstalling_an_iconpack_the_user_replaced_does_not_touch_settings()
    {
        var entry = Installed(kind: ExtensionKind.IconPack, payload: "icons");
        var current = new AppSettings();
        current.Appearance.IconPack = IconPack.Folder;
        current.Appearance.IconPackFolder = "D:\\Baska\\Paket";

        var plan = ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder);

        Assert.Equal("D:\\Baska\\Paket", plan.Settings!.Appearance.IconPackFolder);
        Assert.Equal(IconPack.Folder, plan.Settings.Appearance.IconPack);
    }

    [Fact]
    public void Icon_path_comparison_ignores_case_and_trailing_separator()
    {
        var entry = Installed(kind: ExtensionKind.IconPack, payload: "icons");
        var current = new AppSettings();
        current.Appearance.IconPack = IconPack.Folder;
        current.Appearance.IconPackFolder = (Folder + "\\ICONS\\").ToUpperInvariant();

        Assert.Equal(IconPack.Builtin, ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder).Settings!.Appearance.IconPack);
    }

    [Fact]
    public void Uninstalling_an_applied_background_returns_to_ambient()
    {
        var entry = Installed(kind: ExtensionKind.Background, payload: "background.gif");
        var applied = ExtensionApplyPlanner.Apply(new AppSettings(), entry, Folder).Settings!;

        var plan = ExtensionApplyPlanner.RevertOnUninstall(applied, entry, Folder);

        Assert.Equal(LiveBackgroundSourceKind.BuiltInAmbient, plan.Settings!.Notch.LiveBackgroundSource);
        Assert.Equal("", plan.Settings.Notch.LiveBackgroundFilePath);
        Assert.Equal(applied.Notch.LiveBackgroundTarget, plan.Settings.Notch.LiveBackgroundTarget);
    }

    [Fact]
    public void Uninstalling_a_background_the_user_replaced_does_not_touch_settings()
    {
        var entry = Installed(kind: ExtensionKind.Background, payload: "background.gif");
        var current = new AppSettings();
        current.Notch.LiveBackgroundSource = LiveBackgroundSourceKind.LocalFile;
        current.Notch.LiveBackgroundFilePath = "D:\\mine.gif";

        var plan = ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder);

        Assert.Equal("D:\\mine.gif", plan.Settings!.Notch.LiveBackgroundFilePath);
    }

    [Fact]
    public void Uninstalling_an_applied_theme_restores_backup_and_keeps_overrides_when_a_backup_exists()
    {
        var entry = Installed(kind: ExtensionKind.Theme, applied: true, payload: "theme.json");
        var current = new AppSettings();
        current.Appearance.UserStyleOverridesEnabled = true;

        var withBackup = ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder, userStyleBackupExists: true);
        var withoutBackup = ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder, userStyleBackupExists: false);

        Assert.Equal(ExtensionFileActionKind.RestoreUserStyleBackup, Assert.Single(withBackup.FileActions).Kind);
        Assert.True(withBackup.Settings!.Appearance.UserStyleOverridesEnabled);
        Assert.Equal(ExtensionFileActionKind.RestoreUserStyleBackup, Assert.Single(withoutBackup.FileActions).Kind);
        Assert.False(withoutBackup.Settings!.Appearance.UserStyleOverridesEnabled);
    }

    [Fact]
    public void Uninstalling_a_theme_that_was_not_applied_does_nothing()
    {
        var entry = Installed(kind: ExtensionKind.Theme, applied: false, payload: "theme.json");
        var current = new AppSettings();
        current.Appearance.UserStyleOverridesEnabled = true;

        var plan = ExtensionApplyPlanner.RevertOnUninstall(current, entry, Folder);

        Assert.Empty(plan.FileActions);
        Assert.True(plan.Settings!.Appearance.UserStyleOverridesEnabled);
    }

    [Fact]
    public void Uninstalling_a_profile_does_nothing()
    {
        var plan = ExtensionApplyPlanner.RevertOnUninstall(new AppSettings(), Installed(kind: ExtensionKind.Profile, payload: "profile.json"), Folder);

        Assert.True(plan.IsSuccess);
        Assert.Empty(plan.FileActions);
    }

    [Fact]
    public void IsActive_follows_settings_for_icons_and_background_and_flag_for_theme()
    {
        var icons = Installed(kind: ExtensionKind.IconPack, payload: "icons");
        var bg = Installed(kind: ExtensionKind.Background, payload: "background.gif");
        var theme = Installed(kind: ExtensionKind.Theme, applied: true, payload: "theme.json");
        var settings = new AppSettings();

        Assert.False(ExtensionApplyPlanner.IsActive(settings, icons, Folder));
        Assert.False(ExtensionApplyPlanner.IsActive(settings, bg, Folder));
        Assert.False(ExtensionApplyPlanner.IsActive(settings, theme, Folder));

        settings = ExtensionApplyPlanner.Apply(settings, icons, Folder).Settings!;
        settings = ExtensionApplyPlanner.Apply(settings, bg, Folder).Settings!;
        settings = ExtensionApplyPlanner.Apply(settings, theme, Folder).Settings!;

        Assert.True(ExtensionApplyPlanner.IsActive(settings, icons, Folder));
        Assert.True(ExtensionApplyPlanner.IsActive(settings, bg, Folder));
        Assert.True(ExtensionApplyPlanner.IsActive(settings, theme, Folder));
        Assert.False(ExtensionApplyPlanner.IsActive(settings, Installed(kind: ExtensionKind.Profile), Folder));
    }
}
