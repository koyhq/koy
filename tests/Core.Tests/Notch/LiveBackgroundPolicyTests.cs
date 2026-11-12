using System.Globalization;
using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.Core.Tests.Notch;

public class LiveBackgroundPolicyTests
{
    private const LiveBackgroundTarget Both = LiveBackgroundTarget.Both;

    // ---- Oynatma kararı: tüm oynat/durdur kombinasyonları ----

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]  // çentik kapalı
    [InlineData(true, false, false, false, false)]  // pencere gizli
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, true, false, false)]    // hareketi azalt
    [InlineData(true, true, false, true, false)]    // pil tasarrufu
    [InlineData(true, true, true, true, false)]
    [InlineData(false, true, true, true, false)]
    public void ShouldAnimate_truth_table_for_active_target(bool expanded, bool visible, bool reduce, bool battery, bool expected)
    {
        Assert.Equal(expected, LiveBackgroundPlaybackPolicy.ShouldAnimate(expanded, visible, reduce, battery, Both));
    }

    [Fact]
    public void ShouldAnimate_all_combinations_only_one_plays()
    {
        var playing = 0;
        foreach (var expanded in new[] { false, true })
        foreach (var visible in new[] { false, true })
        foreach (var reduce in new[] { false, true })
        foreach (var battery in new[] { false, true })
        {
            if (LiveBackgroundPlaybackPolicy.ShouldAnimate(expanded, visible, reduce, battery, Both))
            {
                playing++;
                Assert.True(expanded && visible && !reduce && !battery);
            }
        }

        Assert.Equal(1, playing);
    }

    [Fact]
    public void ShouldAnimate_target_none_never_animates()
    {
        Assert.False(LiveBackgroundPlaybackPolicy.ShouldAnimate(true, true, false, false, LiveBackgroundTarget.None));
    }

    [Fact]
    public void ShouldAnimate_undefined_target_never_animates()
    {
        Assert.False(LiveBackgroundPlaybackPolicy.ShouldAnimate(true, true, false, false, (LiveBackgroundTarget)99));
    }

    [Theory]
    [InlineData(LiveBackgroundTarget.Notch)]
    [InlineData(LiveBackgroundTarget.MusicCard)]
    [InlineData(LiveBackgroundTarget.Both)]
    public void ShouldAnimate_active_targets_animate_when_expanded(LiveBackgroundTarget target)
    {
        Assert.True(LiveBackgroundPlaybackPolicy.ShouldAnimate(true, true, false, false, target));
    }

    [Theory]
    [InlineData(LiveBackgroundTarget.None, LiveBackgroundSurface.Notch, false)]
    [InlineData(LiveBackgroundTarget.None, LiveBackgroundSurface.MusicCard, false)]
    [InlineData(LiveBackgroundTarget.Notch, LiveBackgroundSurface.Notch, true)]
    [InlineData(LiveBackgroundTarget.Notch, LiveBackgroundSurface.MusicCard, false)]
    [InlineData(LiveBackgroundTarget.MusicCard, LiveBackgroundSurface.Notch, false)]
    [InlineData(LiveBackgroundTarget.MusicCard, LiveBackgroundSurface.MusicCard, true)]
    [InlineData(LiveBackgroundTarget.Both, LiveBackgroundSurface.Notch, true)]
    [InlineData(LiveBackgroundTarget.Both, LiveBackgroundSurface.MusicCard, true)]
    public void AppliesTo_matches_target_and_surface(LiveBackgroundTarget target, LiveBackgroundSurface surface, bool expected)
    {
        Assert.Equal(expected, LiveBackgroundPlaybackPolicy.AppliesTo(target, surface));
    }

    // ---- Üç durumlu karar: Off / StaticFirstFrame / Animate ----

    [Fact]
    public void Decide_collapsed_is_off_so_resources_are_released()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(false, true, false, false, Both, LiveBackgroundSurface.Notch));
    }

    [Fact]
    public void Decide_hidden_window_is_off()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(true, false, false, false, Both, LiveBackgroundSurface.MusicCard));
    }

    [Fact]
    public void Decide_surface_not_targeted_is_off_even_when_expanded()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, LiveBackgroundTarget.Notch, LiveBackgroundSurface.MusicCard));
    }

    [Fact]
    public void Decide_target_none_is_off()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, LiveBackgroundTarget.None, LiveBackgroundSurface.Notch));
    }

    [Fact]
    public void Decide_expanded_animates()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.Animate,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, LiveBackgroundTarget.Notch, LiveBackgroundSurface.Notch));
    }

    [Fact]
    public void Decide_reduce_motion_shows_static_first_frame()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.StaticFirstFrame,
            LiveBackgroundPlaybackPolicy.Decide(true, true, true, false, Both, LiveBackgroundSurface.Notch));
    }

    [Fact]
    public void Decide_battery_saver_shows_static_first_frame()
    {
        Assert.Equal(LiveBackgroundPlaybackMode.StaticFirstFrame,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, true, Both, LiveBackgroundSurface.MusicCard));
    }

    [Fact]
    public void Decide_never_animates_unless_should_animate_agrees()
    {
        foreach (var target in Enum.GetValues<LiveBackgroundTarget>())
        foreach (var surface in Enum.GetValues<LiveBackgroundSurface>())
        foreach (var expanded in new[] { false, true })
        foreach (var visible in new[] { false, true })
        foreach (var reduce in new[] { false, true })
        foreach (var battery in new[] { false, true })
        {
            var mode = LiveBackgroundPlaybackPolicy.Decide(expanded, visible, reduce, battery, target, surface);
            var animate = LiveBackgroundPlaybackPolicy.ShouldAnimate(expanded, visible, reduce, battery, target);
            if (mode == LiveBackgroundPlaybackMode.Animate)
                Assert.True(animate && LiveBackgroundPlaybackPolicy.AppliesTo(target, surface));
            if (!expanded || !visible)
                Assert.Equal(LiveBackgroundPlaybackMode.Off, mode);
        }
    }

    // ---- Yerel dosya doğrulama ----

    [Theory]
    [InlineData(@"C:\Videos\loop.gif")]
    [InlineData(@"C:\Videos\loop.mp4")]
    [InlineData(@"D:\a b\loop.webm")]
    [InlineData(@"C:\Videos\still.webp")]
    [InlineData(@"C:\Videos\LOOP.GIF")]
    public void Validate_accepts_allowed_local_files(string path)
    {
        Assert.Equal(LiveBackgroundFileError.None, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    [Fact]
    public void AllowedExtensions_are_exactly_the_four_formats()
    {
        Assert.Equal(new[] { ".gif", ".mp4", ".webm", ".webp" }, LiveBackgroundFileValidator.AllowedExtensions);
        Assert.Equal("*.gif;*.mp4;*.webm;*.webp", LiveBackgroundFileValidator.DialogPattern);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_rejects_missing_path(string? path)
    {
        Assert.Equal(LiveBackgroundFileError.MissingPath, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    [Theory]
    [InlineData(@"C:\a\x.exe")]
    [InlineData(@"C:\a\x.png")]
    [InlineData(@"C:\a\x.avi")]
    [InlineData(@"C:\a\x.lnk")]
    [InlineData(@"C:\a\noext")]
    [InlineData(@"C:\a\x.gif.")]
    [InlineData(@"C:\dir.gif\file")]
    public void Validate_rejects_unsupported_extensions(string path)
    {
        Assert.Equal(LiveBackgroundFileError.UnsupportedExtension, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    [Theory]
    [InlineData(@"\\server\share\x.gif")]
    [InlineData(@"\\?\C:\x.gif")]
    [InlineData("https://example.com/x.gif")]
    [InlineData("http://example.com/x.mp4")]
    [InlineData("file:///C:/x.gif")]
    [InlineData(@"x.gif")]
    [InlineData(@"..\x.gif")]
    [InlineData(@".\x.gif")]
    [InlineData(@"C:x.gif")]
    [InlineData("C:/Videos/x.gif")]
    public void Validate_rejects_non_local_or_relative_paths(string path)
    {
        Assert.Equal(LiveBackgroundFileError.NotLocalPath, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    [Theory]
    [InlineData(@"C:\a\..\b\x.gif")]
    [InlineData(@"C:\a\x.gif:stream.gif")]
    [InlineData(@"C:\nul\x.gif")]
    [InlineData("C:\\a\\x\n.gif")]
    [InlineData(@"C:\a\*.gif")]
    public void Validate_rejects_unsafe_paths(string path)
    {
        Assert.Equal(LiveBackgroundFileError.InvalidPath, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    [Fact]
    public void Validate_rejects_empty_file()
    {
        Assert.Equal(LiveBackgroundFileError.Empty, LiveBackgroundFileValidator.Validate(@"C:\a\x.gif", 0));
        Assert.Equal(LiveBackgroundFileError.Empty, LiveBackgroundFileValidator.Validate(@"C:\a\x.gif", -1));
    }

    [Fact]
    public void Validate_enforces_size_cap_inclusive()
    {
        Assert.Equal(LiveBackgroundFileError.None,
            LiveBackgroundFileValidator.Validate(@"C:\a\x.mp4", LiveBackgroundLimits.MaxFileSizeBytes));
        Assert.Equal(LiveBackgroundFileError.TooLarge,
            LiveBackgroundFileValidator.Validate(@"C:\a\x.mp4", LiveBackgroundLimits.MaxFileSizeBytes + 1));
    }

    [Fact]
    public void Validate_rejects_too_long_path()
    {
        var path = @"C:\" + new string('a', 300) + ".gif";
        Assert.Equal(LiveBackgroundFileError.InvalidPath, LiveBackgroundFileValidator.Validate(path, 1024));
    }

    // ---- Çözümleyici ----

    [Fact]
    public void Resolve_target_none_returns_none_even_with_valid_file()
    {
        var result = LiveBackgroundResolver.Resolve(LiveBackgroundTarget.None, LiveBackgroundSourceKind.LocalFile, null, @"C:\a\x.gif", 10);
        Assert.Equal(LiveBackgroundSelection.None, result);
    }

    [Fact]
    public void Resolve_ambient_uses_preset_and_falls_back_to_default()
    {
        var known = LiveBackgroundResolver.Resolve(Both, LiveBackgroundSourceKind.BuiltInAmbient, "ocean", null, 0);
        Assert.Equal(LiveBackgroundContentKind.Ambient, known.Kind);
        Assert.Equal("ocean", known.Preset!.Id);

        var unknown = LiveBackgroundResolver.Resolve(Both, LiveBackgroundSourceKind.BuiltInAmbient, "nope", null, 0);
        Assert.Equal(LiveBackgroundPresets.DefaultId, unknown.Preset!.Id);
    }

    [Theory]
    [InlineData(@"C:\a\x.gif", LiveBackgroundContentKind.AnimatedImage)]
    [InlineData(@"C:\a\x.mp4", LiveBackgroundContentKind.Video)]
    [InlineData(@"C:\a\x.webm", LiveBackgroundContentKind.Video)]
    [InlineData(@"C:\a\x.webp", LiveBackgroundContentKind.StillImage)]
    public void Resolve_local_file_maps_extension_to_content_kind(string path, LiveBackgroundContentKind expected)
    {
        var result = LiveBackgroundResolver.Resolve(Both, LiveBackgroundSourceKind.LocalFile, null, path, 1024);
        Assert.Equal(expected, result.Kind);
        Assert.Equal(path, result.FilePath);
        Assert.Equal(LiveBackgroundFileError.None, result.Error);
    }

    [Fact]
    public void Resolve_invalid_local_file_reports_error_and_draws_nothing()
    {
        var result = LiveBackgroundResolver.Resolve(Both, LiveBackgroundSourceKind.LocalFile, null, @"\\server\x.gif", 1024);
        Assert.Equal(LiveBackgroundContentKind.None, result.Kind);
        Assert.Equal(LiveBackgroundFileError.NotLocalPath, result.Error);
        Assert.Null(result.FilePath);
    }

    [Fact]
    public void Resolve_undefined_source_returns_none()
    {
        var result = LiveBackgroundResolver.Resolve(Both, (LiveBackgroundSourceKind)42, null, null, 0);
        Assert.Equal(LiveBackgroundSelection.None, result);
    }

    // ---- Önayar kataloğu ----

    [Fact]
    public void Presets_have_unique_ids_and_valid_dark_colors()
    {
        Assert.NotEmpty(LiveBackgroundPresets.All);
        Assert.Equal(LiveBackgroundPresets.All.Count, LiveBackgroundPresets.All.Select(p => p.Id).Distinct().Count());
        Assert.Contains(LiveBackgroundPresets.All, p => p.Id == LiveBackgroundPresets.DefaultId);

        foreach (var preset in LiveBackgroundPresets.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.NameKey));
            Assert.InRange(preset.CycleSeconds, 8, 60);
            Assert.True(preset.Colors.Count >= 2);
            foreach (var color in preset.Colors)
            {
                Assert.Matches("^#[0-9A-Fa-f]{6}$", color);
                var r = int.Parse(color[1..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var g = int.Parse(color[3..5], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var b = int.Parse(color[5..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                Assert.True(r + g + b < 3 * 128, $"{preset.Id} {color} yeterince koyu değil");
            }
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing")]
    public void Find_unknown_preset_returns_default(string? id)
    {
        Assert.Equal(LiveBackgroundPresets.DefaultId, LiveBackgroundPresets.Find(id).Id);
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        Assert.Equal("ember", LiveBackgroundPresets.Find("EMBER").Id);
    }

    // ---- Sınırlar: çözümleme boyutu, kare planı, perde ----

    [Theory]
    [InlineData(1240, 704, 620, 352)]
    [InlineData(1920, 1080, 620, 349)]
    [InlineData(300, 200, 300, 200)]   // küçük kaynak büyütülmez
    [InlineData(620, 352, 620, 352)]
    public void DecodeSize_clamps_width_to_620_and_keeps_ratio(int w, int h, int ew, int eh)
    {
        Assert.Equal((ew, eh), LiveBackgroundLimits.DecodeSize(w, h));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(-5, -5)]
    public void DecodeSize_invalid_source_is_zero(int w, int h)
    {
        Assert.Equal((0, 0), LiveBackgroundLimits.DecodeSize(w, h));
    }

    [Fact]
    public void DecodeSize_extreme_aspect_never_returns_zero_height()
    {
        Assert.Equal(1, LiveBackgroundLimits.DecodeSize(10000, 1).Height);
    }

    [Fact]
    public void DecodePixelWidth_matches_notch_canvas_width()
    {
        Assert.Equal(620, LiveBackgroundLimits.DecodePixelWidth);
    }

    [Theory]
    [InlineData(480, 270, 60, true)]      // ~31 MB
    [InlineData(1920, 1080, 15, true)]    // ~124 MB
    [InlineData(1920, 1080, 17, false)]   // ~141 MB
    [InlineData(4000, 4000, 1, true)]     // tek büyük kare (64 MB)
    [InlineData(4000, 4000, 3, false)]  // 192 MB
    [InlineData(0, 100, 1, false)]
    [InlineData(100, 0, 1, false)]
    [InlineData(100, 100, 0, false)]
    [InlineData(100, 100, -1, false)]
    public void IsSourceWithinBudget_limits_raw_decoded_size(int w, int h, int frames, bool expected)
    {
        Assert.Equal(expected, LiveBackgroundLimits.IsSourceWithinBudget(w, h, frames));
    }

    [Fact]
    public void IsSourceWithinBudget_does_not_overflow_on_huge_input()
    {
        Assert.False(LiveBackgroundLimits.IsSourceWithinBudget(int.MaxValue, int.MaxValue, int.MaxValue));
    }

    [Fact]
    public void FramePlan_small_gif_keeps_every_frame()
    {
        Assert.Equal(1, GifFramePlan.Stride(30, 620, 352));
        Assert.Equal(30, GifFramePlan.KeptFrames(30, 620, 352));
    }

    [Fact]
    public void FramePlan_caps_by_frame_count_for_tiny_frames()
    {
        // 100x100 kare 40 KB: bayt sınırı gevşek, 120 kare sınırı belirler.
        Assert.Equal(1, GifFramePlan.Stride(120, 100, 100));
        Assert.Equal(2, GifFramePlan.Stride(121, 100, 100));
        Assert.Equal(120, GifFramePlan.KeptFrames(240, 100, 100));
    }

    [Fact]
    public void FramePlan_caps_by_decoded_bytes_for_full_size_frames()
    {
        var stride = GifFramePlan.Stride(120, 620, 352);
        var kept = GifFramePlan.KeptFrames(120, 620, 352);

        Assert.True(stride > 1);
        Assert.True((long)kept * 620 * 352 * 4 <= LiveBackgroundLimits.MaxDecodedBytes);
    }

    [Theory]
    [InlineData(1, 620, 352)]
    [InlineData(500, 620, 352)]
    [InlineData(5000, 100, 100)]
    [InlineData(300, 4000, 4000)]
    public void FramePlan_never_exceeds_caps(int frames, int w, int h)
    {
        var kept = GifFramePlan.KeptFrames(frames, w, h);

        Assert.InRange(kept, 1, Math.Max(1, LiveBackgroundLimits.MaxGifFrames));
        Assert.True(kept == 1 || (long)kept * w * h * 4 <= LiveBackgroundLimits.MaxDecodedBytes);
    }

    [Fact]
    public void FramePlan_invalid_input_is_safe()
    {
        Assert.Equal(1, GifFramePlan.Stride(0, 620, 352));
        Assert.Equal(1, GifFramePlan.Stride(10, 0, 352));
        Assert.Equal(0, GifFramePlan.KeptFrames(0, 620, 352));
    }

    [Fact]
    public void FramePlan_groups_cover_every_frame_once()
    {
        const int frames = 250;
        var stride = GifFramePlan.Stride(frames, 100, 100);
        var ends = Enumerable.Range(0, frames).Count(i => GifFramePlan.IsGroupEnd(i, frames, stride));

        Assert.Equal(GifFramePlan.KeptFrames(frames, 100, 100), ends);
        Assert.True(GifFramePlan.IsGroupEnd(frames - 1, frames, stride));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 100)]
    [InlineData(2, 20)]
    [InlineData(10, 100)]
    [InlineData(25, 250)]
    public void NormalizeDelay_matches_browser_behaviour(int centiseconds, int expectedMs)
    {
        Assert.Equal(expectedMs, GifFramePlan.NormalizeDelayMs(centiseconds));
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(29, 30)]
    [InlineData(65, 65)]
    [InlineData(100, 90)]
    [InlineData(-10, 30)]
    public void Scrim_percent_is_clamped(int input, int expected)
    {
        Assert.Equal(expected, LiveBackgroundLimits.ClampScrimPercent(input));
        Assert.Equal(expected / 100.0, LiveBackgroundLimits.ScrimOpacity(input), 5);
    }

    // ---- Ayar varsayılanları ----

    [Fact]
    public void Defaults_keep_feature_off_for_existing_users()
    {
        var notch = new NotchSettings();

        Assert.Equal(LiveBackgroundTarget.None, notch.LiveBackgroundTarget);
        Assert.Equal(LiveBackgroundSourceKind.BuiltInAmbient, notch.LiveBackgroundSource);
        Assert.Equal(LiveBackgroundPresets.DefaultId, notch.LiveBackgroundPreset);
        Assert.Equal("", notch.LiveBackgroundFilePath);
        Assert.Equal(LiveBackgroundLimits.DefaultScrimPercent, notch.LiveBackgroundScrimPercent);
        Assert.Equal(LiveBackgroundPlaybackMode.Off,
            LiveBackgroundPlaybackPolicy.Decide(true, true, false, false, notch.LiveBackgroundTarget, LiveBackgroundSurface.Notch));
    }
}
