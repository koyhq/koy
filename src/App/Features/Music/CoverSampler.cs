using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.Core.Media;

namespace Koy.App.Features.Music;

/// <summary>
/// Kapaktan 32 px genişlikte ikinci bir çözüm yapar: pikseller palet çıkarıcıya gider; Artwork modunda aynı küçük
/// bitmap kart arka planı olur (~4 KB). Arayüz dışı iş parçacığında çalıştırılabilir: sonuçlar Freeze edilmiştir.
/// </summary>
internal static class CoverSampler
{
    public const int SamplePixels = 32;

    public sealed record Result(CoverPalette? Palette, BitmapSource? Small);

    public static Result Sample(byte[] bytes, bool keepBitmap)
    {
        try
        {
            var decoded = new BitmapImage();
            decoded.BeginInit();
            decoded.DecodePixelWidth = SamplePixels;
            decoded.CacheOption = BitmapCacheOption.OnLoad;
            decoded.StreamSource = new MemoryStream(bytes);
            decoded.EndInit();
            decoded.Freeze();

            var bgra = new FormatConvertedBitmap(decoded, PixelFormats.Bgra32, null, 0);
            var width = bgra.PixelWidth;
            var height = bgra.PixelHeight;
            var stride = width * 4;
            var pixels = new byte[stride * height];
            bgra.CopyPixels(pixels, stride, 0);

            var palette = CoverPaletteExtractor.Extract(pixels, width, height, stride);
            if (palette is null || !keepBitmap)
                return new Result(palette, null);

            // Dekoderden bağımsız, dondurulmuş küçük bitmap: kartın arka planı bunu büyütür.
            var small = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            small.Freeze();
            return new Result(palette, small);
        }
        catch
        {
            return new Result(null, null);
        }
    }
}
