using Koy.Core.Settings;
using Koy.Core.Weather;

namespace Koy.Core.Tests;

public class WeatherLocationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_settings_have_no_location_so_weather_is_hidden()
    {
        var settings = new AppSettings();

        Assert.Equal(WeatherLocationSource.None, settings.Weather.LocationSource);
        Assert.False(settings.Weather.UseLocation);
        Assert.False(WeatherLocationPolicy.HasLocation(settings));
    }

    [Fact]
    public void Old_hardcoded_istanbul_without_a_source_is_not_a_location()
    {
        // Eski ayar dosyaları İstanbul'u varsayılan olarak yazmıştı; kaynak yoksa yok sayılır.
        var settings = new AppSettings { Weather = new WeatherSettings { Latitude = 41.01, Longitude = 28.97, City = "İstanbul" } };

        Assert.False(WeatherLocationPolicy.HasLocation(settings));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(91, 10)]
    [InlineData(10, 181)]
    public void Invalid_coordinates_are_not_a_location(double lat, double lon)
    {
        var settings = new AppSettings { Weather = new WeatherSettings { LocationSource = WeatherLocationSource.City, Latitude = lat, Longitude = lon } };

        Assert.False(WeatherLocationPolicy.HasLocation(settings));
    }

    [Fact]
    public void Refresh_is_not_asked_when_user_did_not_allow_location()
    {
        Assert.False(WeatherLocationPolicy.ShouldRefreshDeviceLocation(new AppSettings(), Now));
    }

    [Fact]
    public void Refresh_is_asked_when_allowed_but_no_device_location_yet()
    {
        var settings = new AppSettings { Weather = new WeatherSettings { UseLocation = true } };

        Assert.True(WeatherLocationPolicy.ShouldRefreshDeviceLocation(settings, Now));
    }

    [Fact]
    public void Refresh_is_asked_when_allowed_and_a_city_is_only_the_fallback()
    {
        var settings = WeatherLocationPolicy.ApplyCity(new AppSettings(), new GeoPlace("Ankara", null, "Türkiye", 39.9, 32.8), Now);
        settings = WeatherLocationPolicy.SetUseLocation(settings, true);

        Assert.True(WeatherLocationPolicy.ShouldRefreshDeviceLocation(settings, Now));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(23, false)]
    [InlineData(24, true)]
    [InlineData(80, true)]
    public void Device_location_is_refreshed_at_most_once_a_day(int hoursAgo, bool expected)
    {
        var settings = WeatherLocationPolicy.ApplyDeviceLocation(
            new AppSettings { Weather = new WeatherSettings { UseLocation = true } },
            new LocationResult(LocationStatus.Success, 41.0, 29.0),
            Now.AddHours(-hoursAgo));

        Assert.Equal(expected, WeatherLocationPolicy.ShouldRefreshDeviceLocation(settings, Now));
    }

    [Fact]
    public void A_record_dated_in_the_future_is_refreshed()
    {
        var settings = WeatherLocationPolicy.ApplyDeviceLocation(
            new AppSettings { Weather = new WeatherSettings { UseLocation = true } },
            new LocationResult(LocationStatus.Success, 41.0, 29.0),
            Now.AddHours(5));

        Assert.True(WeatherLocationPolicy.ShouldRefreshDeviceLocation(settings, Now));
    }

    [Fact]
    public void Successful_device_location_sets_coordinates_and_clears_the_city()
    {
        var before = new AppSettings { Weather = new WeatherSettings { UseLocation = true, City = "Eski" } };

        var after = WeatherLocationPolicy.ApplyDeviceLocation(before, new LocationResult(LocationStatus.Success, 40.5, 29.5), Now);

        Assert.Equal(WeatherLocationSource.Device, after.Weather.LocationSource);
        Assert.Equal(40.5, after.Weather.Latitude);
        Assert.Equal(29.5, after.Weather.Longitude);
        Assert.Equal("", after.Weather.City);
        Assert.Equal(Now.ToUnixTimeSeconds(), after.Weather.LocationUpdatedUnix);
        Assert.True(WeatherLocationPolicy.HasLocation(after));
    }

    [Theory]
    [InlineData(LocationStatus.Denied)]
    [InlineData(LocationStatus.Unavailable)]
    [InlineData(LocationStatus.TimedOut)]
    [InlineData(LocationStatus.Error)]
    public void Failed_device_location_leaves_settings_untouched(LocationStatus status)
    {
        var before = WeatherLocationPolicy.ApplyCity(new AppSettings { Weather = new WeatherSettings { UseLocation = true } },
            new GeoPlace("Ankara", null, null, 39.9, 32.8), Now);
        before = before with { Weather = before.Weather with { UseLocation = true } };

        var after = WeatherLocationPolicy.ApplyDeviceLocation(before, new LocationResult(status), Now.AddDays(1));

        Assert.Equal(before, after);
    }

    [Fact]
    public void Out_of_range_device_coordinates_are_rejected()
    {
        var before = new AppSettings { Weather = new WeatherSettings { UseLocation = true } };

        Assert.Equal(before, WeatherLocationPolicy.ApplyDeviceLocation(before, new LocationResult(LocationStatus.Success, 120, 10), Now));
    }

    [Fact]
    public void Choosing_a_city_turns_device_location_off()
    {
        var before = new AppSettings { Weather = new WeatherSettings { UseLocation = true, LocationSource = WeatherLocationSource.Device, Latitude = 41, Longitude = 29 } };

        var after = WeatherLocationPolicy.ApplyCity(before, new GeoPlace("İzmir", "İzmir", "Türkiye", 38.4, 27.1), Now);

        Assert.Equal(WeatherLocationSource.City, after.Weather.LocationSource);
        Assert.False(after.Weather.UseLocation);
        Assert.Equal("İzmir, Türkiye", after.Weather.City);
        Assert.Equal(38.4, after.Weather.Latitude);
    }

    [Fact]
    public void Turning_location_off_drops_device_coordinates_but_keeps_a_chosen_city()
    {
        var device = WeatherLocationPolicy.ApplyDeviceLocation(
            new AppSettings { Weather = new WeatherSettings { UseLocation = true } }, new LocationResult(LocationStatus.Success, 41, 29), Now);
        var city = WeatherLocationPolicy.ApplyCity(new AppSettings(), new GeoPlace("Bursa", null, null, 40.2, 29.0), Now);

        var offDevice = WeatherLocationPolicy.SetUseLocation(device, false);
        var offCity = WeatherLocationPolicy.SetUseLocation(city with { Weather = city.Weather with { UseLocation = true } }, false);

        Assert.False(WeatherLocationPolicy.HasLocation(offDevice));
        Assert.Equal(0, offDevice.Weather.Latitude);
        Assert.False(offDevice.Weather.UseLocation);
        Assert.True(WeatherLocationPolicy.HasLocation(offCity));
        Assert.Equal(WeatherLocationSource.City, offCity.Weather.LocationSource);
    }

    [Fact]
    public void Place_display_name_skips_repeats_and_missing_parts()
    {
        Assert.Equal("Ankara, Türkiye", new GeoPlace("Ankara", "Ankara", "Türkiye", 1, 1).DisplayName);
        Assert.Equal("Kadıköy, İstanbul, Türkiye", new GeoPlace("Kadıköy", "İstanbul", "Türkiye", 1, 1).DisplayName);
        Assert.Equal("Yer", new GeoPlace("Yer", null, null, 1, 1).DisplayName);
    }

    [Fact]
    public void Settings_with_location_survive_a_save_and_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), "koy-weather-tests-" + Guid.NewGuid());
        try
        {
            var store = new SettingsStore(Path.Combine(dir, "settings.json"));
            var saved = WeatherLocationPolicy.ApplyDeviceLocation(
                new AppSettings { Weather = new WeatherSettings { UseLocation = true } }, new LocationResult(LocationStatus.Success, 41.25, 29.75), Now);
            store.Save(saved);

            var loaded = store.Load();

            Assert.Equal(WeatherLocationSource.Device, loaded.Weather.LocationSource);
            Assert.True(loaded.Weather.UseLocation);
            Assert.Equal(41.25, loaded.Weather.Latitude);
            Assert.Equal(Now.ToUnixTimeSeconds(), loaded.Weather.LocationUpdatedUnix);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}

public class OpenMeteoParserTests
{
    [Fact]
    public void Current_weather_is_parsed()
    {
        var json = """{"latitude":41.0,"current":{"time":"2026-11-06T09:00","temperature_2m":14.6,"weather_code":61}}""";

        var result = OpenMeteoParser.ParseCurrent(json);

        Assert.NotNull(result);
        Assert.Equal(14.6, result.Value.Temperature);
        Assert.Equal(61, result.Value.WeatherCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bozuk json")]
    [InlineData("{}")]
    [InlineData("""{"current":{"temperature_2m":"sıcak","weather_code":1}}""")]
    [InlineData("""{"current":{"temperature_2m":10}}""")]
    [InlineData("""{"error":true,"reason":"limit"}""")]
    public void Bad_current_weather_responses_return_null(string? json)
    {
        Assert.Null(OpenMeteoParser.ParseCurrent(json));
    }

    [Fact]
    public void Geocoding_results_are_parsed_and_capped()
    {
        var json = """
        {"results":[
          {"name":"Ankara","latitude":39.91987,"longitude":32.85427,"admin1":"Ankara","country":"Türkiye"},
          {"name":"Ankara","latitude":-1.5,"longitude":10.5,"country":"Gabon"},
          {"name":"A3","latitude":1,"longitude":1},
          {"name":"A4","latitude":1,"longitude":1},
          {"name":"A5","latitude":1,"longitude":1},
          {"name":"A6","latitude":1,"longitude":1}
        ]}
        """;

        var places = OpenMeteoParser.ParseGeocoding(json);

        Assert.Equal(OpenMeteoParser.MaxResults, places.Count);
        Assert.Equal("Ankara, Türkiye", places[0].DisplayName);
        Assert.Equal(39.91987, places[0].Latitude);
        Assert.Equal("Ankara, Gabon", places[1].DisplayName);
    }

    [Fact]
    public void Geocoding_skips_entries_without_a_name_or_valid_coordinates()
    {
        var json = """
        {"results":[
          {"latitude":1,"longitude":1},
          {"name":"Uzak","latitude":200,"longitude":1},
          {"name":"Eksik","latitude":1},
          {"name":"İyi","latitude":10,"longitude":20}
        ]}
        """;

        var places = OpenMeteoParser.ParseGeocoding(json);

        Assert.Single(places);
        Assert.Equal("İyi", places[0].Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"generationtime_ms":0.5}""")]
    [InlineData("""{"results":"yok"}""")]
    [InlineData("[1,2")]
    public void Empty_or_bad_geocoding_responses_return_an_empty_list(string? json)
    {
        Assert.Empty(OpenMeteoParser.ParseGeocoding(json));
    }
}
