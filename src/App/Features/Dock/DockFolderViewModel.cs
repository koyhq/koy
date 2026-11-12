using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Dock;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock üzerinde sabitlenmiş uygulamaları gruplayan klasörün görünüm modeli.
/// İlk 4 uygulamanın simgesini 2×2 önizleme ızgarasında gösterir; içindeki herhangi bir uygulama
/// çalışıyorsa klasör kutusunun altındaki çalışma göstergesi yanar. Tıklanınca FolderPanel açılır.
/// </summary>
public sealed partial class DockFolderViewModel : DockEntryViewModel
{
    public DockFolder Folder { get; private set; }
    public DockViewModel? OwnerViewModel { get; set; }

    public override string Id => PinnedOrder.ToFolderToken(Folder.Id);

    private string _name = string.Empty;
    public override string Title
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public override bool IsPinned
    {
        get => true;
        set { }
    }

    public override bool IsRunning
    {
        get => Items.Any(i => i.IsRunning);
        set { }
    }

    [ObservableProperty]
    private bool _isFolderPanelOpen;

    [ObservableProperty]
    private bool _isEditingTitle;

    public ObservableCollection<DockItemViewModel> Items { get; } = [];

    public string RenameTooltip => Resources.Strings.Get("DockFolderRename");
    public string DissolveTooltip => Resources.Strings.Get("DockFolderDissolve");

    /// <summary>
    /// Dock üzerindeki 2×2 mini ızgara için ilk 4 uygulamanın simgeleri.
    /// </summary>
    public IReadOnlyList<ImageSource?> PreviewIcons =>
        Items.Take(4).Select(i => i.Icon).ToList();

    public DockFolderViewModel(
        DockFolder folder,
        IEnumerable<DockItemViewModel> items,
        DockViewModel? ownerViewModel = null)
    {
        Folder = folder;
        _name = folder.Name;
        OwnerViewModel = ownerViewModel;

        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    public void UpdateFolder(DockFolder folder, IEnumerable<DockItemViewModel> items)
    {
        Folder = folder;
        Title = folder.Name;

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PreviewIcons));
        OnPropertyChanged(nameof(IsRunning));
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(PreviewIcons));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(Title));
    }

    public void CloseFolderPanel()
    {
        IsFolderPanelOpen = false;
        IsEditingTitle = false;
    }

    public void StartRename()
    {
        IsFolderPanelOpen = true;
        IsEditingTitle = true;
    }

    [RelayCommand]
    private void Click()
    {
        IsFolderPanelOpen = !IsFolderPanelOpen;
        if (!IsFolderPanelOpen)
        {
            IsEditingTitle = false;
        }
    }

    [RelayCommand]
    private void Rename()
    {
        StartRename();
    }

    [RelayCommand]
    private void Dissolve()
    {
        CloseFolderPanel();
        OwnerViewModel?.DissolveFolder(this);
    }
}
