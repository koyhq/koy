using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Shelf;

namespace Koy.App.Features.Shelf;

public sealed partial class ShelfViewModel : ObservableObject, IDisposable
{
    private readonly IShelfService _shelfService;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public ObservableCollection<ShelfItem> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotEmpty))]
    private bool _isEmpty = true;

    public bool IsNotEmpty => !IsEmpty;

    /// <summary>Başlıktaki "3 öğe".</summary>
    [ObservableProperty]
    private string _countText = "";

    public ShelfViewModel(IShelfService shelfService, Dispatcher? dispatcher = null)
    {
        _shelfService = shelfService;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        UpdateItems();
        _shelfService.ItemsChanged += OnItemsChanged;
    }

    private void OnItemsChanged(object? sender, EventArgs e)
    {
        _dispatcher.InvokeAsync(UpdateItems);
    }

    private void UpdateItems()
    {
        Items.Clear();
        foreach (var item in _shelfService.Items)
        {
            Items.Add(item);
        }
        IsEmpty = Items.Count == 0;
        CountText = string.Format(System.Globalization.CultureInfo.CurrentCulture, Koy.App.Resources.Strings.Get("ShelfCount"), Items.Count);
    }

    [RelayCommand]
    private void RemoveItem(string id)
    {
        _shelfService.Remove(id);
    }

    [RelayCommand]
    private void ClearAll()
    {
        _shelfService.Clear();
    }

    [RelayCommand]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "RelayCommand")]
    private void OpenItem(ShelfItem item)
    {
        if (item.Kind is ShelfItemKind.File or ShelfItemKind.Directory)
        {
            try
            {
                Process.Start(new ProcessStartInfo(item.PathOrContent) { UseShellExecute = true });
            }
            catch
            {
            }
        }
    }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        foreach (var file in filePaths)
        {
            if (!string.IsNullOrWhiteSpace(file))
                _shelfService.AddFile(file);
        }
    }

    public void HandleDrop(string[]? filePaths, string? text)
    {
        if (filePaths is not null && filePaths.Length > 0)
        {
            AddFiles(filePaths);
        }
        else if (!string.IsNullOrWhiteSpace(text))
        {
            _shelfService.AddText(text);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shelfService.ItemsChanged -= OnItemsChanged;
    }
}
