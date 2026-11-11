namespace Koy.Core.Appearance;

/// <summary>
/// Okunabilirlik koruması: duvar kağıdı açık renkliyse saydam yüzeyde ikincil metin 4,5:1 altına inmesin diye seçili
/// malzeme kendiliğinden daha opak seviyeye çıkar (Cam → Buzlu → Katı). Hesap onaylı demodakiyle aynıdır:
/// görünen yüzey = taban rengi, opaklığıyla duvar kağıdı ortalamasının üstünde; ikincil metin = metin rengi, alfasıyla
/// görünen yüzeyin üstünde. Bulanıklık olmadığından duvar kağıdının ortalaması yeterli bir tahmindir.
/// </summary>
public static class ReadabilityGuard
{
    /// <summary>WCAG AA normal metin eşiği.</summary>
    public const double MinSecondaryContrast = 4.5;

    /// <summary>
    /// Bir yüzeyin taban rengi, birincil metin rengi, ikincil metnin alfası ve bulanıklıksız (katmanlı) olup olmadığı.
    /// Bulanıklıksız yüzey <see cref="MaterialSetting.LayeredOpacity"/>, diğerleri <see cref="MaterialSetting.SurfaceOpacity"/> kullanır.
    /// </summary>
    public readonly record struct Surface(Rgb Base, Rgb Text, double SecondaryAlpha, bool Layered = false)
    {
        public double OpacityOf(MaterialLevel level) =>
            Layered ? MaterialSetting.LayeredOpacity(level) : MaterialSetting.SurfaceOpacity(level);
    }

    /// <summary>Çentik ve dock: #0A0A0B taban, %62 beyaz ikincil metin, bulanıklıksız.</summary>
    public static Surface Notch { get; } = new(new Rgb(10, 10, 11), new Rgb(244, 244, 245), 0.62, Layered: true);

    /// <summary>Koy penceresi, koyu tema.</summary>
    public static Surface WindowDark { get; } = new(new Rgb(22, 22, 25), new Rgb(244, 244, 245), 0.60);

    /// <summary>Koy penceresi, açık tema.</summary>
    public static Surface WindowLight { get; } = new(new Rgb(243, 243, 245), new Rgb(24, 24, 27), 0.62);

    /// <summary>Verilen opaklıkta ikincil metnin kontrastı.</summary>
    public static double SecondaryContrast(double opacity, Rgb wallpaper, Surface surface)
    {
        var shown = ColorMath.Mix(wallpaper, surface.Base, opacity);
        var secondary = ColorMath.Mix(shown, surface.Text, surface.SecondaryAlpha);
        return ColorMath.Contrast(secondary, shown);
    }

    /// <summary>
    /// Etkin malzeme. Koruma kapalıysa ya da duvar kağıdı bilinmiyorsa (okunamadı) seçilen değer aynen döner.
    /// Aksi halde seçilenden başlayıp opağa doğru eşiği geçen ilk seviye; hiçbiri geçmezse Katı. Opaklık hiç düşmez.
    /// </summary>
    public static MaterialLevel Effective(MaterialLevel level, bool enabled, Rgb? wallpaper, Surface surface)
    {
        if (!enabled || wallpaper is not { } wall || level == MaterialLevel.Solid)
            return level;

        var levels = MaterialSetting.Levels;
        var start = 0;
        while (start < levels.Count && levels[start] != level)
            start++;

        for (var i = start; i < levels.Count; i++)
        {
            var candidate = levels[i];
            if (SecondaryContrast(surface.OpacityOf(candidate), wall, surface) >= MinSecondaryContrast)
                return candidate;
        }

        return MaterialLevel.Solid;
    }

    /// <summary>BGRA piksel dizisinin (WinRT çözücü çıktısı) ortalama rengi; alfa yok sayılır. Tam piksel yoksa null.</summary>
    public static Rgb? AverageBgra(ReadOnlySpan<byte> pixels)
    {
        var count = pixels.Length / 4;
        if (count == 0)
            return null;

        long b = 0, g = 0, r = 0;
        for (var i = 0; i < count * 4; i += 4)
        {
            b += pixels[i];
            g += pixels[i + 1];
            r += pixels[i + 2];
        }

        return new Rgb((byte)(r / count), (byte)(g / count), (byte)(b / count));
    }
}
