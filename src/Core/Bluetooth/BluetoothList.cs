using Koy.Core.SystemStats;
using System.Globalization;
using Koy.Core.Radio;

namespace Koy.Core.Bluetooth;

/// <summary>Bluetooth listesinin saf mantığı: cihaz türü eşleme, birleştirme, sıralama, pil ve durum metinleri.</summary>
public static class BluetoothList
{
    // Class of Device (24 bit): ana sınıf bit 8-12, alt sınıf bit 2-7.
    private const int MajorComputer = 1;
    private const int MajorPhone = 2;
    private const int MajorAudioVideo = 4;
    private const int MajorPeripheral = 5;

    public static BluetoothDeviceKind FromClassOfDevice(uint cod)
    {
        var major = (int)((cod >> 8) & 0x1F);
        var minor = (int)((cod >> 2) & 0x3F);

        return major switch
        {
            MajorPhone => BluetoothDeviceKind.Phone,
            MajorComputer => minor == 7 ? BluetoothDeviceKind.Tablet : BluetoothDeviceKind.Other,
            MajorAudioVideo => minor switch
            {
                // Kulaklık takımı, eller serbest, kulaklık
                1 or 2 or 6 => BluetoothDeviceKind.Headphones,
                // Hoparlör, taşınabilir ses, hi-fi, ekran + hoparlör ve ayrıntısız ses cihazları
                _ => BluetoothDeviceKind.Speaker
            },
            MajorPeripheral => ((minor >> 4) & 0x3) switch
            {
                // Alt sınıfın üst iki biti: 1 klavye, 2 işaretçi (fare, dokunmatik yüzey), 3 ikisi birden
                1 or 3 => BluetoothDeviceKind.Keyboard,
                2 => BluetoothDeviceKind.Mouse,
                _ => BluetoothDeviceKind.Other
            },
            _ => BluetoothDeviceKind.Other
        };
    }

    /// <summary>BLE "Appearance" (16 bit: kategori = değer &gt;&gt; 6, alt kategori = alt 6 bit) değerinden tür.</summary>
    public static BluetoothDeviceKind FromAppearance(uint appearance)
    {
        var category = (int)(appearance >> 6);
        var sub = (int)(appearance & 0x3F);

        return category switch
        {
            1 => BluetoothDeviceKind.Phone,
            2 => sub == 7 ? BluetoothDeviceKind.Tablet : BluetoothDeviceKind.Other,
            // HID: 1 klavye, 2 fare, 9 dokunmatik yüzey
            15 => sub switch
            {
                1 => BluetoothDeviceKind.Keyboard,
                2 or 9 => BluetoothDeviceKind.Mouse,
                _ => BluetoothDeviceKind.Other
            },
            // Ses alıcısı (hoparlör, soundbar)
            33 => BluetoothDeviceKind.Speaker,
            // Giyilebilir ses cihazı (kulakiçi, kulaklık, boyunluk)
            37 => BluetoothDeviceKind.Headphones,
            _ => BluetoothDeviceKind.Other
        };
    }

    /// <summary>
    /// Aynı fiziksel cihazın (aynı ContainerId) klasik ve LE kayıtlarını tek satıra indirir: klasik kayıt tercih edilir,
    /// bağlı ve pil bilgisi iki kayıttan birleştirilir. Adsız kayıtlar atılır. Sıra: bağlı olanlar, sonra ada göre.
    /// </summary>
    public static List<BluetoothDeviceInfo> Normalize(IEnumerable<BluetoothDeviceInfo> raw)
    {
        var result = new List<BluetoothDeviceInfo>();
        var byContainer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in raw.OrderBy(d => d.IsLowEnergy).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(device.Name))
                continue;

            if (device.ContainerId is { Length: > 0 } container)
            {
                if (byContainer.TryGetValue(container, out var index))
                {
                    var kept = result[index];
                    result[index] = kept with
                    {
                        IsConnected = kept.IsConnected || device.IsConnected,
                        BatteryPercent = kept.BatteryPercent ?? device.BatteryPercent,
                        Kind = kept.Kind == BluetoothDeviceKind.Other ? device.Kind : kept.Kind
                    };
                    continue;
                }

                byContainer[container] = result.Count;
            }

            result.Add(device);
        }

        return result
            .OrderByDescending(d => d.IsConnected)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Ana'daki kulaklık kartı için: bağlı ses cihazı (pili olan önce); yoksa null.</summary>
    public static BluetoothDeviceInfo? PrimaryAudio(IEnumerable<BluetoothDeviceInfo> normalized) =>
        normalized.Where(d => d.IsConnected && d.IsAudio)
            .OrderByDescending(d => d.BatteryPercent.HasValue)
            .FirstOrDefault();

    /// <summary>Dile uygun yüzde ("%86", "86%"); pil bilinmiyorsa ya da aralık dışıysa null (hiçbir şey gösterilmez).</summary>
    public static string? BatteryText(int? percent, CultureInfo culture) =>
        percent is >= 0 and <= 100 ? SystemFormat.Percent(percent.Value, culture) : null;

    /// <summary>Ham pil değerini (bayt) yüzdeye çevirir; 0-100 dışı değer ya da okunamayan değer "pil yok" sayılır.</summary>
    public static int? ParseBattery(object? raw)
    {
        try
        {
            if (raw is null)
                return null;

            var value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            return value is >= 0 and <= 100 ? value : null;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>Satırın durum anahtarı (Strings.resx).</summary>
    public static string StatusKey(BluetoothDeviceInfo device) =>
        device.IsConnected ? "BluetoothConnected" : "BluetoothNotConnected";

    public static BluetoothRowAction ActionFor(BluetoothDeviceInfo device)
    {
        if (!device.IsAudio)
            return BluetoothRowAction.OpenSettings;

        return device.IsConnected ? BluetoothRowAction.Disconnect : BluetoothRowAction.Connect;
    }

    /// <summary>Radyo durumu ve servis hazırlığından listenin hangi hâlde gösterileceği.</summary>
    public static BluetoothListState Resolve(RadioTileStatus radio, bool serviceReady, int deviceCount) => radio switch
    {
        RadioTileStatus.NoAccess => BluetoothListState.NoAccess,
        RadioTileStatus.NotFound => BluetoothListState.NotFound,
        RadioTileStatus.Off or RadioTileStatus.HardwareOff => BluetoothListState.RadioOff,
        RadioTileStatus.Loading => BluetoothListState.Loading,
        _ => !serviceReady ? BluetoothListState.Loading : deviceCount == 0 ? BluetoothListState.Empty : BluetoothListState.Ready
    };

    /// <summary>Bağlan / kes sonucu için hata metni anahtarı; doğrulanmış başarıda null.</summary>
    public static string? ErrorKey(BluetoothActionResult result, bool connecting) => result switch
    {
        BluetoothActionResult.Confirmed => null,
        BluetoothActionResult.Unsupported => "BluetoothErrUnsupported",
        _ => connecting ? "BluetoothErrConnect" : "BluetoothErrDisconnect"
    };
}
