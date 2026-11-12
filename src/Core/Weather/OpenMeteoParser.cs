using System.Text.Json;

namespace Koy.Core.Weather;

/// <summary>
/// Open-Meteo yanıtlarını ayrıştırır. Saf: ağ yok, bozuk ya da eksik yanıt istisna fırlatmaz (null / boş liste döner),
/// böylece servis çökmeden önbelleğe düşebilir.
/// </summary>
public static class OpenMeteoParser
{
    /// <summary>Şehir aramasında dönen en çok sonuç (istekteki <c>count</c> ile aynı).</summary>
    public const int MaxResults = 5;

    /// <summary>Adın en az bu kadar karakter olması gerekir (Open-Meteo 3+ karakterde önek eşleşmesi yapıyor).</summary>
    public const int MinQueryLength = 2;

    public static (double Temperature, int WeatherCode)? ParseCurrent(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var current) ||
                !current.TryGetProperty("temperature_2m", out var temp) ||
                !current.TryGetProperty("weather_code", out var code) ||
                temp.ValueKind != JsonValueKind.Number ||
                code.ValueKind != JsonValueKind.Number)
                return null;

            return (temp.GetDouble(), code.GetInt32());
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Anlık rüzgâr hızı (<c>current.wind_speed_10m</c>, Open-Meteo varsayılanı km/sa). Alan yoksa, sayı değilse ya da
    /// negatifse null: rüzgâr yoksa hava durumu yine de gösterilir.
    /// </summary>
    public static double? ParseWindSpeed(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out var current) ||
                !current.TryGetProperty("wind_speed_10m", out var wind) ||
                wind.ValueKind != JsonValueKind.Number)
                return null;

            var value = wind.GetDouble();
            return double.IsFinite(value) && value >= 0 ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static IReadOnlyList<GeoPlace> ParseGeocoding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return [];

            var places = new List<GeoPlace>();
            foreach (var item in results.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var name = Text(item, "name");
                if (string.IsNullOrWhiteSpace(name) ||
                    !item.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number ||
                    !item.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number)
                    continue;

                var latitude = lat.GetDouble();
                var longitude = lon.GetDouble();
                if (!IsValidCoordinate(latitude, longitude))
                    continue;

                places.Add(new GeoPlace(name!, Text(item, "admin1"), Text(item, "country"), latitude, longitude));
                if (places.Count == MaxResults)
                    break;
            }

            return places;
        }
        catch (JsonException)
        {
            return [];
        }
        catch (FormatException)
        {
            return [];
        }
    }

    public static bool IsValidCoordinate(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    private static string? Text(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
