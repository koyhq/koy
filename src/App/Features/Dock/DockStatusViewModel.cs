using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Audio;
using Koy.Core.Dock;
using Koy.Core.Network;
using Koy.Core.Power;
using Koy.Core.Shell;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock'un sağındaki durum alanı: ağ, ses, pil, saat. Windows görev çubuğu gizlendiği için onun yerini tutar.
/// Tıklamalar Windows'un kendi Hızlı Ayarlar ve Bildirim Merkezi panellerini açar.
/// </summary>
public sealed partial class DockStatusViewModel : ObservableObject, IDisposable
{
    private readonly INetworkService _network;
    private readonly IAudioService _audio;
    private readonly IPowerService _power;
    private readonly IShellActionService _shell;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _clockTimer;
    private bool _active;
    private bool _disposed;

    [ObservableProperty]
    private string _networkGlyph = TrayGlyphs.Offline;

    [ObservableProperty]
    private string _wiFiLevelGlyph = string.Empty;

    [ObservableProperty]
    private string _networkTooltip = string.Empty;

    [ObservableProperty]
    private string _volumeGlyph = string.Empty;

    [ObservableProperty]
    private string _volumeTooltip = string.Empty;

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private string _batteryGlyph = string.Empty;

    [ObservableProperty]
    private string _batteryTooltip = string.Empty;

    [ObservableProperty]
    private string _timeText = string.Empty;

    [ObservableProperty]
    private string _dateText = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "XAML binding")]
    public string NotificationsGlyph => TrayGlyphs.Notifications;

    public DockStatusViewModel(
        INetworkService network,
        IAudioService audio,
        IPowerService power,
        IShellActionService shell,
        Dispatcher? dispatcher = null)
    {
        _network = network;
        _audio = audio;
        _power = power;
        _shell = shell;
        _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _clockTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        _clockTimer.Tick += OnClockTick;

        _network.Changed += OnNetworkChanged;
        _audio.StateChanged += OnAudioChanged;
        _power.PowerChanged += OnPowerChanged;

        ApplyNetwork(_network.Current);
        ApplyAudio(_audio.Volume, _audio.IsMuted);
        ApplyPower(_power.HasBattery, _power.RemainingPercent, _power.IsCharging);
        SetActive(true);
    }

    /// <summary>Dock görünmüyorken (tam ekran) saat zamanlayıcısı durur (AGENTS.md #5).</summary>
    public void SetActive(bool active)
    {
        if (_active == active || _disposed)
            return;

        _active = active;
        if (active)
        {
            UpdateClock();
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

    private void OnClockTick(object? sender, EventArgs e)
    {
        UpdateClock();
        ScheduleNextTick();
    }

    private void ScheduleNextTick()
    {
        // Saniyede bir değil, bir sonraki dakika başında uyan.
        var now = DateTime.Now;
        _clockTimer.Interval = TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(50);
        _clockTimer.Start();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        TimeText = now.ToString("t", CultureInfo.CurrentCulture);
        DateText = now.ToString("d", CultureInfo.CurrentCulture);
    }

    private void OnNetworkChanged(object? sender, NetworkStatus status) =>
        _dispatcher.InvokeAsync(() => ApplyNetwork(status));

    private void OnAudioChanged(object? sender, AudioStateChangedEventArgs e) =>
        _dispatcher.InvokeAsync(() => ApplyAudio(e.Volume, e.IsMuted));

    private void OnPowerChanged(object? sender, PowerChangedEventArgs e) =>
        _dispatcher.InvokeAsync(() => ApplyPower(e.HasBattery, e.RemainingPercent, e.IsCharging));

    private void ApplyNetwork(NetworkStatus status)
    {
        NetworkGlyph = TrayGlyphs.Network(status);
        WiFiLevelGlyph = TrayGlyphs.WiFiLevel(status);
        NetworkTooltip = status switch
        {
            { Kind: NetworkKind.None } => Strings.NetworkDisconnected,
            { HasInternet: false } => Strings.NetworkNoInternet,
            { Kind: NetworkKind.WiFi } => Strings.NetworkWiFi,
            { Kind: NetworkKind.Cellular } => Strings.NetworkCellular,
            _ => Strings.NetworkEthernet
        };
    }

    private void ApplyAudio(float volume, bool muted)
    {
        VolumeGlyph = TrayGlyphs.Volume(volume, muted);
        VolumeTooltip = muted
            ? Strings.VolumeMuted
            : string.Format(CultureInfo.CurrentCulture, Strings.VolumeLevel, (int)Math.Round(volume * 100));
    }

    private void ApplyPower(bool hasBattery, int percent, bool charging)
    {
        HasBattery = hasBattery;
        BatteryGlyph = TrayGlyphs.Battery(percent, charging);
        BatteryTooltip = string.Format(CultureInfo.CurrentCulture, charging ? Strings.BatteryCharging : Strings.BatteryLevel, percent);
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
    }
}
