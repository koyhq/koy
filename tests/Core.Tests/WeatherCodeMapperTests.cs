using Koy.Core.Weather;

namespace Koy.Core.Tests;

public sealed class WeatherCodeMapperTests
{
    [Theory]
    [InlineData(0, "WeatherCondClear", "Sun")]
    [InlineData(1, "WeatherCondMostlyClear", "PartlyCloudy")]
    [InlineData(2, "WeatherCondPartlyCloudy", "PartlyCloudy")]
    [InlineData(3, "WeatherCondOvercast", "Cloud")]
    [InlineData(45, "WeatherCondFog", "Fog")]
    [InlineData(61, "WeatherCondLightRain", "Rain")]
    [InlineData(71, "WeatherCondLightSnow", "Snow")]
    [InlineData(95, "WeatherCondThunderstorm", "Thunderstorm")]
    [InlineData(999, "WeatherCondUnknown", "Cloud")]
    public void Maps_Wmo_Codes_Correctly(int code, string expectedKey, string expectedIcon)
    {
        Assert.Equal(expectedKey, WeatherCodeMapper.ToDescriptionKey(code));
        Assert.Equal(expectedIcon, WeatherCodeMapper.ToIconKind(code));
    }
}
