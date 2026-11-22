using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Status;
using Koy.App.Resources;
using Koy.Core.Audio;
using Koy.Core.Dock;
using Koy.Core.Dock.SystemArea;
using Koy.Core.Network;
using Koy.Core.Power;
using Koy.Core.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// Tam genişlik dock'un sağ ucundaki sistem alanının verisi (Windows 11 görev çubuğundaki Wi-Fi + ses + pil grubu, saat,
/// bildirim zili). Tıklamalar Windows'un kendi Hızlı Ayarlar ve Bildirim Merkezi panellerini açar. Veri olay tabanlıdır
/// (ağ, ses, güç, odak yardımı); yoklama yok. Saat tek zamanlayıcıyla dakika başında uyanır ve yalnız dock görünürken çalışır.
/// Servisler yalnız tam genişlik dock varken alınır: sahibi (DockFeature) <paramref name="release"/> ile geri alır.
/// </summary>
public sealed partial class DockStatusViewModel : ObservableObject, IDisposable
{
    private readonly INetworkService _network;
    private readonly IAudioService _audio;
    private readonly IPowerService _power;
    private readonly IFocusAssistService? _focus;
    private readonly IShellActionService _shell;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _clockTimer;
    private readonly Action? _release;
    private bool _active;
    private bool _disposed;

    [ObservableProperty]
    private string _networkGlyph = TrayGlyphs.Offline;

    /// <summary>Kablosuz sinyal çubukları: tam simgenin üstüne dolu kısmı çizen ikinci glif (boşsa tam simge yeter).</summary>
    [ObservableProperty]
    private string _wiFiLevelGlyph = string.Empty;

    /// <summary>Tam kablosuz simgenin saydamlığı: çubuklar üstüne bindiğinde soluk (0,4), aksi halde tam.</summary>
    [ObservableProperty]
    private double _networkBaseOpacity = 1.0;

    [ObservableProperty]
    private string _networkTooltip = string.Empty;

    [ObservableProperty]
    private string _volumeGlyph = string.Empty;

    [ObservableProperty]
    private string _volumeTooltip = string.Empty;

    [ObservableProperty]
    private bool _hasBattery;

    /// <summary>Pil gösterim kararı (simge, dolum, renk); ipucu <see cref="BatteryTooltip"/>.</summary>
    [ObservableProperty]
    private BatteryDisplay _battery = BatteryDisplay.None;

    [ObservableProperty]
    private string _batteryTooltip = string.Empty;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    [ObservableProperty]
    private string _clockTooltip = string.Empty;

    /// <summary>Rahatsız etmeyin (odaklanma oturumu, sunum modu) açık: zil üstü çizili.</summary>
    [ObservableProperty]
    private bool _isDoNotDisturb;

    [ObservableProperty]
    private string _bellGlyph = TrayGlyphs.Notifications;

    [ObservableProperty]
    private string _bellTooltip = string.Empty;

    public DockStatusViewModel(
        INetworkService network,
        IAudioService audio,
        IPowerService power,
        IShellActionService shell,
        IFocusAssistService? focus = null,
        Action? release = null,
        Dispatcher? dispatcher = null)
    {
        _network = network;
        _audio = audio;
        _power = power;
        _focus = focus;
        _shell = shell;
        _release = release;
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        _clockTimer.Tick += OnClockTick;

        _network.Changed += OnNetworkChanged;
        _audio.StateChanged += OnAudioChanged;
        _power.PowerChanged += OnPowerChanged;
        if (_focus is not null)
            _focus.StateChanged += OnFocusChanged;

        ApplyNetwork(_network.Current);
        ApplyAudio(_audio.Volume, _audio.IsMuted);
        ApplyPower(_power.Display());
        ApplyFocus(_focus?.IsDoNotDisturb == true);
        UpdateClock();
    }

    /// <summary>Dock görünmüyorken (tam ekran) saat zamanlayıcısı durur (AGENTS.md #5).</summary>
    public void SetActive(bool active)
    {
        if (_active == active || _disposed)
            return;

        _active = active;
        if (active)
        {
            // Gizliyken geçen süre için saat tazelenir; ilk uyanma bir sonraki dakika başında.
            UpdateClock();
            ApplyFocus(_focus?.IsDoNotDisturb == true);
            ScheduleNextTick();
        }
        else
        {
            _clockTimer.Stop();
        }
    }

    /// <summary>Ses simgesi üstünde tekerlek: Windows görev çubuğundaki gibi %2 adım.</summary>
    public void AdjustVolume(int wheelDelta)
    {
        var step = wheelDelta > 0 ? 0.02f : -0.02f;
        _audio.SetVolume(Math.Clamp(_audio.Volume + step, 0f, 1f));
    }

    [RelayCommand]
    private void OpenQuickSettings() => _shell.OpenQuickSettings();

    [RelayCommand]
    private void OpenNotificationCenter() => _shell.OpenNotificationCenter();

    /// <summary>En sağdaki ince şerit: pencereleri küçültür ya da geri getirir (Win+D).</summary>
    [RelayCommand]
    private void ShowDesktop() => _shell.ShowDesktop();

    private void OnClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
        ScheduleNextTick();
    }

    private void ScheduleNextTick()
    {
        // Saniyede bir değil, bir sonraki dakika başında uyan.
        _clockTimer.Interval = Koy.Core.Dock.Items.DockClockTick.DelayToNextMinute(DateTime.Now);
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        var culture = CultureInfo.CurrentCulture;
        TimeText = SystemClockText.Time(now, culture);
        DateText = SystemClockText.Date(now, culture);
        ClockTooltip = SystemClockText.Tooltip(now, culture);
    }

    private void OnNetworkChanged(object? sender, NetworkStatus status) =>
        _dispatcher.InvokeAsync(() => ApplyNetwork(status));

    private void OnAudioChanged(object? sender, AudioStateChangedEventArgs e) =>
        _dispatcher.InvokeAsync(() => ApplyAudio(e.Volume, e.IsMuted));

    private void OnPowerChanged(object? sender, PowerChangedEventArgs e) =>
        _dispatcher.InvokeAsync(() => ApplyPower(e.Display()));

    private void OnFocusChanged(object? sender, bool active) =>
        _dispatcher.InvokeAsync(() => ApplyFocus(active));

    private void ApplyNetwork(NetworkStatus status)
    {
        NetworkGlyph = TrayGlyphs.Network(status);
        WiFiLevelGlyph = TrayGlyphs.WiFiLevel(status);
        NetworkBaseOpacity = WiFiLevelGlyph.Length > 0 ? 0.4 : 1.0;
        var text = status switch
        {
            { Kind: NetworkKind.None } => Strings.NetworkDisconnected,
            { HasInternet: false } => Strings.NetworkNoInternet,
            { Kind: NetworkKind.WiFi } => Strings.NetworkWiFi,
            { Kind: NetworkKind.Cellular } => Strings.NetworkCellular,
            _ => Strings.NetworkEthernet
        };
        // Windows ipucu gibi: ağ adı bilinirse başa gelir.
        var name = NetworkSummary.WiFiName(status);
        NetworkTooltip = name is null ? text : name + ": " + text;
    }

    private void ApplyAudio(float volume, bool muted)
    {
        VolumeGlyph = TrayGlyphs.Volume(volume, muted);
        VolumeTooltip = muted
            ? Strings.VolumeMuted
            : string.Format(CultureInfo.CurrentCulture, Strings.VolumeLevel, (int)Math.Round(volume * 100));
    }

    private void ApplyPower(BatteryDisplay display)
    {
        HasBattery = display.HasBattery;
        Battery = display;
        BatteryTooltip = BatteryText.Tooltip(display);
    }

    private void ApplyFocus(bool doNotDisturb)
    {
        IsDoNotDisturb = doNotDisturb;
        BellGlyph = doNotDisturb ? TrayGlyphs.NotificationsMuted : TrayGlyphs.Notifications;
        BellTooltip = Strings.Get(doNotDisturb ? "DockBellTooltipDnd" : "DockBellTooltip");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _clockTimer.Stop();
        _clockTimer.Tick -= OnClockTick;
        _network.Changed -= OnNetworkChanged;
        _audio.StateChanged -= OnAudioChanged;
        _power.PowerChanged -= OnPowerChanged;
        if (_focus is not null)
            _focus.StateChanged -= OnFocusChanged;
        _release?.Invoke();
    }
}
