using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Display;
using Koy.Core.Settings;
using Koy.Platform.Display;

namespace Koy.App.Features.Settings;

/// <summary>Ekranlar sayfasındaki şemada tek bir monitör kartı.</summary>
public sealed partial class MonitorCardViewModel : ObservableObject
{
    public required DisplayMonitor Monitor { get; init; }
    public required int Number { get; init; }
    public required double Left { get; init; }
    public required double Top { get; init; }
    public required double Width { get; init; }
    public required double Height { get; init; }

    public string Id => Monitor.Id;
    public bool IsPrimary => Monitor.IsPrimary;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Bu ekranda çentik / dock açık mı (kartta mini çentik ve dock çizilir).</summary>
    [ObservableProperty]
    private bool _showsNotch;

    [ObservableProperty]
    private bool _showsDock;
}

/// <summary>
/// Ekranlar sayfası: gerçek monitör yerleşimini çizer, seçili ekranın çentik/dock ayarını ve "yeni ekran" varsayılanını
/// ayar dosyasına yazar, ekran takılıp çıkınca kendini günceller. Yalnız sayfa görünürken (Activate/Deactivate) sistem
/// olaylarını dinler.
/// </summary>
public sealed partial class DisplaysViewModel(SettingsStore store) : ObservableObject, IDisposable
{
    // Şema alanı: içerik sütunu ~640 DIP, kutu yüksekliği tasarımdaki gibi 132 DIP'i geçmez.
    private const double SchemaMaxWidth = 560;
    private const double SchemaMaxHeight = 132;

    private MonitorService? _monitorService;
    private DisplayEvents? _events;
    private bool _loading;
    private IReadOnlyList<DisplayMonitor> _monitors = [];
    private string? _selectedId;
    private NewScreenMode _newScreenMode = NewScreenMode.NotchAndDock;

    public ObservableCollection<MonitorCardViewModel> Cards { get; } = [];

    [ObservableProperty]
    private double _schemaWidth;

    [ObservableProperty]
    private double _schemaHeight;

    [ObservableProperty]
    private MonitorCardViewModel? _selectedCard;

    [ObservableProperty]
    private string _selectedHeader = "";

    [ObservableProperty]
    private string _selectedName = "";

    [ObservableProperty]
    private string _selectedDetails = "";

    [ObservableProperty]
    private bool _selectedIsPrimary;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _notchOn = true;

    [ObservableProperty]
    private bool _dockOn = true;

    public bool NewScreenBoth
    {
        get => _newScreenMode == NewScreenMode.NotchAndDock;
        set => SetNewScreenMode(value, NewScreenMode.NotchAndDock);
    }

    public bool NewScreenNotchOnly
    {
        get => _newScreenMode == NewScreenMode.NotchOnly;
        set => SetNewScreenMode(value, NewScreenMode.NotchOnly);
    }

    public bool NewScreenNone
    {
        get => _newScreenMode == NewScreenMode.None;
        set => SetNewScreenMode(value, NewScreenMode.None);
    }

    /// <summary>Sayfa görünür oldu: ekranları oku ve takma/çıkarmayı dinlemeye başla.</summary>
    public void Activate()
    {
        if (_monitorService is null)
        {
            _monitorService = new MonitorService();
            _monitorService.Start();
            _monitorService.MonitorsChanged += OnMonitorsChanged;
            _events = new DisplayEvents(_monitorService);
        }

        Refresh();
    }

    /// <summary>Sayfa kapandı: dinlemeyi bırak.</summary>
    public void Deactivate()
    {
        if (_monitorService is null)
            return;

        _monitorService.MonitorsChanged -= OnMonitorsChanged;
        _events?.Dispose();
        _events = null;
        _monitorService.Dispose();
        _monitorService = null;
    }

    public void Dispose() => Deactivate();

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        // Sistem olayı UI dışı thread'den gelir.
        dispatcher.InvokeAsync(Refresh);
    }

    /// <summary>Bağlı ekranları ve kayıtlı ayarları yeniden okuyup şemayı kurar.</summary>
    public void Refresh()
    {
        try
        {
            if (_monitorService is not null)
                _monitors = _monitorService.GetMonitors();
            else
            {
                using var service = new MonitorService();
                _monitors = service.GetMonitors();
            }
        }
        catch
        {
            _monitors = [];
        }

        var settings = store.Load();
        _newScreenMode = settings.Displays.NewScreenDefault;
        OnPropertyChanged(nameof(NewScreenBoth));
        OnPropertyChanged(nameof(NewScreenNotchOnly));
        OnPropertyChanged(nameof(NewScreenNone));

        var layout = DisplaySchema.Compute(_monitors, SchemaMaxWidth, SchemaMaxHeight);
        var byId = _monitors.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);

        var assignments = MonitorPolicy.Resolve(settings.Displays.Monitors, settings.Displays.NewScreenDefault, _monitors);

        Cards.Clear();
        foreach (var tile in layout.Tiles)
        {
            var assignment = assignments.First(a => string.Equals(a.Monitor.Id, tile.Id, StringComparison.OrdinalIgnoreCase));
            Cards.Add(new MonitorCardViewModel
            {
                ShowsNotch = assignment.NotchEnabled,
                ShowsDock = assignment.DockEnabled,
                Monitor = byId[tile.Id],
                Number = tile.Number,
                Left = tile.Left,
                Top = tile.Top,
                Width = tile.Width,
                Height = tile.Height
            });
        }

        SchemaWidth = layout.Width;
        SchemaHeight = layout.Height;

        // Seçim ekran değişse de korunur; seçili ekran çıktıysa ana ekrana dönülür.
        var card = Cards.FirstOrDefault(c => string.Equals(c.Id, _selectedId, StringComparison.OrdinalIgnoreCase))
                   ?? Cards.FirstOrDefault(c => c.IsPrimary)
                   ?? Cards.FirstOrDefault();
        Select(card);
    }

    /// <summary>"Ekranları tanımla": her monitörde 2 saniye ekran numarası.</summary>
    [RelayCommand]
    private void Identify()
    {
        // Şema yenilenmemiş olabilir (ekran yeni takıldı); güncel liste okunur.
        using var service = new MonitorService();
        IdentifyOverlayWindow.ShowOn(service.GetMonitors());
    }

    [RelayCommand]
    private void SelectCard(MonitorCardViewModel? card) => Select(card);

    private void Select(MonitorCardViewModel? card)
    {
        foreach (var c in Cards)
            c.IsSelected = ReferenceEquals(c, card);

        SelectedCard = card;
        _selectedId = card?.Id;
        HasSelection = card is not null;

        _loading = true;
        try
        {
            if (card is null)
            {
                SelectedHeader = SelectedName = SelectedDetails = "";
                SelectedIsPrimary = false;
                return;
            }

            var settings = store.Load();
            var assignment = MonitorPolicy.Resolve(settings.Displays.Monitors, settings.Displays.NewScreenDefault, [card.Monitor])[0];
            NotchOn = assignment.NotchEnabled;
            DockOn = assignment.DockEnabled;

            var m = card.Monitor;
            SelectedName = m.Name;
            SelectedIsPrimary = m.IsPrimary;
            SelectedHeader = string.Format(CultureInfo.CurrentCulture, Strings.Get("DisplayHeaderFormat"), card.Number, m.Name.ToUpper(CultureInfo.CurrentCulture));
            SelectedDetails = string.Format(
                CultureInfo.CurrentCulture, Strings.Get("DisplayDetailsFormat"),
                m.PixelWidth, m.PixelHeight, (int)Math.Round(m.DpiScaleX * 100));
        }
        finally
        {
            _loading = false;
        }
    }

    partial void OnNotchOnChanged(bool value)
    {
        if (!_loading && SelectedCard is { } card)
        {
            Persist(card.Monitor, s => s with { NotchEnabled = value });
            card.ShowsNotch = value;
            (Application.Current as App)?.ReloadNotch();
        }
    }

    partial void OnDockOnChanged(bool value)
    {
        if (!_loading && SelectedCard is { } card)
        {
            Persist(card.Monitor, s => s with { DockEnabled = value });
            card.ShowsDock = value;
            (Application.Current as App)?.ReloadDock();
        }
    }

    private void Persist(DisplayMonitor monitor, Func<MonitorSettings, MonitorSettings> change)
    {
        var current = store.Load();
        store.Save(current with
        {
            Displays = current.Displays with
            {
                Monitors = MonitorPolicy.Update(current.Displays.Monitors, current.Displays.NewScreenDefault, monitor, change)
            }
        });
    }

    private void SetNewScreenMode(bool selected, NewScreenMode mode)
    {
        // RadioButton, seçilen seçenek için true bildirir; bırakılan için gelen false yok sayılır.
        if (!selected || _newScreenMode == mode)
            return;

        _newScreenMode = mode;
        OnPropertyChanged(nameof(NewScreenBoth));
        OnPropertyChanged(nameof(NewScreenNotchOnly));
        OnPropertyChanged(nameof(NewScreenNone));

        if (_loading)
            return;

        var current = store.Load();
        store.Save(current with { Displays = current.Displays with { NewScreenDefault = mode } });

        // Çalışan özellikler ayarı kurulurken okur; kayıtsız ekran varsa yeni varsayılan hemen uygulansın.
        if (Application.Current is App app)
        {
            app.ReloadNotch();
            app.ReloadDock();
        }
    }
}
