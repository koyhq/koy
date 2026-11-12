using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Bluetooth;
using Koy.Core.Diagnostics;
using Koy.Core.Radio;

namespace Koy.App.Features.ControlCenter;

/// <summary>Bluetooth listesindeki tek cihaz satırı.</summary>
public sealed partial class BluetoothDeviceRowViewModel : ObservableObject
{
    public string Id { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private BluetoothDeviceKind _kind;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isAudio;

    [ObservableProperty]
    private string? _batteryText;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>Ses dışı cihazlarda satırın altındaki küçük yazı (tıklayınca Windows ayarları açılır).</summary>
    [ObservableProperty]
    private string? _hintText;

    [ObservableProperty]
    private string? _errorText;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _actionText = string.Empty;

    public bool HasBattery => BatteryText is not null;

    /// <summary>Satır Windows Bluetooth ayarlarını açar (ses dışı cihaz); ses cihazlarında işlem sağdaki düğmeyle yapılır.</summary>
    public bool OpensSettings => !IsAudio;

    public BluetoothDeviceInfo Info { get; private set; }

    public BluetoothDeviceRowViewModel(BluetoothDeviceInfo info)
    {
        Id = info.Id;
        Info = info;
        Update(info);
    }

    partial void OnBatteryTextChanged(string? value) => OnPropertyChanged(nameof(HasBattery));

    partial void OnIsAudioChanged(bool value) => OnPropertyChanged(nameof(OpensSettings));

    partial void OnIsBusyChanged(bool value) => Refresh();

    partial void OnErrorTextChanged(string? value) => Refresh();

    public void Update(BluetoothDeviceInfo info)
    {
        Info = info;
        Name = info.Name;
        Kind = info.Kind;
        IsConnected = info.IsConnected;
        IsAudio = info.IsAudio;
        BatteryText = info.IsConnected ? BluetoothList.BatteryText(info.BatteryPercent, CultureInfo.CurrentCulture) : null;
        Refresh();
    }

    // Durum satırı: işlem sürerken "Bağlanıyor…", hata varsa hata, yoksa gerçek bağlantı durumu.
    private void Refresh()
    {
        var action = BluetoothList.ActionFor(Info);
        ActionText = action switch
        {
            BluetoothRowAction.Connect => Strings.Get("BluetoothConnect"),
            BluetoothRowAction.Disconnect => Strings.Get("BluetoothDisconnect"),
            _ => string.Empty
        };
        HintText = action == BluetoothRowAction.OpenSettings ? Strings.Get("BluetoothOpenSettingsHint") : null;
        StatusText = IsBusy
            ? Strings.Get(Info.IsConnected ? "BluetoothDisconnecting" : "BluetoothConnecting")
            : ErrorText ?? Strings.Get(BluetoothList.StatusKey(Info));
    }
}

/// <summary>
/// Eşleşmiş Bluetooth cihazları: Ana'daki kulaklık kartı, Kontrol'deki "Bağlı cihazlar" ve Bluetooth listesi aynı
/// veriyi kullanır. Servis yalnız bir sayfa "izliyorken" alınır (<see cref="SetViewing"/>); izleyen kalmayınca
/// DeviceWatcher durur ve listeler boşaltılır (boşta %0 CPU, bellek serbest).
/// </summary>
public sealed partial class BluetoothDevicesViewModel : ObservableObject, IDisposable
{
    private readonly Func<IBluetoothService> _acquire;
    private readonly Action? _release;
    private readonly Dispatcher _dispatcher;
    private readonly HashSet<string> _viewers = [];
    private readonly Dictionary<string, BluetoothDeviceRowViewModel> _rows = new(StringComparer.Ordinal);
    private IBluetoothService? _service;
    private bool _syncQueued;
    private bool _disposed;

    /// <summary>Bluetooth listesi sayfası: bütün eşleşmiş cihazlar (bağlı olanlar üstte).</summary>
    public ObservableCollection<BluetoothDeviceRowViewModel> Devices { get; } = [];

    /// <summary>Kontrol'deki "Bağlı cihazlar" kartı: yalnız bağlı cihazlar.</summary>
    public ObservableCollection<BluetoothDeviceRowViewModel> Connected { get; } = [];

    /// <summary>Ana'daki kulaklık kartı: bağlı ses cihazı; yoksa null (kart görünmez).</summary>
    [ObservableProperty]
    private BluetoothDeviceRowViewModel? _primaryAudio;

    [ObservableProperty]
    private bool _hasPrimaryAudio;

    [ObservableProperty]
    private bool _hasConnected;

    [ObservableProperty]
    private bool _isReady;

    public BluetoothDevicesViewModel(Func<IBluetoothService> acquire, Action? release, Dispatcher? dispatcher = null)
    {
        _acquire = acquire;
        _release = release;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    /// <summary>Bu sahip (ör. "home", "control") şu an cihaz verisini gösteriyor mu. Hiç sahip kalmayınca servis bırakılır.</summary>
    public void SetViewing(string owner, bool viewing)
    {
        if (_disposed)
            return;

        var changed = viewing ? _viewers.Add(owner) : _viewers.Remove(owner);
        if (!changed)
            return;

        if (_viewers.Count > 0)
            Start();
        else
            Stop();
    }

    private void Start()
    {
        if (_service is not null)
            return;

        _service = _acquire();
        _service.DevicesChanged += OnDevicesChanged;
        _service.SetBatteryReadsEnabled(true);
        Sync();
    }

    private void Stop()
    {
        if (_service is null)
            return;

        _service.SetBatteryReadsEnabled(false);
        _service.DevicesChanged -= OnDevicesChanged;
        if (_release is not null)
            _release();
        else
            _service.Dispose();
        _service = null;

        _rows.Clear();
        Devices.Clear();
        Connected.Clear();
        PrimaryAudio = null;
        HasPrimaryAudio = false;
        HasConnected = false;
        IsReady = false;
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // WinRT iş parçacığından gelir; art arda olaylar tek eşitlemeye indirilir.
        if (_disposed || _syncQueued)
            return;

        _syncQueued = true;
        _dispatcher.InvokeAsync(() =>
        {
            _syncQueued = false;
            Sync();
        });
    }

    private void Sync()
    {
        if (_service is null || _disposed)
            return;

        IsReady = _service.IsReady;
        var devices = BluetoothList.Normalize(_service.Devices);

        var next = new List<BluetoothDeviceRowViewModel>(devices.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var info in devices)
        {
            seen.Add(info.Id);
            if (_rows.TryGetValue(info.Id, out var row))
                row.Update(info);
            else
                _rows[info.Id] = row = new BluetoothDeviceRowViewModel(info);
            next.Add(row);
        }

        foreach (var stale in _rows.Keys.Where(id => !seen.Contains(id)).ToList())
            _rows.Remove(stale);

        Reconcile(Devices, next);
        Reconcile(Connected, next.Where(r => r.IsConnected).ToList());

        var primary = BluetoothList.PrimaryAudio(devices);
        PrimaryAudio = primary is not null && _rows.TryGetValue(primary.Id, out var primaryRow) ? primaryRow : null;
        HasPrimaryAudio = PrimaryAudio is not null;
        HasConnected = Connected.Count > 0;
    }

    // Koleksiyonu yerinde günceller: satır nesneleri korunur (busy / hata durumu kaybolmaz), liste titremez.
    private static void Reconcile(ObservableCollection<BluetoothDeviceRowViewModel> target, List<BluetoothDeviceRowViewModel> desired)
    {
        for (var i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
                target.RemoveAt(i);
        }

        for (var i = 0; i < desired.Count; i++)
        {
            var row = desired[i];
            var current = target.IndexOf(row);
            if (current < 0)
                target.Insert(i, row);
            else if (current != i)
                target.Move(current, i);
        }
    }

    /// <summary>Satıra ya da düğmesine basılınca: ses cihazı bağlanır / bağlantısı kesilir, diğerleri için Windows ayarları açılır.</summary>
    [RelayCommand]
    private async Task ActivateAsync(BluetoothDeviceRowViewModel? row)
    {
        if (row is null || _service is null || row.IsBusy)
            return;

        var action = BluetoothList.ActionFor(row.Info);
        if (action == BluetoothRowAction.OpenSettings)
        {
            _service.OpenSettings();
            return;
        }

        var connecting = action == BluetoothRowAction.Connect;
        var service = _service;
        row.ErrorText = null;
        row.IsBusy = true;
        try
        {
            var result = await (connecting ? service.ConnectAsync(row.Id) : service.DisconnectAsync(row.Id));
            var key = BluetoothList.ErrorKey(result, connecting);
            row.ErrorText = key is null ? null : Strings.Get(key);
        }
        catch (Exception ex)
        {
            CrashLog.Write("Bluetooth bağlan/kes", ex);
            row.ErrorText = Strings.Get(connecting ? "BluetoothErrConnect" : "BluetoothErrDisconnect");
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    /// <summary>Listenin hangi hâlde gösterileceği (radyo durumu + servis).</summary>
    public BluetoothListState StateFor(RadioTileStatus radio) => BluetoothList.Resolve(radio, IsReady, Devices.Count);

    public void OpenSettings()
    {
        if (_service is not null)
        {
            _service.OpenSettings();
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch
        {
            // Ayarlar uygulaması kaldırılmış olabilir.
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _disposed = true;
        _viewers.Clear();
    }
}
