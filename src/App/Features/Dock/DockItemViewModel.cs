using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Animation;
using Koy.Core.Dock;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock üzerinde yer alan herhangi bir öğenin (tekil uygulama veya klasör) ortak temel görünüm modeli.
/// Yay fiziğiyle ölçeklenme (Scale), sıralama kayması (OffsetX) ve birleştirme parıltısını (IsTargetGlow) yönetir.
/// </summary>
public abstract partial class DockEntryViewModel : ObservableObject
{
    public abstract string Id { get; }
    public abstract string Title { get; set; }
    public abstract bool IsPinned { get; set; }
    public abstract bool IsRunning { get; set; }

    [ObservableProperty]
    private double _scale = 1.0;

    [ObservableProperty]
    private double _iconSize = 28.0;

    [ObservableProperty]
    private double _itemSize = 38.0;

    [ObservableProperty]
    private bool _isTargetGlow;

    protected readonly Spring ScaleSpring = new(1.0) { Stiffness = 320, Damping = 26 };
    protected readonly Spring OffsetSpring = new(0) { Stiffness = 380, Damping = 30 };

    public double OffsetX => OffsetSpring.Value;
    public void SetTargetOffset(double target) => OffsetSpring.Target = target;
    public void SnapOffset(double offset)
    {
        OffsetSpring.SnapTo(offset);
        OnPropertyChanged(nameof(OffsetX));
    }

    public bool StepOffset(double dt)
    {
        if (OffsetSpring.IsAtRest)
            return false;

        var moving = OffsetSpring.Step(dt);
        OnPropertyChanged(nameof(OffsetX));
        return moving;
    }

    public void SetTargetScale(double target) => ScaleSpring.Target = target;
    public void SnapScale(double scale)
    {
        ScaleSpring.SnapTo(scale);
        Scale = scale;
    }

    public bool StepScale(double dt)
    {
        var moving = ScaleSpring.Step(dt);
        Scale = ScaleSpring.Value;
        return moving;
    }
}

/// <summary>
/// Tek bir dock öğesinin (uygulama simgesi, çalışan durumu, büyütme ölçeği) görünüm modeli.
/// </summary>
public sealed partial class DockItemViewModel : DockEntryViewModel
{
    private readonly DockController _controller;

    public DockItem Model { get; private set; }

    public override string Id => Model.Id;

    private string _title = string.Empty;
    public override string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private bool _isPinned;
    public override bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    private bool _isRunning;
    public override bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isFlashing;

    [ObservableProperty]
    private bool _isAttention;

    [ObservableProperty]
    private int _badgeCount;

    [ObservableProperty]
    private bool _hasBadge;

    [ObservableProperty]
    private bool _isMultiWindow;

    [ObservableProperty]
    private IReadOnlyList<DockWindowEntry> _windows = [];

    [ObservableProperty]
    private ImageSource? _icon;

    /// <summary>Çalışan göstergesi stili "Çizgi" mi (ayar: 0 = nokta, 1 = çizgi). Öğe oluşturulurken verilir.</summary>
    public bool UseLineIndicator { get; init; }

    public bool HasOpenWindows => Windows.Count > 0;
    public DockWindowEntry? SingleWindow => Windows.Count == 1 ? Windows[0] : null;

    public DockItemViewModel(DockItem model, DockController controller)
    {
        Model = model;
        _controller = controller;

        _title = model.Title;
        _isPinned = model.IsPinned;
        _isRunning = model.IsRunning;
        _isActive = model.IsActive;
        _isFlashing = model.IsFlashing;
        _isAttention = model.IsFlashing;
        _badgeCount = model.BadgeCount;
        _hasBadge = model.BadgeCount > 0;
        _windows = model.Windows ?? [];
        _isMultiWindow = _windows.Count > 1;

        if (model.IconData is { Length: > 0 })
        {
            _icon = LoadImage(model.IconData);
        }
    }

    public void Update(DockItem model)
    {
        Model = model;
        Title = model.Title;
        IsPinned = model.IsPinned;
        IsRunning = model.IsRunning;
        IsActive = model.IsActive;
        IsFlashing = model.IsFlashing;
        IsAttention = model.IsFlashing;
        BadgeCount = model.BadgeCount;
        HasBadge = model.BadgeCount > 0;
        var newWindows = model.Windows ?? [];
        if (!Windows.SequenceEqual(newWindows))
        {
            Windows = newWindows;
            IsMultiWindow = Windows.Count > 1;
            OnPropertyChanged(nameof(HasOpenWindows));
            OnPropertyChanged(nameof(SingleWindow));
        }

        if (Icon is null && model.IconData is { Length: > 0 })
        {
            Icon = LoadImage(model.IconData);
        }
    }

    [RelayCommand]
    private void Click()
    {
        _controller.HandleItemClick(Model);
    }

    [RelayCommand]
    private void MiddleClick()
    {
        _controller.HandleItemMiddleClick(Model);
    }

    [RelayCommand]
    private void Close()
    {
        _controller.HandleItemClose(Model);
    }

    [RelayCommand]
    private void Terminate()
    {
        _controller.HandleItemTerminate(Model);
    }

    [RelayCommand]
    private void ActivateSpecificWindow(nint handle)
    {
        _controller.ActivateWindow(handle);
    }

    [RelayCommand]
    private void CloseSpecificWindow(nint handle)
    {
        _controller.CloseWindow(handle);
    }

    private static BitmapImage? LoadImage(byte[] data)
    {
        try
        {
            using var ms = new MemoryStream(data);
            var image = new BitmapImage();
            image.BeginInit();
            image.DecodePixelWidth = 48;
            image.StreamSource = ms;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }
}
