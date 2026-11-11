using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Dock;

public sealed class DockStartIconTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "koy-tests-" + Guid.NewGuid());

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Default_is_the_koy_icon_with_no_custom_path()
    {
        var s = new AppSettings();

        Assert.Equal(DockStartIcon.Koy, s.Dock.StartIcon);
        Assert.Equal("", s.Dock.StartIconPath);
        Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null), DockStartIconResolver.Resolve(s));
    }

    [Fact]
    public void Old_settings_file_without_the_fields_gets_the_koy_icon()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "DockStyle": "FullWidth", "DockShowStartButton": true }""");

        var loaded = new SettingsStore(SettingsPath).Load();

        Assert.Equal(DockStartIcon.Koy, loaded.Dock.StartIcon);
        Assert.Equal("", loaded.Dock.StartIconPath);
    }

    [Theory]
    [InlineData(DockStartIcon.Windows)]
    [InlineData(DockStartIcon.Custom)]
    public void Round_trips_by_name_with_path(DockStartIcon icon)
    {
        var store = new SettingsStore(SettingsPath);

        store.Save(new AppSettings { Dock = new DockSettings { StartIcon = icon, StartIconPath = @"C:\icons\baslat.png" } });

        Assert.Contains($"\"StartIcon\": \"{icon}\"", File.ReadAllText(SettingsPath));
        var loaded = store.Load();
        Assert.Equal(icon, loaded.Dock.StartIcon);
        Assert.Equal(@"C:\icons\baslat.png", loaded.Dock.StartIconPath);
    }

    [Fact]
    public void Icon_and_path_take_part_in_equality()
    {
        Assert.NotEqual(new AppSettings(), new AppSettings { Dock = new DockSettings { StartIcon = DockStartIcon.Windows } });
        Assert.NotEqual(new AppSettings(), new AppSettings { Dock = new DockSettings { StartIconPath = @"C:\a.png" } });
    }

    [Fact]
    public void Windows_choice_ignores_the_path()
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Windows, null),
            DockStartIconResolver.Resolve(DockStartIcon.Windows, @"C:\a.png", _ => true));

    [Fact]
    public void Koy_choice_ignores_the_path()
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null),
            DockStartIconResolver.Resolve(DockStartIcon.Koy, @"C:\a.png", _ => true));

    [Theory]
    [InlineData(@"C:\icons\a.png")]
    [InlineData(@"C:\icons\a.JPG")]
    [InlineData(@"C:\icons\a.jpeg")]
    [InlineData(@"C:\icons\a.ico")]
    public void Custom_with_an_existing_supported_file_uses_it(string path)
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Custom, path),
            DockStartIconResolver.Resolve(DockStartIcon.Custom, path, _ => true));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Custom_with_an_empty_path_falls_back_to_koy(string? path)
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null),
            DockStartIconResolver.Resolve(DockStartIcon.Custom, path, _ => true));

    [Fact]
    public void Custom_with_a_missing_file_falls_back_to_koy()
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null),
            DockStartIconResolver.Resolve(DockStartIcon.Custom, @"C:\icons\gone.png", _ => false));

    [Theory]
    [InlineData(@"C:\icons\a.svg")]
    [InlineData(@"C:\icons\a.exe")]
    [InlineData(@"C:\icons\noextension")]
    public void Custom_with_an_unsupported_extension_falls_back_to_koy(string path)
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null),
            DockStartIconResolver.Resolve(DockStartIcon.Custom, path, _ => true));

    [Fact]
    public void A_throwing_file_check_falls_back_to_koy()
        => Assert.Equal(new DockStartIconSource(DockStartIcon.Koy, null),
            DockStartIconResolver.Resolve(DockStartIcon.Custom, @"C:\icons\a.png", _ => throw new IOException()));

    [Fact]
    public void Real_file_system_check_finds_an_existing_file()
    {
        Directory.CreateDirectory(_dir);
        var file = Path.Combine(_dir, "a.png");
        File.WriteAllBytes(file, [1]);

        Assert.Equal(DockStartIcon.Custom, DockStartIconResolver.Resolve(DockStartIcon.Custom, file).Kind);
        Assert.Equal(DockStartIcon.Koy, DockStartIconResolver.Resolve(DockStartIcon.Custom, Path.Combine(_dir, "b.png")).Kind);
    }

    [Fact]
    public void Changing_the_icon_only_rebuilds_view_models()
    {
        foreach (var after in new[]
        {
            new AppSettings { Dock = new DockSettings { StartIcon = DockStartIcon.Windows } },
            new AppSettings { Dock = new DockSettings { StartIcon = DockStartIcon.Custom, StartIconPath = @"C:\a.png" } },
            new AppSettings { Dock = new DockSettings { StartIconPath = @"C:\a.png" } }
        })
        {
            var d = DockSettingsDiff.Compare(new AppSettings(), after);

            Assert.True(d.RebuildViewModels);
            Assert.False(d.ReconcileAppBar);
            Assert.False(d.ReconcileTaskbar);
            Assert.False(d.ReconcileMonitors);
            Assert.False(d.ReconcileTray);
            Assert.False(d.ReconcilePower);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
