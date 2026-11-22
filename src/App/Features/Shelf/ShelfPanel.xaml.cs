using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Koy.Core.Shelf;

namespace Koy.App.Features.Shelf;

public partial class ShelfPanel : UserControl
{
    private ShelfViewModel? ViewModel => DataContext as ShelfViewModel;

    public ShelfPanel()
    {
        InitializeComponent();
    }

    private void ShelfPanel_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void ShelfPanel_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel is null)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                ViewModel.HandleDrop(files, null);
            }
        }
        else if (e.Data.GetDataPresent(DataFormats.Text))
        {
            if (e.Data.GetData(DataFormats.Text) is string text)
            {
                ViewModel.HandleDrop(null, text);
            }
        }

        e.Handled = true;
    }

    private void Item_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element && element.DataContext is ShelfItem item)
        {
            if ((item.Kind is ShelfItemKind.File or ShelfItemKind.Directory) && (File.Exists(item.PathOrContent) || Directory.Exists(item.PathOrContent)))
            {
                var data = new DataObject(DataFormats.FileDrop, new string[] { item.PathOrContent });
                DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
            }
            else if (item.Kind == ShelfItemKind.Text)
            {
                var data = new DataObject(DataFormats.Text, item.PathOrContent);
                DragDrop.DoDragDrop(element, data, DragDropEffects.Copy);
            }
        }
    }

    // Enter / Boşluk açar, Delete raftan kaldırır (geri alınabilir).
    private void Item_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource != sender || sender is not FrameworkElement { DataContext: ShelfItem item })
            return;

        if (e.Key is Key.Enter or Key.Space)
        {
            ViewModel?.OpenItemCommand.Execute(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete)
        {
            ViewModel?.RemoveItemCommand.Execute(item.Id);
            e.Handled = true;
        }
    }

    private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is ShelfItem item)
        {
            ViewModel?.OpenItemCommand.Execute(item);
            e.Handled = true;
        }
    }
}
