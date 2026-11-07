using System.Reflection;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

/// <summary>
/// AppSettings.Equals ve GetHashCode elle yazılı (listeler için). Yeni bir alan eklenip karşılaştırmaya
/// eklenmezse ayarlar "değişmedi" sanılır; bu test her alanı tek tek değiştirerek bunu yakalar.
/// </summary>
public class AppSettingsEqualityTests
{
    public static IEnumerable<object[]> ScalarProperties() =>
        typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.PropertyType != typeof(List<string>) &&
                        p.PropertyType != typeof(List<DockFolder>) &&
                        p.PropertyType != typeof(Dictionary<string, MonitorSettings>))
            .Select(p => new object[] { p.Name });

    [Theory]
    [MemberData(nameof(ScalarProperties))]
    public void Changing_any_field_makes_settings_unequal_and_changes_the_hash(string propertyName)
    {
        var property = typeof(AppSettings).GetProperty(propertyName)!;
        var original = new AppSettings();
        var changed = new AppSettings();
        property.SetValue(changed, Different(property.PropertyType, property.GetValue(changed)));

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.GetHashCode(), changed.GetHashCode());
    }

    [Theory]
    [InlineData(nameof(AppSettings.EnabledPanels))]
    [InlineData(nameof(AppSettings.ControlCenterLayout))]
    [InlineData(nameof(AppSettings.DockPinnedOrder))]
    [InlineData(nameof(AppSettings.QuickLaunchItems))]
    public void Changing_a_list_field_makes_settings_unequal(string propertyName)
    {
        var property = typeof(AppSettings).GetProperty(propertyName)!;
        var changed = new AppSettings();
        property.SetValue(changed, new List<string> { "yeni" });

        Assert.NotEqual(new AppSettings(), changed);
    }

    [Fact]
    public void Changing_monitor_settings_makes_settings_unequal_and_changes_the_hash()
    {
        var changed = new AppSettings { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = false } } };
        var other = new AppSettings { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = true } } };

        Assert.NotEqual(new AppSettings(), changed);
        Assert.NotEqual(new AppSettings().GetHashCode(), changed.GetHashCode());
        Assert.NotEqual(changed, other);
        Assert.Equal(changed, new AppSettings { Monitors = new() { ["A"] = new MonitorSettings { DockEnabled = false } } });
    }

    [Fact]
    public void Changing_dock_folders_makes_settings_unequal_and_changes_the_hash()
    {
        var original = new AppSettings();
        var changed = new AppSettings
        {
            DockFolders = [new DockFolder("f1", "Araçlar", ["calc.lnk", "notepad.lnk"])]
        };

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.GetHashCode(), changed.GetHashCode());

        var same = new AppSettings
        {
            DockFolders = [new DockFolder("f1", "Araçlar", ["calc.lnk", "notepad.lnk"])]
        };
        Assert.Equal(changed, same);
        Assert.Equal(changed.GetHashCode(), same.GetHashCode());

        var differentName = new AppSettings
        {
            DockFolders = [new DockFolder("f1", "Farklı", ["calc.lnk", "notepad.lnk"])]
        };
        Assert.NotEqual(changed, differentName);

        var differentItems = new AppSettings
        {
            DockFolders = [new DockFolder("f1", "Araçlar", ["calc.lnk"])]
        };
        Assert.NotEqual(changed, differentItems);
    }

    private static object? Different(Type type, object? value)
    {
        if (type == typeof(bool)) return !(bool)value!;
        if (type == typeof(int)) return (int)value! + 1;
        if (type == typeof(long)) return (long)value! + 1;
        if (type == typeof(double)) return (double)value! + 1.0;
        if (type == typeof(string)) return ((string?)value ?? "") + "x";
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue((Array.IndexOf(values, value) + 1) % values.Length);
        }
        throw new NotSupportedException(type.Name);
    }
}
