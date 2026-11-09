using System.Runtime.InteropServices;
using Koy.Core.Bluetooth;
using Koy.Core.Diagnostics;
using Koy.Platform.Wireless;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

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
    private static readonly TimeSpan BatteryThrottle = TimeSpan.FromSeconds(60);

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
        public bool HasPropBattery { get; set; }
    }

    private readonly Lock _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _kindGate = new(1, 1);
    private readonly BluetoothBatteryFilter _batteryFilter = new();
    private readonly Dictionary<string, DateTime> _batteryLookups = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private DeviceWatcher? _classicWatcher;
    private DeviceWatcher? _leWatcher;
    private int _pendingEnumerations;
    private TaskCompletionSource _changed = NewSignal();
    private bool _disposed;
    private bool _batteryReadsEnabled;

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
        QueueBatteryLookup(entry);
        Notify();
    }

    private void OnUpdated(DeviceInformationUpdate update)
    {
        Entry? entryToLookup = null;
        lock (_gate)
        {
            if (_disposed || !_entries.TryGetValue(update.Id, out var entry))
                return;

            var wasConnected = entry.IsConnected;
            ApplyProperties(entry, update.Properties);

            if (!entry.IsConnected && wasConnected)
            {
                _batteryFilter.Reset(update.Id);
                _batteryLookups.Remove(update.Id);
            }
            else if (entry.IsConnected && entry.IsLowEnergy && !entry.HasPropBattery)
            {
                if (!wasConnected || !_batteryLookups.TryGetValue(update.Id, out var last) || (DateTime.UtcNow - last) >= BatteryThrottle)
                {
                    entryToLookup = entry;
                }
            }
        }

        if (entryToLookup is not null)
            QueueBatteryLookup(entryToLookup);

        Notify();
    }

    private void OnRemoved(DeviceInformationUpdate update)
    {
        _batteryFilter.Reset(update.Id);
        lock (_gate)
        {
            _batteryLookups.Remove(update.Id);
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

    private void ApplyProperties(Entry entry, IReadOnlyDictionary<string, object> properties)
    {
        if (properties.TryGetValue(PropIsConnected, out var connected))
            entry.IsConnected = connected is true;

        if (properties.TryGetValue(PropContainerId, out var container) && container is Guid guid && guid != Guid.Empty)
            entry.ContainerId = guid.ToString("D");

        // Pil güncellemesi yalnız pil değişince gelir; yok olan değer önceki pili silmez.
        if (properties.TryGetValue(PropBattery, out var battery))
        {
            var parsed = BluetoothList.ParseBattery(battery);
            if (parsed.HasValue)
            {
                entry.HasPropBattery = true;
                entry.Battery = _batteryFilter.Filter(entry.Id, parsed.Value);
            }
        }
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

    public void SetBatteryReadsEnabled(bool enabled)
    {
        List<Entry> pending = [];
        lock (_gate)
        {
            if (_disposed)
                return;

            _batteryReadsEnabled = enabled;
            if (!enabled)
                return;

            // Sayfa yeni açıldı: pili eksik bağlı BLE cihazlar için okuma hemen sıraya girer.
            foreach (var entry in _entries.Values)
            {
                if (entry.IsConnected && entry.IsLowEnergy && !entry.HasPropBattery)
                    pending.Add(entry);
            }
        }

        foreach (var entry in pending)
            QueueBatteryLookup(entry);
    }

    private void QueueBatteryLookup(Entry entry)
    {
        string id;
        lock (_gate)
        {
            if (_disposed || !_batteryReadsEnabled || !entry.IsConnected || !entry.IsLowEnergy || entry.HasPropBattery)
                return;

            if (_batteryLookups.TryGetValue(entry.Id, out var last) && (DateTime.UtcNow - last) < BatteryThrottle)
                return;

            _batteryLookups[entry.Id] = DateTime.UtcNow;
            id = entry.Id;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                bool enabled;
                lock (_gate)
                    enabled = _batteryReadsEnabled;

                if (_cts.IsCancellationRequested || _disposed || !enabled)
                    return;

                var battery = await ReadGattBatteryAsync(id, _cts.Token);
                if (battery.HasValue)
                {
                    var filtered = _batteryFilter.Filter(id, battery.Value);
                    lock (_gate)
                    {
                        if (_disposed || !_entries.TryGetValue(id, out var current))
                            return;
                        current.Battery = filtered;
                    }
                    Notify();
                }
            }
            catch (COMException ex) when (ex.HResult == DeviceNotAvailable)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                CrashLog.Write("BluetoothService.GATT pil okuma", ex);
            }
        });
    }

    private static async Task<int?> ReadGattBatteryAsync(string id, CancellationToken ct)
    {
        using var device = await BluetoothLEDevice.FromIdAsync(id);
        if (device is null || device.ConnectionStatus != BluetoothConnectionStatus.Connected)
            return null;

        if (ct.IsCancellationRequested)
            return null;

        var servicesResult = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Cached);
        if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
        {
            if (ct.IsCancellationRequested)
                return null;

            servicesResult = await device.GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success || servicesResult.Services.Count == 0)
                return null;
        }

        var readings = new List<ReadOnlyMemory<byte>>();

        try
        {
            foreach (var service in servicesResult.Services)
            {
                if (ct.IsCancellationRequested)
                    break;

                var charResult = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Cached);
                if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                {
                    charResult = await service.GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached);
                    if (charResult.Status != GattCommunicationStatus.Success || charResult.Characteristics.Count == 0)
                        continue;
                }

                foreach (var ch in charResult.Characteristics)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    var readResult = await ch.ReadValueAsync(BluetoothCacheMode.Cached);
                    if (readResult.Status != GattCommunicationStatus.Success || readResult.Value is null || readResult.Value.Length == 0)
                    {
                        readResult = await ch.ReadValueAsync(BluetoothCacheMode.Uncached);
                    }

                    if (readResult.Status == GattCommunicationStatus.Success && readResult.Value is { Length: > 0 } buffer)
                    {
                        var bytes = new byte[buffer.Length];
                        using var reader = DataReader.FromBuffer(buffer);
                        reader.ReadBytes(bytes);
                        readings.Add(bytes);
                    }
                }
            }
        }
        finally
        {
            foreach (var service in servicesResult.Services)
            {
                service.Dispose();
            }
        }

        return GattBatteryParser.ParseLowest(readings);
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
            _cts.Cancel();
            _cts.Dispose();
            _batteryFilter.Clear();
            _batteryLookups.Clear();
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
