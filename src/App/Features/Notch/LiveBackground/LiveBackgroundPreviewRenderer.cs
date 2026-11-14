using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.Core.Notch;
using Koy.Core.Notch.Effects;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>
/// Yerleşik canlı arka plan önayarları (kodlu efektler ve ambiyans renk geçişleri) için 96×54 küçük önizleme
/// görselleri üretir, dondurur (AGENTS #9) ve bellek içi önbellekte tutar.
/// </summary>
public static class LiveBackgroundPreviewRenderer
{
    public const int PreviewWidth = 96;
    public const int PreviewHeight = 54;
    private const float PreviewTimeSeconds = 12f;

    private static readonly ConcurrentDictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Önayar kimliği için dondurulmuş 96×54 önizleme döner; bilinmeyen kimlikte varsayılana düşer.</summary>
    public static BitmapSource Get(string? id)
    {
        var preset = LiveBackgroundPresets.Find(id);
        return Cache.GetOrAdd(preset.Id, static (_, p) => Render(p), preset);
    }

    private static WriteableBitmap Render(LiveBackgroundPreset preset)
    {
        var palette = preset.Colors.Select(EffectColor.Parse).ToArray();
        var pixels = new uint[PreviewWidth * PreviewHeight];

        if (preset.Effect is { } effectId && BackgroundEffects.Find(effectId) is { } effect)
        {
            effect.Render(new EffectFrame(pixels, PreviewWidth, PreviewHeight, PreviewTimeSeconds, palette));
        }
        else
        {
            RenderDiagonalGradient(pixels, PreviewWidth, PreviewHeight, palette);
        }

        var bitmap = new WriteableBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Bgr32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, PreviewWidth, PreviewHeight), pixels, PreviewWidth * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static void RenderDiagonalGradient(uint[] pixels, int width, int height, ReadOnlySpan<EffectColor> palette)
    {
        if (palette.Length == 0)
        {
            Array.Fill(pixels, EffectColor.Black.ToPixel());
            return;
        }

        if (palette.Length == 1)
        {
            Array.Fill(pixels, palette[0].ToPixel());
            return;
        }

        var invX = width > 1 ? 1f / (width - 1) : 0f;
        var invY = height > 1 ? 1f / (height - 1) : 0f;
        var lastIndex = palette.Length - 1;

        for (var y = 0; y < height; y++)
        {
            var ny = y * invY;
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var t = Math.Clamp((x * invX + ny) * 0.5f, 0f, 1f) * lastIndex;
                var idx = Math.Min((int)t, lastIndex - 1);
                var frac = t - idx;
                pixels[row + x] = EffectColor.Mix(palette[idx], palette[idx + 1], frac).ToPixel();
            }
        }
    }
}
