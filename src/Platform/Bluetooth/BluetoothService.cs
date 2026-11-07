using System.Runtime.InteropServices;
using Koy.Core.Bluetooth;
using Koy.Core.Diagnostics;
using Koy.Platform.Wireless;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Koy.Platform.Bluetooth;

/// <summary>
/// <see cref="IBluetoothService"/> uygulaması. Eşleşmiş cihazlar iki <c>DeviceWatcher</c> ile izlenir (klasik ve LE);
/// bağlantı ve pil değişiklikleri olayla gelir, yoklama yok. Servis yaşadığı sürece izleyiciler çalışır, Dispose ikisini de durdurur.
/// Bağlan / kes: yalnız ses cihazlarında <see cref="BluetoothAudioKs"/> ile; sonuç bağlantı durumundan okunur.
/// </summary>
public sealed class BluetoothService : IBluetoothService
{
    private const string PropIsConnected = "System.Devices.Aep.IsConnected";
    private const string PropContainerId = "System.Devices.Aep.ContainerId";
    // DEVPKEY_Bluetooth_Battery
    private const string PropBattery = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    private static readonly string[] RequestedProperties = [PropIsConnected, PropContainerId, PropBattery];
    private static readonly TimeSpan ConfirmTimeout = TimeSpan.FromSeconds(8);

    private sealed class Entry(string id, bool isLowEnergy)
    {
        public string Id { get; } = id;
        public bool IsLowEnergy { get; } = isLowEnergy;
        public string Name { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public int? Battery { get; set; }
        public string? ContainerId { get; set; }
        public BluetoothDeviceKind Kind { get; set; } = BluetoothDeviceKind.Other;
        public bool KindRequested { get; set; }
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _kindGate = new(1, 1);
    private DeviceWatcher? _classicWatcher;
    private DeviceWatcher? _leWatcher;
    private int _pendingEnumerations;
    private TaskCompletionSource _changed = NewSignal();
    private bool _disposed;

    public bool IsReady { get; private set; }

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<BluetoothDeviceInfo> Devices
    {
        get
        {
            lock (_gate)
                return _entries.Values.Select(ToInfo).ToList();
        }
    }

    public BluetoothService()
    {
        try
        {
            _classicWatcher = CreateWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true), isLowEnergy: false);
            _leWatcher = CreateWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), isLowEnergy: true);
            _pendingEnumerations = 2;
            _classicWatcher.Start();
            _leWatcher.Start();
        }
        catch (Exception ex)
        {
            // Bluetooth yığını yoksa liste boş ve hazır kalır.
            CrashLog.Write("BluetoothService başlatma", ex);
            IsReady = true;
        }
    }

    private DeviceWatcher CreateWatcher(string selector, bool isLowEnergy)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, RequestedProperties, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += (_, info) => OnAdded(info, isLowEnergy);
        watcher.Updated += (_, update) => OnUpdated(update);
        watcher.Removed += (_, update) => OnRemoved(update);
        watcher.EnumerationCompleted += (_, _) => OnEnumerationCompleted();
        return watcher;
    }

    private void OnAdded(DeviceInformation info, bool isLowEnergy)
    {
        Entry entry;
        lock (_gate)
        {
            if (_disposed)
                return;

            entry = new Entry(info.Id, isLowEnergy) { Name = info.Name ?? string.Empty };
            ApplyProperties(entry, info.Properties);
            _entries[info.Id] = entry;
        }

        QueueKindLookup(entry);
        Notify();
    }

    private void OnUpdated(DeviceInformationUpdate update)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(update.Id, out var entry))
                return;

            ApplyProperties(entry, update.Properties);
        }

        Notify();
    }

    private void OnRemoved(DeviceInformationUpdate update)
    {
        lock (_gate)
        {
            if (_disposed || !_entries.Remove(update.Id))
                return;
        }

        Notify();
    }

    private void OnEnumerationCompleted()
    {
        if (Interlocked.Decrement(ref _pendingEnumerations) <= 0)
        {
            IsReady = true;
            Notify();
        }
    }

    private static void ApplyProperties(Entry entry, IReadOnlyDictionary<string, object> properties)
    {
        if (properties.TryGetValue(PropIsConnected, out var connected))
            entry.IsConnected = connected is true;

        if (properties.TryGetValue(PropContainerId, out var container) && container is Guid guid && guid != Guid.Empty)
            entry.ContainerId = guid.ToString("D");

        // Pil güncellemesi yalnız pil değişince gelir; yok olan değer önceki pili silmez.
        if (properties.TryGetValue(PropBattery, out var battery))
            entry.Battery = BluetoothList.ParseBattery(battery);
    }

    // HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_AVAILABLE): "Cihaz kullanıma hazır değil".
    private const int DeviceNotAvailable = unchecked((int)0x800710DF);

    // Cihaz türü yalnız FromIdAsync ile okunur (ClassOfDevice / Appearance); bir kez, sırayla, sonra nesne bırakılır.
    private void QueueKindLookup(Entry entry)
    {
        lock (_gate)
        {
            if (entry.KindRequested)
                return;
            entry.KindRequested = true;
        }

        _ = Task.Run(async () =>
        {
            await _kindGate.WaitAsync();
            try
            {
                if (_disposed)
                    return;

                var kind = entry.IsLowEnergy ? await ReadLeKindAsync(entry.Id) : await ReadClassicKindAsync(entry.Id);
                lock (_gate)
                    entry.Kind = kind;
                Notify();
            }
            catch (COMException ex) when (ex.HResult == DeviceNotAvailable)
            {
                // Eşleşmiş ama erişilemeyen cihaz (kapalı, menzil dışı): olağan durum, tür "diğer" kalır.
                // Eskiden her cihaz ve her izleyici başlangıcında crash.log'a yazılıyor, dosyayı (2000 satır sınırı)
                // dolduruyor ve gerçek hata kayıtlarını siliyordu.
            }
            catch (Exception ex)
            {
                CrashLog.Write("BluetoothService.cihaz türü", ex);
            }
            finally
            {
                _kindGate.Release();
            }
        });
    }

    private static async Task<BluetoothDeviceKind> ReadClassicKindAsync(string id)
    {
        using var device = await BluetoothDevice.FromIdAsync(id);
        return device is null ? BluetoothDeviceKind.Other : BluetoothList.FromClassOfDevice(device.ClassOfDevice.RawValue);
    }

    private static async Task<BluetoothDeviceKind> ReadLeKindAsync(string id)
    {
        using var device = await BluetoothLEDevice.FromIdAsync(id);
        return device is null ? BluetoothDeviceKind.Other : BluetoothList.FromAppearance(device.Appearance.RawValue);
    }

    private static BluetoothDeviceInfo ToInfo(Entry e) =>
        new(e.Id, e.Name, e.Kind, e.IsConnected, e.Battery, e.ContainerId, e.IsLowEnergy);

    private void Notify()
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            if (_disposed)
                return;

            signal = _changed;
            _changed = NewSignal();
        }

        signal.TrySetResult();

        // Başlangıç taraması bitmeden her cihaz için olay yayınlanmaz: arayüz tek seferde güncellenir.
        if (!IsReady)
            return;

        try
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CrashLog.Write("BluetoothService.DevicesChanged abonesi", ex);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<BluetoothActionResult> ConnectAsync(string deviceId) => ChangeAsync(deviceId, connect: true);

    public Task<BluetoothActionResult> DisconnectAsync(string deviceId) => ChangeAsync(deviceId, connect: false);

    private async Task<BluetoothActionResult> ChangeAsync(string deviceId, bool connect)
    {
        string? container;
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
                return BluetoothActionResult.Failed;

            container = entry.ContainerId;
        }

        if (IsConnectedNow(deviceId) == connect)
            return BluetoothActionResult.Confirmed;

        if (container is null)
            return BluetoothActionResult.Unsupported;

        try
        {
            var operation = connect ? BluetoothAudioKs.Operation.Reconnect : BluetoothAudioKs.Operation.Disconnect;
            var sent = await Task.Run(() => BluetoothAudioKs.TrySend(container, operation));
            if (!sent)
                return BluetoothActionResult.Unsupported;

            // Sürücünün isteği alması bağlandığı anlamına gelmez: sonuç bağlantı durumundan okunur.
            return await WaitForStateAsync(deviceId, connect) ? BluetoothActionResult.Confirmed : BluetoothActionResult.NotConfirmed;
        }
        catch (Exception ex)
        {
            CrashLog.Write("BluetoothService.ChangeAsync", ex);
            return BluetoothActionResult.Failed;
        }
    }

    // Aynı fiziksel cihazın klasik ve LE kayıtlarından biri bağlıysa bağlı sayılır (liste birleştirmesiyle aynı kural).
    private bool IsConnectedNow(string deviceId)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(deviceId, out var entry))
                return false;

            return entry.ContainerId is null
                ? entry.IsConnected
                : _entries.Values.Any(e => e.IsConnected && string.Equals(e.ContainerId, entry.ContainerId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<bool> WaitForStateAsync(string deviceId, bool connected)
    {
        var deadline = DateTime.UtcNow + ConfirmTimeout;
        while (true)
        {
            Task changed;
            lock (_gate)
            {
                if (_disposed)
                    return false;
                changed = _changed.Task;
            }

            if (IsConnectedNow(deviceId) == connected)
                return true;

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
                return false;

            await Task.WhenAny(changed, Task.Delay(remaining));
        }
    }

    public void OpenSettings() => SettingsUri.Open("ms-settings:bluetooth");

    public void Dispose()
    {
        DeviceWatcher?[] watchers;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            watchers = [_classicWatcher, _leWatcher];
            _classicWatcher = null;
            _leWatcher = null;
            _entries.Clear();
        }

        foreach (var watcher in watchers)
        {
            if (watcher is null)
                continue;

            try
            {
                if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                    watcher.Stop();
            }
            catch
            {
                // Zaten durmuş izleyici.
            }
        }

        _changed.TrySetResult();
    }
}
