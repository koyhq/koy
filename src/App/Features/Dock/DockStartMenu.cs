using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Koy.App.Resources;
using Koy.Core.Diagnostics;
using Koy.Core.StartMenu;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock'taki Başlat düğmesinin sağ tık menüsü: Windows'un Win+X menüsünün Koy tasarımında karşılığı.
/// Sıra, gruplar ve eylemler Core'daki <see cref="StartMenuModel"/>'den gelir; burası yalnız çizer ve menüyü
/// <see cref="DockContextMenuManager"/> ile açar (dock menü açıkken gizlenmez, dışarı tıklayınca kapanır).
/// </summary>
public static class DockStartMenu
{
    /// <summary>Menü 18 satır; 32 yerine biraz sıkı tutulur ki küçük ekranda da sığsın.</summary>
    private const double RowHeight = 30;

    /// <summary>Sağ tıkla menüyü açar. Her açılışta yeniden kurulur: pil durumu o an sorgulanır, açıkken hiçbir şey çalışmaz.</summary>
    public static void Open(DockViewModel? viewModel, FrameworkElement target)
    {
        if (viewModel?.StartContextMenu is not { } actions)
            return;

        try
        {
            viewModel.ClosePreviewNow();
            var menu = Build(actions.BuildMenu(), item => Run(actions, item));
            DockContextMenuManager.Open(menu, target);
        }
        catch (Exception ex)
        {
            // Menü açılamasa da dock çalışmaya devam eder.
            CrashLog.Write("Başlat sağ tık menüsü", ex);
        }
    }

    /// <summary>Modelden WPF menüsü kurar; <paramref name="onClick"/> yalnız eylem satırları için çağrılır.</summary>
    public static ContextMenu Build(IReadOnlyList<StartMenuGroup> groups, Action<StartMenuItem> onClick)
    {
        var menu = new ContextMenu();
        for (var i = 0; i < groups.Count; i++)
        {
            // ContextMenu ayırıcıya sistem stilini giydirir (parlak, taşan çizgi); Koy'un stili açıkça verilir.
            if (i > 0)
                menu.Items.Add(new Separator { Style = Application.Current?.TryFindResource(typeof(Separator)) as Style });

            foreach (var item in groups[i].Items)
                menu.Items.Add(CreateRow(item, onClick));
        }

        return menu;
    }

    private static MenuItem CreateRow(StartMenuItem item, Action<StartMenuItem> onClick)
    {
        var row = new MenuItem
        {
            Header = Strings.Get("StartItem" + item.Id),
            Height = RowHeight,
        };

        if (item.Children is { Count: > 0 } children)
        {
            foreach (var child in children)
                row.Items.Add(CreateRow(child, onClick));
        }
        else
        {
            row.Click += (_, _) => onClick(item);
        }

        return row;
    }

    private static void Run(StartMenuActions actions, StartMenuItem item)
    {
        DockContextMenuManager.CloseActive();

        // Menü pencereleri kapandıktan sonra, UI iş parçacığından ayrı: UAC istemi (Terminal Yönetici, Görev Yöneticisi)
        // yanıt bekleyen başlatma çağrısını kilitler, dock donmasın.
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => _ = Task.Run(() => actions.Execute(item))));
    }
}
