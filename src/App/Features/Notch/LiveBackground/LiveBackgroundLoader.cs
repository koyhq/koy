using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Koy.Core.Notch;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>Çözülmüş kareler: dondurulmuş, çentik genişliğine küçültülmüş bitmap'ler ve her karenin süresi (ms).</summary>
internal sealed record LiveFrames(IReadOnlyList<BitmapSource> Frames, IReadOnlyList<int> DelaysMs);

/// <summary>
/// GIF ve görsel çözücü. WPF'in GIF çözücüsü kareleri parça parça (ofset + atma yöntemi ile) verir; burada tam kareye
/// birleştirilir, 620 piksel genişliğe indirilir ve dondurulur (AGENTS #9). Kare ve bayt sınırları Core'dadır
/// (<see cref="GifFramePlan"/>, <see cref="LiveBackgroundLimits"/>). Çözme kısa ömürlü bir STA iş parçacığında yapılır:
/// büyük GIF arayüzü dondurmaz. Süreç eklenmez, iş parçacığı iş bitince biter.
/// </summary>
internal static class LiveBackgroundLoader
{
    public static Task<LiveFrames?> LoadAsync(string path, LiveBackgroundContentKind kind, bool firstFrameOnly, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<LiveFrames?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                tcs.SetResult(kind == LiveBackgroundContentKind.AnimatedImage
                    ? DecodeGif(path, firstFrameOnly, token)
                    : DecodeStill(path));
            }
            catch (Exception)
            {
                // Bozuk dosya, eksik codec, bellek yetersizliği, iptal: hepsi "arka plan yok" demektir; uygulama çökmez.
                tcs.SetResult(null);
            }
        })
        {
            IsBackground = true,
            Name = "Koy.LiveBackground.Decode",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }

    private static LiveFrames? DecodeStill(string path)
    {
        // Önce boyut: küçük görsel 620'ye büyütülmez.
        int sourceWidth;
        using (var probe = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var decoder = BitmapDecoder.Create(probe, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            sourceWidth = decoder.Frames[0].PixelWidth;
            var sourceHeight = decoder.Frames[0].PixelHeight;
            if (!LiveBackgroundLimits.IsSourceWithinBudget(sourceWidth, sourceHeight, 1))
                return null;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.DecodePixelWidth = Math.Min(sourceWidth, LiveBackgroundLimits.DecodePixelWidth);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        return new LiveFrames([bitmap], [1000]);
    }

    private static LiveFrames? DecodeGif(string path, bool firstFrameOnly, CancellationToken token)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        // OnDemand: kare sayısı ve ölçü bütçe denetiminden geçmeden hiçbir kare çözülmez.
        var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        var total = decoder.Frames.Count;
        if (total == 0)
            return null;

        var (sourceWidth, sourceHeight) = LogicalSize(decoder);
        var count = firstFrameOnly ? 1 : total;
        if (!LiveBackgroundLimits.IsSourceWithinBudget(sourceWidth, sourceHeight, count))
            return null;

        var (width, height) = LiveBackgroundLimits.DecodeSize(sourceWidth, sourceHeight);
        if (width == 0)
            return null;

        var scale = (double)width / sourceWidth;
        var stride = firstFrameOnly ? 1 : GifFramePlan.Stride(count, width, height);
        var frames = new List<BitmapSource>(GifFramePlan.KeptFrames(count, width, height));
        var delays = new List<int>(frames.Capacity);
        BitmapSource? canvas = null;
        var groupDelay = 0;

        for (var i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();

            var frame = decoder.Frames[i];
            var meta = frame.Metadata as BitmapMetadata;
            var rect = new Rect(
                Query(meta, "/imgdesc/Left", 0) * scale,
                Query(meta, "/imgdesc/Top", 0) * scale,
                Query(meta, "/imgdesc/Width", frame.PixelWidth) * scale,
                Query(meta, "/imgdesc/Height", frame.PixelHeight) * scale);
            var disposal = Query(meta, "/grctlext/Disposal", 0);
            groupDelay += GifFramePlan.NormalizeDelayMs(Query(meta, "/grctlext/Delay", 10));

            var composed = Render(canvas, frame, rect, null, width, height);
            // 2: kare alanı arka plana döner; 3: önceki duruma döner; diğerleri: kare kalır.
            canvas = disposal switch
            {
                2 => Render(canvas, null, default, rect, width, height),
                3 => canvas,
                _ => composed,
            };

            if (GifFramePlan.IsGroupEnd(i, count, stride))
            {
                frames.Add(composed);
                delays.Add(groupDelay);
                groupDelay = 0;
            }
        }

        return new LiveFrames(frames, delays);
    }

    /// <summary>Altlık + (varsa) yeni parça tek bitmap'te; <paramref name="clear"/> alanı altlıktan silinir. Sonuç dondurulur.</summary>
    private static RenderTargetBitmap Render(BitmapSource? under, BitmapSource? over, Rect overRect, Rect? clear, int width, int height)
    {
        var full = new Rect(0, 0, width, height);
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (var dc = visual.RenderOpen())
        {
            if (under is not null)
            {
                if (clear is { } hole)
                {
                    dc.PushClip(Geometry.Combine(new RectangleGeometry(full), new RectangleGeometry(hole), GeometryCombineMode.Exclude, null));
                    dc.DrawImage(under, full);
                    dc.Pop();
                }
                else
                {
                    dc.DrawImage(under, full);
                }
            }

            if (over is not null)
                dc.DrawImage(over, overRect);
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static (int Width, int Height) LogicalSize(GifBitmapDecoder decoder)
    {
        var meta = TryGetDecoderMetadata(decoder);
        var width = Query(meta, "/logscrdesc/Width", 0);
        var height = Query(meta, "/logscrdesc/Height", 0);
        if (width > 0 && height > 0)
            return (width, height);

        var first = decoder.Frames[0];
        return (first.PixelWidth, first.PixelHeight);
    }

    private static BitmapMetadata? TryGetDecoderMetadata(GifBitmapDecoder decoder)
    {
        try
        {
            return decoder.Metadata;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>Üst veri sorgusu; yoksa ya da okunamazsa varsayılan.</summary>
    private static int Query(BitmapMetadata? meta, string query, int fallback)
    {
        try
        {
            return meta?.GetQuery(query) is { } value ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : fallback;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or InvalidCastException or FormatException or OverflowException)
        {
            return fallback;
        }
    }
}
