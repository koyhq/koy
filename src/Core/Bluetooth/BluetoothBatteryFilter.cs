namespace Koy.Core.Bluetooth;

/// <summary>
/// Bluetooth cihazlarında (fare, klavye, kulaklık) yük altındaki anlık voltaj düşüşlerini,
/// toparlanma sıçramalarını ve ardışık tek haneli ADC titreşimlerini süzen saf C# filtresi.
/// Kayan pencerede medyan hesabı ve histerezis eşiği uygular.
/// </summary>
public sealed class BluetoothBatteryFilter
{
    private readonly int _windowSize;
    private readonly int _hysteresisThreshold;
    private readonly int _consecutiveThreshold;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, DeviceState> _devices = new(StringComparer.OrdinalIgnoreCase);

    public BluetoothBatteryFilter(int windowSize = 3, int hysteresisThreshold = 2, int consecutiveThreshold = 3)
    {
        if (windowSize is < 1 or > 64)
            throw new ArgumentOutOfRangeException(nameof(windowSize), "Pencere boyutu 1 ile 64 arasında olmalıdır.");
        if (hysteresisThreshold < 0)
            throw new ArgumentOutOfRangeException(nameof(hysteresisThreshold), "Histerezis eşiği negatif olamaz.");
        if (consecutiveThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(consecutiveThreshold), "Ardışık eşik en az 1 olmalıdır.");

        _windowSize = windowSize;
        _hysteresisThreshold = hysteresisThreshold;
        _consecutiveThreshold = consecutiveThreshold;
    }

    /// <summary>
    /// Belirtilen cihaz için ham pil seviyesini (0..100) filtreleyip kararlı pil seviyesini döner.
    /// </summary>
    public int Filter(string deviceId, int rawPercent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        var clamped = Math.Clamp(rawPercent, 0, 100);

        lock (_gate)
        {
            if (!_devices.TryGetValue(deviceId, out var state))
            {
                state = new DeviceState(_windowSize, _hysteresisThreshold, _consecutiveThreshold);
                _devices[deviceId] = state;
            }

            return state.Push(clamped);
        }
    }

    /// <summary>
    /// Cihaz yeniden bağlandığında veya bağlantısı kesildiğinde filtre geçmişini sıfırlar.
    /// </summary>
    public void Reset(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return;

        lock (_gate)
        {
            _devices.Remove(deviceId);
        }
    }

    /// <summary>Tüm cihaz geçmişini temizler.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _devices.Clear();
        }
    }

    private sealed class DeviceState(int windowSize, int hysteresisThreshold, int consecutiveThreshold)
    {
        private readonly List<int> _samples = new(windowSize);
        private int? _lastReported;
        private int _consecutiveCount;
        private int _lastDirection; // -1: azalan, +1: artan

        public int Push(int sample)
        {
            if (_samples.Count >= windowSize)
                _samples.RemoveAt(0);
            _samples.Add(sample);

            var median = CalculateMedian();

            if (!_lastReported.HasValue)
            {
                _lastReported = median;
                return median;
            }

            var current = _lastReported.Value;
            var delta = median - current;

            if (delta == 0)
            {
                _consecutiveCount = 0;
                _lastDirection = 0;
                return current;
            }

            var direction = Math.Sign(delta);

            // Eşik aşıldıysa doğrudan uygula (örn. %2 ve üzeri değişimler veya hızlı şarj)
            if (Math.Abs(delta) >= hysteresisThreshold)
            {
                _lastReported = median;
                _consecutiveCount = 0;
                _lastDirection = 0;
                return median;
            }

            // Küçük titreşimlerde (örn. %1 değişim) ardışık aynı yön kontrolü
            if (direction == _lastDirection)
            {
                _consecutiveCount++;
            }
            else
            {
                _lastDirection = direction;
                _consecutiveCount = 1;
            }

            if (_consecutiveCount >= consecutiveThreshold)
            {
                _lastReported = median;
                _consecutiveCount = 0;
                _lastDirection = 0;
                return median;
            }

            return current;
        }

        private int CalculateMedian()
        {
            var count = _samples.Count;
            if (count == 1)
                return _samples[0];

            Span<int> copy = stackalloc int[count];
            for (var i = 0; i < count; i++)
                copy[i] = _samples[i];

            copy.Sort();

            if ((count % 2) != 0)
                return copy[count / 2];

            var mid = count / 2;
            return (copy[mid - 1] + copy[mid]) / 2;
        }
    }
}
