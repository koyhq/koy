using Koy.Core.Appearance;
using Microsoft.Win32;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Koy.Platform.Display;

/// <summary>
/// Masaüstü duvar kağıdının ortalama rengi (okunabilirlik koruması için). Resim 16 px genişliğe küçültülerek çözülür;
/// yalnız ayar uygulanınca ve duvar kağıdı değişince çağrılır, yoklama yapılmaz. Duvar kağıdı resim değilse (düz renk)
/// "Control Panel\Colors\Background" okunur. Okunamazsa null: koruma o zaman seçilen malzemeyi aynen kullanır.
/// Çoklu duvar kağıdı ya da slayt gösterisinde Windows'un son çizdiği tek resim (TranscodedWallpaper) yeterli sayılır.
/// </summary>
public static class WallpaperSampler
{
    private const uint SampleWidth = 16;

    public static async Task<Rgb?> SampleAsync()
    {
        try
        {
            var path = ImagePath();
            if (path is null)
                return SolidColor();

            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var width = decoder.PixelWidth;
            var height = decoder.PixelHeight;
            if (width == 0 || height == 0)
                return null;

            var transform = new BitmapTransform
            {
                ScaledWidth = SampleWidth,
                ScaledHeight = Math.Max(1u, (uint)Math.Round(SampleWidth * (double)height / width)),
                InterpolationMode = BitmapInterpolationMode.Fant
            };
            var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, transform,
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            return ReadabilityGuard.AverageBgra(data.DetachPixelData());
        }
        catch
        {
            return null;
        }
    }

    private static string? ImagePath()
    {
        using var desktop = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        var path = desktop?.GetValue("WallPaper") as string;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return path;

        // Spotlight ve bazı slayt gösterilerinde kayıt boş kalır; Windows'un çizdiği kopya burada durur.
        var transcoded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Themes", "TranscodedWallpaper");
        return File.Exists(transcoded) && !string.IsNullOrWhiteSpace(path) ? transcoded : null;
    }

    private static Rgb? SolidColor()
    {
        using var colors = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors");
        var parts = (colors?.GetValue("Background") as string)?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is { Length: 3 } && byte.TryParse(parts[0], out var r) && byte.TryParse(parts[1], out var g) && byte.TryParse(parts[2], out var b))
            return new Rgb(r, g, b);
        return null;
    }
}
