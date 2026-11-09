using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.Core.Dock;
using Koy.Core.Settings;

namespace Koy.App.Features.Dock;

/// <summary>
/// Başlat düğmesinin simgesini <see cref="DockStartIconResolver"/> kararına göre yükler. Özel dosya okunamazsa, bozuksa ya
/// da çok büyükse Koy simgesine düşer; dock hiçbir zaman bu yüzden çökmez ya da boş düğmeyle açılmaz.
/// </summary>
internal static class DockStartIconLoader
{
    // Düğme en çok 18 DIP; %350 DPI'a kadar keskin kalsın diye 64 px yeter (AGENTS #9: gösterilecek boyutta çöz).
    private const int DecodePixels = 64;

    // Bu sınırın üstündeki dosya simge olmaz; bellekte açmadan reddedilir.
    private const long MaxFileBytes = 20 * 1024 * 1024;

    private const string KoyKey = "KoyStartIconImage";
    private const string WindowsKey = "WindowsLogoImage";

    public static ImageSource? Load(AppSettings settings)
    {
        var source = DockStartIconResolver.Resolve(settings);

        if (source.Kind == DockStartIcon.Custom && source.Path is { } path && LoadFile(path) is { } custom)
            return custom;

        return FindResource(source.Kind == DockStartIcon.Windows ? WindowsKey : KoyKey);
    }

    private static ImageSource? FindResource(string key)
        => Application.Current?.TryFindResource(key) as ImageSource;

    /// <summary>Dosyayı gösterim boyutunda çözüp dondurur; hata olursa null (çağıran Koy simgesine döner).</summary>
    internal static ImageSource? LoadFile(string path)
    {
        try
        {
            if (new FileInfo(path).Length is 0 or > MaxFileBytes)
                return null;

            return string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase)
                ? LoadIcon(path)
                : LoadBitmap(path);
        }
        catch (Exception)
        {
            // Bozuk görüntü, erişim hatası, desteklenmeyen biçim: hepsi Koy simgesine düşer.
            return null;
        }
    }

    private static ImageSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        // OnLoad dosyayı hemen bırakır (kullanıcı dosyayı silebilsin); IgnoreImageCache aynı yolda değişen dosyayı yeniden okur.
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.DecodePixelWidth = DecodePixels;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static ImageSource LoadIcon(string path)
    {
        // ICO çok boyutlu: DecodePixelWidth kare seçmez. Hedefe en yakın büyük kare seçilir, gerekirse küçültülür.
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

        var frame = decoder.Frames
            .Where(f => f.PixelWidth >= DecodePixels)
            .OrderBy(f => f.PixelWidth)
            .FirstOrDefault()
            ?? decoder.Frames.OrderByDescending(f => f.PixelWidth).First();

        if (frame.PixelWidth <= DecodePixels * 2)
        {
            frame.Freeze();
            return frame;
        }

        var scale = DecodePixels / (double)frame.PixelWidth;
        var scaled = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        scaled.Freeze();
        return scaled;
    }
}
