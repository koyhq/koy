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

    // Basma ilerlemesi (0 bırakılmış, 1 basılı): hover ölçeğinden ayrı yay. Hafif sönümlü: bırakınca küçük bir geri sıçrama.
    protected readonly Spring PressSpring = new(0) { Stiffness = 520, Damping = 24 };
    private bool _pressMotion = true;

    /// <summary>Farenin sol tuşu bu öğenin üzerinde basılı (basma geri bildirimi).</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Basma çarpanı (hover büyütmesinden ayrı ölçek dönüşümüyle uygulanır). Hareket azaltmada hep 1.</summary>
    public double PressScale => DockPress.Scale(PressSpring.Value, _pressMotion);

    /// <summary>Simgenin basılıyken koyulaşması (opaklık).</summary>
    public double PressOpacity => DockPress.Opacity(PressSpring.Value);

    /// <summary>
    /// Basıldı / bırakıldı. <paramref name="animate"/> false ise (hareket azaltma) ölçek değişmez ve renk anında geçer.
    /// </summary>
    public void SetPressed(bool pressed, bool animate)
    {
        IsPressed = pressed;
        _pressMotion = animate;
        if (animate)
            PressSpring.Target = pressed ? 1.0 : 0.0;
        else
            PressSpring.SnapTo(pressed ? 1.0 : 0.0);

        NotifyPress();
    }

    /// <summary>Animasyonsuz anında basılı / bırakılmış duruma geçer (UiSnapshots sahnesi için).</summary>
    public void SnapPress(bool pressed)
    {
        IsPressed = pressed;
        _pressMotion = true;
        PressSpring.SnapTo(pressed ? 1.0 : 0.0);
        NotifyPress();
    }

    public bool StepPress(double dt)
    {
        if (PressSpring.IsAtRest)
            return false;

        var moving = PressSpring.Step(dt);
        NotifyPress();
        return moving;
    }

    private void NotifyPress()
    {
        OnPropertyChanged(nameof(PressScale));
        OnPropertyChanged(nameof(PressOpacity));
    }
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

    /// <summary>Gösterge ve rozet görünümü (ayardan türetilir, öğe oluşturulurken verilir).</summary>
    public DockPresentationStyle Presentation
    {
        get => _presentation;
        init
        {
            _presentation = value;
            RefreshPresentation();
        }
    }

    private DockPresentationStyle _presentation = DockPresentationStyle.Default;

    /// <summary>Simgenin altında çizilecek gösterge sayısı (Core: pencere başına nokta ya da tek gösterge).</summary>
    [ObservableProperty]
    private int _indicatorCount;

    /// <summary>Birden çok nokta çizilecek mi (pencere başına kip).</summary>
    public bool HasMultiDots => IndicatorCount > 1;

    /// <summary>Nokta şablonu için sıra listesi (uzunluğu <see cref="IndicatorCount"/>).</summary>
    public IReadOnlyList<int> IndicatorDots { get; private set; } = [];

    [ObservableProperty]
    private bool _showBadge;

    [ObservableProperty]
    private string _badgeText = "";

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
        RefreshPresentation();

        if (model.IconData is { Length: > 0 })
        {
            _icon = LoadImage(model.IconData);
        }
    }

    // Gösterge sayısı ve rozet kararı Core'dadır; burada yalnız sonuç özelliklere yazılır.
    private void RefreshPresentation()
    {
        var windowCount = IsRunning ? Math.Max(1, Windows.Count) : 0;
        var count = DockPresentationMath.IndicatorCount(windowCount, Presentation.IndicatorStyle, Presentation.OnePerWindow);
        if (count != IndicatorCount || IndicatorDots.Count != count)
        {
            IndicatorDots = Enumerable.Range(0, count).ToArray();
            IndicatorCount = count;
            OnPropertyChanged(nameof(IndicatorDots));
            OnPropertyChanged(nameof(HasMultiDots));
        }

        ShowBadge = Presentation.ShowBadges && DockPresentationMath.ShowsBadge(BadgeCount, Presentation.BadgeStyle);
        BadgeText = DockPresentationMath.BadgeText(BadgeCount, Presentation.BadgeStyle);
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

        RefreshPresentation();

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
