using Koy.Core.Appearance;
using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Notch;

public class CollapsedBackgroundPolicyTests
{
    private static readonly Rgb LightClockText = new(0xF4, 0xF4, 0xF5);

    [Fact]
    public void All_presets_resolve_to_readable_dark_stops()
    {
        foreach (var preset in LiveBackgroundPresets.All)
        {
            var stops = CollapsedBackgroundPolicy.ResolveAmbientStops(preset.Id);

            Assert.NotEmpty(stops);
            foreach (var stop in stops)
            {
                var contrast = ColorMath.Contrast(stop, LightClockText);
                Assert.True(contrast >= CollapsedBackgroundPolicy.MinTextContrast,
                    $"Preset '{preset.Id}' stop {stop} had contrast {contrast:F2} (< {CollapsedBackgroundPolicy.MinTextContrast}).");
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non-existent-preset")]
    public void Unknown_or_empty_preset_falls_back_cleanly(string? presetId)
    {
        var stops = CollapsedBackgroundPolicy.ResolveAmbientStops(presetId);

        Assert.NotEmpty(stops);
        Assert.All(stops, s => Assert.True(ColorMath.Contrast(s, LightClockText) >= CollapsedBackgroundPolicy.MinTextContrast));
    }

    [Fact]
    public void Ensure_readable_darkens_bright_stops_while_preserving_dark_stops()
    {
        var brightWhite = new Rgb(0xFF, 0xFF, 0xFF);
        var darkBase = new Rgb(0x0A, 0x0A, 0x0B);

        var darkened = CollapsedBackgroundPolicy.EnsureReadable(brightWhite);
        var kept = CollapsedBackgroundPolicy.EnsureReadable(darkBase);

        Assert.True(ColorMath.Contrast(darkened, LightClockText) >= CollapsedBackgroundPolicy.MinTextContrast);
        Assert.Equal(darkBase, kept);
    }

    [Fact]
    public void SettingsKeyMap_maps_NotchCollapsedBackground()
    {
        Assert.True(SettingsKeyMap.V3ToV4.TryGetValue("NotchCollapsedBackground", out var key));
        Assert.Equal(("Notch", "CollapsedBackground"), key);
    }
}
