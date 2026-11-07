namespace Koy.Core.Bluetooth;

/// <summary>
/// Bluetooth ses cihazı bağlantı durumu değiştiğinde üretilen HUD olayı.
/// </summary>
public readonly record struct BluetoothHudEvent(string Name, bool Connected, int? BatteryPercent);

/// <summary>
/// Bluetooth cihaz listesindeki bağlantı değişikliklerini izleyip HUD olayları üreten saf mantık.
/// Açılışta tohumlama (seeding) bastırılır; yalnızca ses cihazları (IsAudio) işlenir;
/// aynı cihaz için 3 saniye içindeki titreşimler (debouncing) bastırılır.
/// </summary>
public sealed class BluetoothHudPolicy
{
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromSeconds(3);

    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, bool> _lastEmittedConnected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TimeSpan> _lastEmittedTime = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _deviceNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _isSeeded;

    public BluetoothHudPolicy(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Verilen cihaz listesini ve zamanı işleyerek yeni HUD olaylarını döner.
    /// </summary>
    public IReadOnlyList<BluetoothHudEvent> Update(IReadOnlyList<BluetoothDeviceInfo> devices, TimeSpan now)
    {
        var normalized = BluetoothList.Normalize(devices);
        var audioDevices = normalized.Where(d => d.IsAudio).ToList();

        if (!_isSeeded)
        {
            _isSeeded = true;
            foreach (var device in audioDevices)
            {
                var key = KeyOf(device);
                _lastEmittedConnected[key] = device.IsConnected;
                _deviceNames[key] = device.Name;
            }
            return [];
        }

        var events = new List<BluetoothHudEvent>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in audioDevices)
        {
            var key = KeyOf(device);
            seenKeys.Add(key);
            _deviceNames[key] = device.Name;

            var wasConnected = _lastEmittedConnected.TryGetValue(key, out var prev) && prev;

            if (device.IsConnected != wasConnected)
            {
                if (_lastEmittedTime.TryGetValue(key, out var lastTime) &&
                    (now - lastTime) >= TimeSpan.Zero &&
                    (now - lastTime) < DebounceWindow)
                {
                    // 3 saniyelik titreşim penceresinde bastır
                    continue;
                }

                _lastEmittedTime[key] = now;
                _lastEmittedConnected[key] = device.IsConnected;
                events.Add(new BluetoothHudEvent(device.Name, device.IsConnected, device.BatteryPercent));
            }
        }

        // Önceden bağlı olup listeden tamamen çıkan (bağlantı kopması / eşleşme silinmesi) cihazlar
        foreach (var (key, wasConnected) in _lastEmittedConnected)
        {
            if (wasConnected && !seenKeys.Contains(key))
            {
                if (_lastEmittedTime.TryGetValue(key, out var lastTime) &&
                    (now - lastTime) >= TimeSpan.Zero &&
                    (now - lastTime) < DebounceWindow)
                {
                    continue;
                }

                _lastEmittedTime[key] = now;
                _lastEmittedConnected[key] = false;
                var name = _deviceNames.TryGetValue(key, out var n) ? n : "Bluetooth";
                events.Add(new BluetoothHudEvent(name, Connected: false, BatteryPercent: null));
            }
        }

        return events;
    }

    /// <summary>
    /// TimeProvider kullanarak anlık zaman ile güncelleme yapar.
    /// </summary>
    public IReadOnlyList<BluetoothHudEvent> Update(IReadOnlyList<BluetoothDeviceInfo> devices) =>
        Update(devices, TimeSpan.FromTicks(_timeProvider.GetUtcNow().Ticks));

    /// <summary>
    /// Update ile aynı işlevi görür; alternatif adlandırma için.
    /// </summary>
    public IReadOnlyList<BluetoothHudEvent> ProcessDevices(IReadOnlyList<BluetoothDeviceInfo> devices) =>
        Update(devices);

    private static string KeyOf(BluetoothDeviceInfo device) =>
        !string.IsNullOrEmpty(device.ContainerId) ? device.ContainerId : device.Id;
}
