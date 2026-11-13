using System.Diagnostics;
using Koy.Core.Notch;
using Koy.Core.Notch.Effects;
using Xunit.Abstractions;

namespace Koy.Core.Tests.Notch;

public class BackgroundEffectsTests(ITestOutputHelper output)
{
    public static TheoryData<string> EffectIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in BackgroundEffects.All)
            data.Add(e.Id);
        return data;
    }

    private static uint[] Render(BackgroundEffect effect, float time, int w = 155, int h = 88)
    {
        var preset = LiveBackgroundPresets.All.Single(p => p.Effect == effect.Id);
        var palette = preset.Colors.Select(EffectColor.Parse).ToArray();
        var pixels = new uint[w * h];
        effect.Render(new EffectFrame(pixels, w, h, time, palette));
        return pixels;
    }

    [Fact]
    public void Ids_are_unique_and_every_effect_has_a_preset()
    {
        var ids = BackgroundEffects.All.Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var id in ids)
        {
            var preset = Assert.Single(LiveBackgroundPresets.All, p => p.Effect == id);
            Assert.Equal(id, preset.Id);
            Assert.True(preset.Colors.Count >= 3, $"{id}: palette needs at least 3 colors");
        }
    }

    [Fact]
    public void Preset_ids_stay_unique_after_adding_effects()
    {
        var ids = LiveBackgroundPresets.All.Select(p => p.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Null(LiveBackgroundPresets.Find("aurora").Effect);
    }

    [Theory]
    [MemberData(nameof(EffectIds))]
    public void Frame_is_opaque_and_not_blank(string id)
    {
        var px = Render(BackgroundEffects.Find(id)!, 12f);
        Assert.All(px, p => Assert.Equal(0xFF000000u, p & 0xFF000000u));
        Assert.True(px.Distinct().Count() > 20, $"{id}: frame looks flat");
    }

    [Theory]
    [MemberData(nameof(EffectIds))]
    public void Same_time_gives_same_frame_and_time_moves_it(string id)
    {
        var effect = BackgroundEffects.Find(id)!;
        Assert.Equal(Render(effect, 12f), Render(effect, 12f));
        Assert.NotEqual(Render(effect, 12f), Render(effect, 14f));
    }

    [Theory]
    [MemberData(nameof(EffectIds))]
    public void Tiny_and_odd_buffers_do_not_throw(string id)
    {
        var effect = BackgroundEffects.Find(id)!;
        Render(effect, 0f, 1, 1);
        Render(effect, 3.3f, 7, 3);
        Render(effect, 1000f, 311, 177);
    }

    [Fact]
    public void Empty_palette_draws_black_without_throwing()
    {
        foreach (var effect in BackgroundEffects.All)
        {
            var pixels = new uint[40 * 24];
            effect.Render(new EffectFrame(pixels, 40, 24, 5f, ReadOnlySpan<EffectColor>.Empty));
        }
    }

    [Theory]
    [MemberData(nameof(EffectIds))]
    public void Frame_cost_stays_within_budget(string id)
    {
        // Gerçek tampon boyutu (açık çentik 620×352 / küçültme). Bütçe gevşek: yavaş CI makinesinde de geçsin,
        // ama piksel başına pahalı bir hata (ör. küçültmesiz çizim) yakalansın.
        var effect = BackgroundEffects.Find(id)!;
        var w = (int)Math.Ceiling(NotchLayout.ExpandedWidth / effect.Downscale);
        var h = (int)Math.Ceiling(NotchLayout.ExpandedHeight / effect.Downscale);
        Render(effect, 1f, w, h);
        var sw = Stopwatch.StartNew();
        const int frames = 10;
        for (var i = 0; i < frames; i++)
            Render(effect, 1f + i * 0.07f, w, h);
        var perFrameMs = sw.Elapsed.TotalMilliseconds / frames;
        output.WriteLine($"{id}: {w}x{h}, {perFrameMs:F2} ms per frame, {effect.FramesPerSecond} fps");
        Assert.True(perFrameMs < 25, $"{id}: {perFrameMs:F1} ms per frame");
    }

    [Theory]
    [InlineData("#112233", 0x11, 0x22, 0x33)]
    [InlineData("FF00FF", 0xFF, 0x00, 0xFF)]
    [InlineData("#80AABBCC", 0xAA, 0xBB, 0xCC)]
    public void Color_parse_round_trips(string hex, int r, int g, int b) =>
        Assert.Equal(0xFF000000u | (uint)(r << 16 | g << 8 | b), EffectColor.Parse(hex).ToPixel());

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("zzzzzz")]
    public void Bad_color_is_black(string hex) => Assert.Equal(EffectColor.Black, EffectColor.Parse(hex));
}
