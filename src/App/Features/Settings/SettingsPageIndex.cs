using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using Koy.App.Resources;
using Koy.Core.Settings;

namespace Koy.App.Features.Settings;

/// <summary>Arama listesinde bir sonuç: satır başlığı ve sayfa adı.</summary>
public sealed record SettingsSearchResult(string Title, string Page, SettingsRow Row);

/// <summary>Sayfadaki bir ayar satırı: arama girdisi, kenarlık öğesi ve (bulunabildiyse) ayar anahtarı ("Dock.ShowTray").</summary>
public sealed record SettingsRow(SettingSearchEntry Entry, int PageIndex, FrameworkElement Element, string? SettingKey);

/// <summary>
/// Ayar sayfalarındaki satırları elle liste tutmadan sayfaların kendisinden çıkarır (issue #69): her
/// <c>SettingRowStyle</c> kenarlığı bir satırdır; başlığı <c>SettingTitleStyle</c> metninin Loc anahtarı, açıklaması
/// <c>SettingDescStyle</c> metni, anahtar sözcüğü bulunduğu bölümün başlığıdır. Metinler o anki dilde
/// <see cref="Strings"/>'ten okunur; dil değişince dizin yeniden kurulur. Satırın ayar anahtarı, içindeki denetimin
/// bağlandığı ViewModel özelliğinden <see cref="SettingsKeyMap.V3ToV4"/> ile bulunur (VM özellik adları düz v3 adlarıdır).
/// Mantık (eşleştirme, puan, değişenler) Core'da: <see cref="SettingsSearch"/>, <see cref="SettingsChanges"/>.
/// </summary>
public sealed class SettingsPageIndex
{
    private static readonly string[] PageNameKeys =
        ["NavGeneral", "NavNotch", "NavDock", "NavDisplays", "NavAppearance", "NavShortcuts", "NavDictation", "NavPerformance", "NavAbout", "NavAdvanced", "NavExtensions"];

    private static readonly DependencyProperty[] ValueProperties =
        [ToggleButton.IsCheckedProperty, Selector.SelectedItemProperty, Selector.SelectedValueProperty, RangeBase.ValueProperty, TextBox.TextProperty];

    private readonly List<SettingsRow> _rows = [];
    private readonly List<FrameworkElement> _headers = [];

    public IReadOnlyList<SettingsRow> Rows => _rows;

    /// <summary>Bölüm başlıkları (yalnız değişenler açıkken gizlenir).</summary>
    public IReadOnlyList<FrameworkElement> Headers => _headers;

    /// <summary>Dizinin kurulduğu dil; değişince yeniden kurulur.</summary>
    public string Culture { get; private set; } = "";

    public static string PageName(int index) =>
        index >= 0 && index < PageNameKeys.Length ? Strings.Get(PageNameKeys[index]) : "";

    public static SettingsPageIndex Build(Panel pagesHost)
    {
        var index = new SettingsPageIndex { Culture = System.Globalization.CultureInfo.CurrentUICulture.Name };
        var rowStyle = Application.Current?.TryFindResource("SettingRowStyle") as Style;
        var headerStyle = Application.Current?.TryFindResource("SectionHeaderStyle") as Style;
        var titleStyle = Application.Current?.TryFindResource("SettingTitleStyle") as Style;
        var descStyle = Application.Current?.TryFindResource("SettingDescStyle") as Style;

        for (var page = 0; page < pagesHost.Children.Count; page++)
        {
            var section = "";
            foreach (var element in Walk(pagesHost.Children[page]))
            {
                if (element is TextBlock header && header.Style == headerStyle && headerStyle is not null)
                {
                    section = Text(header);
                    index._headers.Add(header);
                    continue;
                }

                if (element is not Border row || row.Style != rowStyle || rowStyle is null)
                    continue;

                var inner = Walk(row).ToList();
                var title = inner.OfType<TextBlock>().FirstOrDefault(t => t.Style == titleStyle);
                if (title is null)
                    continue;
                var titleKey = LocKey(title) ?? "";
                var desc = inner.OfType<TextBlock>().FirstOrDefault(t => t.Style == descStyle);

                var entry = new SettingSearchEntry(
                    Key: $"{page}:{titleKey}:{index._rows.Count}",
                    Page: PageName(page),
                    Title: Text(title),
                    Description: desc is null ? "" : Text(desc),
                    Keywords: section.Length > 0 ? [section] : []);
                index._rows.Add(new SettingsRow(entry, page, row, SettingKeyOf(inner)));
            }
        }

        return index;
    }

    // Mantıksal ağaç: daraltılmış (hiç çizilmemiş) sayfalarda da tamdır.
    private static IEnumerable<DependencyObject> Walk(object root)
    {
        if (root is not DependencyObject start)
            yield break;

        var stack = new Stack<DependencyObject>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            var children = LogicalTreeHelper.GetChildren(current).OfType<DependencyObject>().Reverse();
            foreach (var child in children)
                stack.Push(child);
        }
    }

    // "{Binding [Anahtar], Source=Loc}" → "Anahtar"; o anki dilde Strings'ten okunur. Bağ yoksa görünen metin.
    private static string Text(TextBlock tb)
    {
        var key = LocKey(tb);
        return key is null ? tb.Text : Strings.Get(key);
    }

    private static string? LocKey(TextBlock tb)
    {
        var path = BindingOperations.GetBinding(tb, TextBlock.TextProperty)?.Path?.Path;
        return path is { Length: > 2 } && path[0] == '[' && path[^1] == ']' ? path[1..^1] : null;
    }

    private static string? SettingKeyOf(IEnumerable<DependencyObject> inner)
    {
        foreach (var d in inner)
        {
            foreach (var prop in ValueProperties)
            {
                var path = BindingOperations.GetBinding(d, prop)?.Path?.Path;
                if (string.IsNullOrEmpty(path))
                    continue;
                var root = path.Split('.')[0];
                if (SettingsKeyMap.V3ToV4.TryGetValue(root, out var target))
                    return $"{target.Section}.{target.Name}";
            }
        }

        return null;
    }
}
