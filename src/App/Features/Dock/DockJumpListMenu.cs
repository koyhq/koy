using System.IO;
using System.Windows;
using System.Windows.Controls;
using Koy.App.Resources;
using Koy.Core.JumpList;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock simgesinin sağ tık menüsüne uygulamanın atlama listesini ekler. Sıra: uygulamanın kategorileri, sık / son
/// kullanılanlar, görevler, ardından Koy'un kendi öğeleri (menüde zaten bulunanlar). Liste yalnız menü açılırken okunur
/// (ayrı iş parçacığında, önbellekli); okuma yavaşsa menü Koy'un kendi öğeleriyle açılır, UI iş parçacığı beklemez.
/// </summary>
public static class DockJumpListMenu
{
    /// <summary>Atlama listesi öğelerini Koy'un kendi öğelerinden ayırt eder; her açılışta yeniden kurulur.</summary>
    private const string Marker = "koy-jumplist";

    /// <summary>Okuma bundan uzun sürerse menü listesiz açılır (sonuç yine de önbelleğe girer).</summary>
    private const int LoadTimeoutMs = 700;

    /// <summary>Menü uzamasın: atlama listesi satırları Koy'un kendi satırlarından (32) biraz sıkı.</summary>
    private const double RowHeight = 28;

    private static int _version;
    private static ControlTemplate? _headerTemplate;

    /// <summary>
    /// Menüyü atlama listesiyle doldurup açar. Yeni bir sağ tık gelirse (ya da pencere kapanırsa) eski çağrı menüyü açmaz.
    /// </summary>
    public static async Task OpenAsync(FrameworkElement target, ContextMenu menu, DockItemViewModel item, IJumpListService? service)
    {
        var version = ++_version;
        Clear(menu);

        var appId = item.Model.AppUserModelId;
        if (service is not null && !string.IsNullOrEmpty(appId))
        {
            var sections = await LoadAsync(service, appId);
            if (version != _version || !target.IsLoaded)
                return;

            var exe = item.Model.TargetPath;
            Populate(menu, sections, entry => service.Launch(entry, ExeOrNull(exe)));
        }

        // Liste menüyle birlikte bırakılır: kapanınca eklenen öğeler (ve yakaladıkları girdiler) atılır.
        void OnClosed(object? s, RoutedEventArgs e)
        {
            menu.Closed -= OnClosed;
            Clear(menu);
        }

        menu.Closed += OnClosed;
        DockContextMenuManager.Open(menu, target);
    }

    private static async Task<IReadOnlyList<JumpListSection>> LoadAsync(IJumpListService service, string appId)
    {
        try
        {
            var load = service.GetAsync(appId);
            if (load.IsCompleted)
                return await load; // önbellekten, bekleme yok

            using var timeout = new CancellationTokenSource();
            var finished = await Task.WhenAny(load, Task.Delay(LoadTimeoutMs, timeout.Token));
            timeout.Cancel();
            return finished == load ? await load : [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// <paramref name="sections"/> bölümlerini menünün başına ekler (Koy'un öğeleri altta kalır). Önceki atlama listesi
    /// öğeleri silinir. Başlıklar tıklanmaz; girdi tıklanınca <paramref name="onClick"/> çağrılır.
    /// </summary>
    public static void Populate(ContextMenu menu, IReadOnlyList<JumpListSection> sections, Action<JumpListEntry> onClick)
    {
        Clear(menu);

        var index = 0;
        foreach (var section in sections)
        {
            if (section.Entries.Count == 0)
                continue;

            var isFirst = index == 0;
            menu.Items.Insert(index++, Header(HeaderText(section), isFirst));
            foreach (var entry in section.Entries)
            {
                if (entry.IsSeparator)
                {
                    menu.Items.Insert(index++, new Separator { Tag = Marker });
                    continue;
                }

                var captured = entry;
                var row = new MenuItem
                {
                    Header = new TextBlock
                    {
                        Text = entry.Title,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 320,
                    },
                    ToolTip = entry.Title,
                    Height = RowHeight,
                    Tag = Marker,
                };
                row.Click += (_, _) => onClick(captured);
                menu.Items.Insert(index++, row);
            }
        }

        if (index > 0)
            menu.Items.Insert(index, new Separator { Tag = Marker });
    }

    /// <summary>Önceki atlama listesi öğelerini menüden çıkarır.</summary>
    public static void Clear(ContextMenu menu)
    {
        for (var i = menu.Items.Count - 1; i >= 0; i--)
        {
            if (menu.Items[i] is FrameworkElement { Tag: Marker })
                menu.Items.RemoveAt(i);
        }
    }

    private static string HeaderText(JumpListSection section) => section.Kind switch
    {
        JumpListSectionKind.Custom => section.Name ?? string.Empty,
        JumpListSectionKind.Frequent => Strings.Get("DockJumpListFrequent"),
        JumpListSectionKind.Recent => Strings.Get("DockJumpListRecent"),
        _ => Strings.Get("DockJumpListTasks"),
    };

    /// <summary>Bölüm başlığı: tıklanmayan, odaklanmayan, vurgulanmayan sönük küçük metin.</summary>
    private static MenuItem Header(string text, bool first) => new()
    {
        Header = text,
        Template = HeaderTemplate,
        // İlk başlıktan sonrakilerin üstünde bölümleri ayıran boşluk var.
        Margin = new Thickness(0, first ? 0 : 6, 0, 0),
        IsHitTestVisible = false,
        Focusable = false,
        Tag = Marker,
    };

    private static ControlTemplate HeaderTemplate => _headerTemplate ??= BuildHeaderTemplate();

    private static ControlTemplate BuildHeaderTemplate()
    {
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Header")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        label.SetValue(TextBlock.FontSizeProperty, 11.0);
        label.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 6, 12, 2));
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetResourceReference(TextBlock.ForegroundProperty, "KoyMenuTextMuted");

        var template = new ControlTemplate(typeof(MenuItem)) { VisualTree = label };
        template.Seal();
        return template;
    }

    /// <summary>Belge girdilerini uygulamanın exe'siyle açmak için yalnız var olan .exe yolu verilir.</summary>
    private static string? ExeOrNull(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) &&
                   path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                   File.Exists(path)
                ? path
                : null;
        }
        catch
        {
            return null;
        }
    }
}
