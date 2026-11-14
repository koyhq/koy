using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.Core.Notch;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>
/// Yerleşik canlı arka plan önayarları (ambiyans renk geçişleri ve video döngüleri) için 192×108 küçük önizleme
/// görselleri üretir, dondurur (AGENTS #9) ve bellek içi önbellekte tutar.
/// </summary>
public static class LiveBackgroundPreviewRenderer
{
    public const int PreviewWidth = 192;
    public const int PreviewHeight = 108;

    private static readonly ConcurrentDictionary<string, BitmapSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Önayar kimliği için dondurulmuş 192×108 önizleme döner; bilinmeyen kimlikte varsayılana düşer.</summary>
    public static BitmapSource Get(string? id)
    {
        var preset = LiveBackgroundPresets.Find(id);
        return Cache.GetOrAdd(preset.Id, static (_, p) => Render(p), preset);
    }

    private static BitmapSource Render(LiveBackgroundPreset preset)
    {
        if (preset.VideoFile is { } videoFile)
        {
            var previewPath = Path.Combine(AppContext.BaseDirectory, "assets", "backgrounds", Path.ChangeExtension(videoFile, ".png"));
            if (File.Exists(previewPath))
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.DecodePixelWidth = PreviewWidth;
                img.UriSource = new Uri(previewPath, UriKind.Absolute);
                img.EndInit();
                img.Freeze();
                return img;
            }
        }

        var palette = preset.Colors.Select(ParseColor).ToArray();
        var pixels = new uint[PreviewWidth * PreviewHeight];
        RenderDiagonalGradient(pixels, PreviewWidth, PreviewHeight, palette);

        var bitmap = new WriteableBitmap(PreviewWidth, PreviewHeight, 96, 96, PixelFormats.Bgr32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, PreviewWidth, PreviewHeight), pixels, PreviewWidth * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static Color ParseColor(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    private static void RenderDiagonalGradient(uint[] pixels, int width, int height, ReadOnlySpan<Color> palette)
    {
        if (palette.Length == 0)
        {
            Array.Fill(pixels, 0xFF000000u);
            return;
        }

        if (palette.Length == 1)
        {
            Array.Fill(pixels, Pack(palette[0]));
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
                var a = palette[idx];
                var b = palette[idx + 1];
                var r = (byte)(a.R + (b.R - a.R) * frac);
                var g = (byte)(a.G + (b.G - a.G) * frac);
                var bl = (byte)(a.B + (b.B - a.B) * frac);
                pixels[row + x] = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | bl;
            }
        }
    }

    private static uint Pack(Color c) => 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
}
