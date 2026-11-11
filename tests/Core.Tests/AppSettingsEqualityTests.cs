using System.Collections;
using System.Reflection;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.Core.Tests;

/// <summary>
/// Ayar bölümleri record'dur; eşitlik alanlarından gelir. Yine de yeni bir alan ValueList ya da ValueDictionary
/// gibi elle yazılmış bir tipse, ya da eşitlik dışında bırakılırsa "değişmedi" sanılır. Bu testler her bölümdeki her
/// alanı tek tek değiştirir ve eşitliğin ve hash değerinin bunu gördüğünü doğrular.
/// </summary>
public class AppSettingsEqualityTests
{
    /// <summary>Her bölüm (General, Dock ...) ve bölümdeki her yazılabilir skaler alan.</summary>
    public static IEnumerable<object[]> ScalarFields() =>
        AllLeafFields().Where(f => IsScalar(f.Field.PropertyType)).Select(f => new object[] { f.Section, f.Field.Name });

    public static IEnumerable<object[]> ListFields() =>
        AllLeafFields().Where(f => IsList(f.Field.PropertyType)).Select(f => new object[] { f.Section, f.Field.Name });

    [Fact]
    public void Two_default_instances_are_equal_with_equal_hashes()
    {
        var a = new AppSettings();
        var b = new AppSettings();

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(ScalarFields))]
    public void Changing_any_scalar_field_makes_settings_unequal_and_changes_the_hash(string section, string fieldName)
    {
        var original = new AppSettings();
        var changed = new AppSettings();
        var sectionInstance = SectionOf(changed, section);
        var field = sectionInstance.GetType().GetProperty(fieldName)!;
        field.SetValue(sectionInstance, Different(field.PropertyType, field.GetValue(sectionInstance)));

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.GetHashCode(), changed.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(ListFields))]
    public void Changing_any_list_field_makes_settings_unequal_and_changes_the_hash(string section, string fieldName)
    {
        var original = new AppSettings();
        var changed = new AppSettings();
        var sectionInstance = SectionOf(changed, section);
        var field = sectionInstance.GetType().GetProperty(fieldName)!;
        var list = (IList)Activator.CreateInstance(field.PropertyType)!;
        list.Add(SampleItem(field.PropertyType));
        field.SetValue(sectionInstance, list);

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.GetHashCode(), changed.GetHashCode());
    }

    [Fact]
    public void Same_list_content_in_another_instance_is_equal()
    {
        var a = new AppSettings { QuickLaunch = new QuickLaunchSettings { Items = ["a.exe", "docs"] } };
        var b = new AppSettings { QuickLaunch = new QuickLaunchSettings { Items = ["a.exe", "docs"] } };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void List_order_matters()
    {
        var a = new AppSettings { DockItems = new DockItemsSettings { PinnedOrder = ["a.lnk", "b.lnk"] } };
        var b = new AppSettings { DockItems = new DockItemsSettings { PinnedOrder = ["b.lnk", "a.lnk"] } };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Changing_monitor_settings_makes_settings_unequal_and_changes_the_hash()
    {
        var changed = new AppSettings { Displays = new() { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = false } } } };
        var other = new AppSettings { Displays = new() { Monitors = new() { ["a"] = new MonitorSettings { DockEnabled = true } } } };

        Assert.NotEqual(new AppSettings(), changed);
        Assert.NotEqual(new AppSettings().GetHashCode(), changed.GetHashCode());
        Assert.NotEqual(changed, other);
        // Monitör kimliği büyük ve küçük harf duyarsız: aynı ekran tek kayıt sayılır.
        Assert.Equal(changed, new AppSettings { Displays = new() { Monitors = new() { ["A"] = new MonitorSettings { DockEnabled = false } } } });
    }

    [Fact]
    public void Changing_dock_folders_makes_settings_unequal_and_changes_the_hash()
    {
        var original = new AppSettings();
        var changed = new AppSettings
        {
            DockItems = new DockItemsSettings { Folders = [new DockFolder("f1", "Araçlar", ["calc.lnk", "notepad.lnk"])] }
        };

        Assert.NotEqual(original, changed);
        Assert.NotEqual(original.GetHashCode(), changed.GetHashCode());

        var same = new AppSettings
        {
            DockItems = new DockItemsSettings { Folders = [new DockFolder("f1", "Araçlar", ["calc.lnk", "notepad.lnk"])] }
        };
        Assert.Equal(changed, same);
        Assert.Equal(changed.GetHashCode(), same.GetHashCode());

        var differentName = new AppSettings
        {
            DockItems = new DockItemsSettings { Folders = [new DockFolder("f1", "Farklı", ["calc.lnk", "notepad.lnk"])] }
        };
        Assert.NotEqual(changed, differentName);

        var differentItems = new AppSettings
        {
            DockItems = new DockItemsSettings { Folders = [new DockFolder("f1", "Araçlar", ["calc.lnk"])] }
        };
        Assert.NotEqual(changed, differentItems);
    }

    [Fact]
    public void Changing_the_schema_version_makes_settings_unequal()
        => Assert.NotEqual(new AppSettings(), new AppSettings { Version = AppSettings.CurrentVersion - 1 });

    // ---- yardımcılar ----

    private static IEnumerable<(string Section, PropertyInfo Field)> AllLeafFields()
    {
        foreach (var section in typeof(AppSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanWrite && p.PropertyType.Namespace == typeof(AppSettings).Namespace && p.PropertyType.IsClass))
        {
            foreach (var field in section.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         .Where(p => p.CanWrite))
            {
                yield return (section.Name, field);
            }
        }
    }

    private static object SectionOf(AppSettings settings, string section) =>
        typeof(AppSettings).GetProperty(section)!.GetValue(settings)!;

    private static bool IsScalar(Type type) =>
        type == typeof(bool) || type == typeof(int) || type == typeof(long) || type == typeof(double)
        || type == typeof(string) || type.IsEnum;

    private static bool IsList(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueList<>);

    private static object SampleItem(Type listType)
    {
        var element = listType.GetGenericArguments()[0];
        if (element == typeof(string)) return "yeni";
        if (element == typeof(DockFolder)) return new DockFolder("yeni", "Yeni", ["x.lnk"]);
        throw new NotSupportedException(element.Name);
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
