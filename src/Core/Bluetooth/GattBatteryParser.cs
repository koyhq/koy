namespace Koy.Core.Bluetooth;

/// <summary>
/// Bluetooth SIG GATT Battery Service (0x180F) ve Battery Level (0x2A19) karakteristiği için saf C# ayrıştırıcı.
/// Standart gereği pil seviyesi 0..100 arasında tek bayttır.
/// </summary>
public static class GattBatteryParser
{
    /// <summary>
    /// Ham GATT karakteristik verisini (0x2A19) pil yüzdesine (0..100) çevirir.
    /// Veri boşsa veya 100'den büyükse (Bluetooth SIG reserved) null döner.
    /// </summary>
    public static int? Parse(ReadOnlySpan<byte> raw)
    {
        if (raw.IsEmpty)
            return null;

        int value = raw[0];
        return value <= 100 ? value : null;
    }

    /// <summary>
    /// Çoklu pil karakteristiği (örn. TWS kulaklıkların sol/sağ modülleri) okunduğunda
    /// kullanıcıya uyarı vermek adına en düşük (kritik) pil seviyesini seçer.
    /// </summary>
    public static int? ParseLowest(IEnumerable<ReadOnlyMemory<byte>>? readings)
    {
        if (readings is null)
            return null;

        int? lowest = null;
        foreach (var reading in readings)
        {
            var parsed = Parse(reading.Span);
            if (parsed.HasValue)
            {
                if (!lowest.HasValue || parsed.Value < lowest.Value)
                    lowest = parsed.Value;
            }
        }
        return lowest;
    }
}
