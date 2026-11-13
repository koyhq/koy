using static Koy.Core.Notch.Effects.EffectMath;

namespace Koy.Core.Notch.Effects;

/// <summary>
/// Kodla çizilen canlı arka plan efekti. Küçük bir tampona (tuval / <see cref="Downscale"/>) yazar; App onu yumuşatarak
/// büyütür. Yazılımla çizimde (AGENTS #3) maliyet piksel sayısıyla orantılı olduğu için tampon küçük, kare hızı düşük tutulur.
/// Efektler durumsuzdur: kare yalnız zamandan ve paletten hesaplanır (aynı t aynı kareyi verir).
/// </summary>
public abstract class BackgroundEffect
{
    /// <summary>Önayar kataloğunda kullanılan kalıcı kimlik.</summary>
    public abstract string Id { get; }

    /// <summary>Tamponun tuvale göre küçültme oranı: yumuşak efektlerde 4, ince ayrıntılı (parçacık, çizgi) efektlerde 2.</summary>
    public virtual int Downscale => 4;

    /// <summary>
    /// Saniyedeki kare sayısı. Ölçümde (UiSnapshots --scene efekt-cpu) maliyetin çoğu efektin hesabı değil, her karede
    /// açık çentiğin yeniden çizilmesiydi: 24 fps tek çekirdeğin ~%28'i, 15 fps ~%17'si. Bu yüzden hepsi 15.
    /// </summary>
    public virtual int FramesPerSecond => 15;

    public abstract void Render(in EffectFrame f);

    /// <summary>Her piksele uygulanan işlevle tamponu doldurur; koordinatlar yüksekliğe göre (v 0..1, u 0..en/boy).</summary>
    protected static void PerPixel(in EffectFrame f, Func<float, float, float, EffectColor> shade)
    {
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            var row = y * f.Width;
            for (var x = 0; x < f.Width; x++)
                f.Pixels[row + x] = shade((x + 0.5f) * inv, v, f.Time).ToPixel();
        }
    }
}

/// <summary>Yerleşik efektlerin listesi. Kimlikler ayar dosyasına yazılır, değiştirilmez.</summary>
public static class BackgroundEffects
{
    public static IReadOnlyList<BackgroundEffect> All { get; } =
    [
        new PlasmaEffect(), new LavaLampEffect(), new NebulaEffect(), new SilkEffect(), new NorthernLightsEffect(),
        new WavesEffect(), new ContourEffect(), new HorizonEffect(), new UnderwaterEffect(), new PearlEffect(),
        new NightSkyEffect(), new FirefliesEffect(), new SnowEffect(), new RainEffect(), new BokehEffect(),
        new WarpEffect(), new RipplesEffect(),
    ];

    public static BackgroundEffect? Find(string? id) =>
        id is null ? null : All.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}

// ------------------------------------------------------------------ yumuşak, piksel başına efektler

/// <summary>Sinüs toplamıyla akan renk alanı.</summary>
internal sealed class PlasmaEffect : BackgroundEffect
{
    public override string Id => "plasma";

    public override void Render(in EffectFrame f)
    {
        Span<EffectColor> pal = stackalloc EffectColor[f.Palette.Length];
        f.Palette.CopyTo(pal);
        var t = f.Time * 0.35f;
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var s = MathF.Sin(u * 3.1f + t) + MathF.Sin(v * 4.3f - t * 1.3f)
                        + MathF.Sin((u + v) * 2.7f + t * 0.7f) + MathF.Sin(MathF.Sqrt(u * u * 4f + v * v * 4f) * 3.3f - t);
                f.Pixels[y * f.Width + x] = (Palette(pal, s * 0.125f + 0.5f) * 0.9f).ToPixel();
            }
        }
    }
}

/// <summary>Birbirine karışan yavaş balonlar (metaball).</summary>
internal sealed class LavaLampEffect : BackgroundEffect
{
    public override string Id => "lava";

    public override void Render(in EffectFrame f)
    {
        const int count = 6;
        Span<float> bx = stackalloc float[count];
        Span<float> by = stackalloc float[count];
        Span<float> br = stackalloc float[count];
        var aspect = f.Width / (float)f.Height;
        for (var i = 0; i < count; i++)
        {
            var sp = 0.05f + Rand(i, 1) * 0.06f;
            bx[i] = aspect * (0.5f + 0.42f * MathF.Sin(f.Time * sp * Tau + Rand(i, 2) * Tau));
            by[i] = 0.5f + 0.55f * MathF.Sin(f.Time * sp * 0.8f * Tau + Rand(i, 3) * Tau);
            br[i] = 0.16f + Rand(i, 4) * 0.12f;
        }

        var bg = f.Color(0) * 0.35f;
        var body = f.Color(1);
        var edge = f.Color(2);
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var field = 0f;
                for (var i = 0; i < count; i++)
                {
                    var dx = u - bx[i];
                    var dy = v - by[i];
                    field += br[i] * br[i] / (dx * dx + dy * dy + 0.0001f);
                }

                var inside = SmoothStep(0.85f, 1.15f, field);
                var rim = SmoothStep(0.4f, 1f, field) * (1f - inside);
                var c = EffectColor.Mix(bg, EffectColor.Mix(body, edge, SmoothStep(1f, 3f, field)), inside) + edge * (rim * 0.35f);
                f.Pixels[y * f.Width + x] = c.ToPixel();
            }
        }
    }
}

/// <summary>Katmanlı gürültüden renkli gaz bulutu.</summary>
internal sealed class NebulaEffect : BackgroundEffect
{
    public override string Id => "nebula";

    public override void Render(in EffectFrame f)
    {
        var a = f.Color(0);
        var b = f.Color(1);
        var c = f.Color(2);
        var t = f.Time * 0.04f;
        PerPixel(f, (u, v, _) =>
        {
            var n1 = Fbm(u * 2.2f + t, v * 2.2f - t * 0.6f, 4);
            var n2 = Fbm(u * 3.1f - t * 0.8f + 5.2f, v * 3.1f + 1.3f, 3);
            var col = EffectColor.Mix(a * 0.25f, b, SmoothStep(0.35f, 0.75f, n1));
            col = EffectColor.Mix(col, c, SmoothStep(0.5f, 0.85f, n2) * 0.8f);
            return col * (0.55f + n1 * 0.6f);
        });
    }
}

/// <summary>Gürültüyle bükülmüş gürültü: ipek gibi akan katmanlar.</summary>
internal sealed class SilkEffect : BackgroundEffect
{
    public override string Id => "silk";

    public override void Render(in EffectFrame f)
    {
        Span<EffectColor> pal = stackalloc EffectColor[f.Palette.Length];
        f.Palette.CopyTo(pal);
        var t = f.Time * 0.06f;
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var qx = Fbm(u * 1.6f + t, v * 1.6f, 3);
                var qy = Fbm(u * 1.6f + 5.2f, v * 1.6f - t, 3);
                var n = Fbm(u * 1.4f + qx * 2.2f, v * 1.4f + qy * 2.2f + t * 0.5f, 3);
                var shine = MathF.Pow(SmoothStep(0.45f, 0.8f, n), 2f);
                var col = Palette(pal, n * 1.2f + t) * (0.45f + shine * 0.7f);
                f.Pixels[y * f.Width + x] = col.ToPixel();
            }
        }
    }
}

/// <summary>Gökyüzünde salınan dikey ışık perdeleri.</summary>
internal sealed class NorthernLightsEffect : BackgroundEffect
{
    public override string Id => "northern";

    public override void Render(in EffectFrame f)
    {
        var sky = f.Color(0);
        var green = f.Color(1);
        var violet = f.Color(2);
        var t = f.Time;
        PerPixel(f, (u, v, _) =>
        {
            var col = EffectColor.Mix(sky * 0.2f, sky * 0.7f, v);
            for (var band = 0; band < 3; band++)
            {
                var center = 0.32f + band * 0.14f + 0.08f * MathF.Sin(u * (1.6f + band * 0.5f) + t * (0.18f + band * 0.05f))
                             + 0.05f * (Noise(u * 3f + band * 7f, t * 0.12f) - 0.5f);
                var d = (v - center) / (0.09f + band * 0.02f);
                var curtain = MathF.Exp(-d * d) * (0.55f + 0.45f * Noise(u * 9f + band * 3f, t * 0.35f + band));
                // Perdenin üstü sönük, altı keskin: ışık aşağı doğru dökülür.
                curtain *= 0.7f + 0.3f * SmoothStep(center - 0.06f, center + 0.02f, v);
                col += EffectColor.Mix(green, violet, band / 2f + (center - v)) * (curtain * 0.8f);
            }

            return col;
        });
    }
}

/// <summary>Üst üste kayan dalga katmanları.</summary>
internal sealed class WavesEffect : BackgroundEffect
{
    public override string Id => "waves";

    public override void Render(in EffectFrame f)
    {
        var top = f.Color(0) * 0.25f;
        var t = f.Time;
        Span<EffectColor> layers = stackalloc EffectColor[3];
        for (var i = 0; i < 3; i++)
            layers[i] = f.Color(i + 1);
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var col = top;
                for (var i = 0; i < 3; i++)
                {
                    var surface = 0.5f + i * 0.13f
                                  - 0.09f * MathF.Sin(u * (3.2f + i * 1.3f) + t * (0.55f + i * 0.25f) + i * 1.7f)
                                  - 0.03f * MathF.Sin(u * 9f - t * 0.9f + i);
                    var cover = SmoothStep(surface - 0.012f, surface + 0.012f, v);
                    col = EffectColor.Mix(col, layers[i] * (0.65f + 0.35f * (1f - (v - surface) * 2f)), cover);
                }

                f.Pixels[y * f.Width + x] = col.ToPixel();
            }
        }
    }
}

/// <summary>Yavaş kayan gürültünün eş yükselti çizgileri (harita konturu).</summary>
internal sealed class ContourEffect : BackgroundEffect
{
    // Yükseklik alanı tek geçişte hesaplanır, eğim komşu piksellerden okunur (piksel başına bir fbm).
    [ThreadStatic] private static float[]? _field;

    public override string Id => "contour";
    public override int Downscale => 3;

    public override void Render(in EffectFrame f)
    {
        const float levels = 9f;
        var bg = f.Color(0) * 0.3f;
        var line = f.Color(1);
        var hi = f.Color(2);
        var t = f.Time * 0.03f;
        var w = f.Width;
        var h = f.Height;
        var field = _field is { } cached && cached.Length >= w * h ? cached : _field = new float[w * h];
        var inv = 1f / h;
        for (var y = 0; y < h; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < w; x++)
                field[y * w + x] = Fbm((x + 0.5f) * inv * 1.8f + t, v * 1.8f - t * 0.7f, 3) * levels;
        }

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = y * w + x;
                var n = field[i];
                var gx = (x + 1 < w ? field[i + 1] : n) - (x > 0 ? field[i - 1] : n);
                var gy = (y + 1 < h ? field[i + w] : n) - (y > 0 ? field[i - w] : n);
                // Merkezi fark iki piksellik: yarısı bir piksellik eğim.
                var slope = MathF.Sqrt(gx * gx + gy * gy) * 0.5f + 0.0001f;
                var toLine = (0.5f - MathF.Abs(Fract(n) - 0.5f)) / slope;
                var stroke = 1f - SmoothStep(0.4f, 1.4f, toLine);
                var major = (int)MathF.Floor(n + 0.5f) % 3 == 0 ? 1f : 0.5f;
                f.Pixels[i] = (bg + EffectColor.Mix(line, hi, n / levels) * (stroke * major * 0.9f)).ToPixel();
            }
        }
    }
}

/// <summary>Batan güneş ve ufka uzanan ışıklı ızgara.</summary>
internal sealed class HorizonEffect : BackgroundEffect
{
    public override string Id => "horizon";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        var skyTop = f.Color(0) * 0.35f;
        var skyLow = f.Color(1);
        var sunA = f.Color(2);
        var sunB = f.Color(3);
        var grid = f.Color(4);
        var aspect = f.Width / (float)f.Height;
        const float horizon = 0.62f;
        var scroll = Fract(f.Time * 0.35f);
        PerPixel(f, (u, v, _) =>
        {
            if (v < horizon)
            {
                var sky = EffectColor.Mix(skyTop, skyLow * 0.8f, v / horizon);
                var dx = u - aspect * 0.5f;
                var dy = v - horizon * 0.82f;
                var r = MathF.Sqrt(dx * dx + dy * dy);
                if (r < 0.26f)
                {
                    // Güneşin alt yarısı yatay şeritlerle kesilir; şeritler aşağı doğru kalınlaşır.
                    var gap = v > horizon * 0.62f && Fract((v - horizon * 0.62f) * 26f) < (v - horizon * 0.62f) * 4.5f;
                    if (!gap)
                        return EffectColor.Mix(sunA, sunB, SmoothStep(horizon * 0.4f, horizon * 0.95f, v));
                }

                return sky + sunA * (MathF.Max(0f, 0.45f - r) * 0.9f);
            }

            var depth = (v - horizon) / (1f - horizon);
            var z = 1f / (depth + 0.04f);
            // Izgara aralığı derinlikle ölçeklenir; çizgi kalınlığı ekranda yaklaşık sabit kalsın diye z ile çarpılır.
            var gx = (u - aspect * 0.5f) * z * 3.2f;
            var gz = z * 0.9f + scroll;
            var lineX = 1f - SmoothStep(0f, 0.012f * z * 3.2f, 0.5f - MathF.Abs(Fract(gx) - 0.5f));
            var lineZ = 1f - SmoothStep(0f, 0.01f * z * z * 0.9f, 0.5f - MathF.Abs(Fract(gz) - 0.5f));
            var floor = skyTop * 0.6f;
            var fade = SmoothStep(0f, 0.25f, depth);
            return floor + grid * (MathF.Max(lineX, lineZ) * fade) + skyLow * ((1f - fade) * 0.25f);
        });
    }
}

/// <summary>Su altından görülen ışık kırılması (kostik ağ).</summary>
internal sealed class UnderwaterEffect : BackgroundEffect
{
    public override string Id => "underwater";
    public override int Downscale => 3;

    public override void Render(in EffectFrame f)
    {
        var deep = f.Color(0);
        var mid = f.Color(1);
        var light = f.Color(2);
        var t = f.Time * 0.12f;
        PerPixel(f, (u, v, _) =>
        {
            // İki kayan gürültünün sırtları kesişince ince parlak ağ oluşur.
            var r1 = 1f - MathF.Abs(Noise(u * 4.5f + t, v * 4.5f - t * 0.6f) * 2f - 1f);
            var r2 = 1f - MathF.Abs(Noise(u * 3.7f - t * 0.8f + 9.1f, v * 3.7f + t * 0.5f) * 2f - 1f);
            var c = MathF.Pow(r1 * r2, 3.5f);
            var water = EffectColor.Mix(mid, deep, v);
            return water * 0.8f + light * (c * 0.9f * (1f - v * 0.5f));
        });
    }
}

/// <summary>Sedef gibi kayan renk şeritleri.</summary>
internal sealed class PearlEffect : BackgroundEffect
{
    public override string Id => "pearl";

    public override void Render(in EffectFrame f)
    {
        Span<EffectColor> pal = stackalloc EffectColor[f.Palette.Length];
        f.Palette.CopyTo(pal);
        var t = f.Time * 0.05f;
        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var n = Noise(u * 1.5f + t * 3f, v * 1.5f);
                var band = (u * 0.55f + v * 0.35f) + n * 0.5f + t;
                var sheen = 0.6f + 0.4f * MathF.Sin(band * Tau * 1.5f);
                f.Pixels[y * f.Width + x] = (Palette(pal, band) * sheen).ToPixel();
            }
        }
    }
}

// ------------------------------------------------------------------ parçacık efektleri

/// <summary>Göz kırpan yıldızlar ve ara sıra kayan yıldız.</summary>
internal sealed class NightSkyEffect : BackgroundEffect
{
    public override string Id => "nightsky";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.25f, f.Color(1) * 0.6f);
        var star = f.Color(2);
        var count = f.Width * f.Height / 400;
        for (var i = 0; i < count; i++)
        {
            var twinkle = 0.35f + 0.65f * MathF.Abs(MathF.Sin(f.Time * (0.4f + Rand(i, 3)) + Rand(i, 4) * Tau));
            var size = 0.6f + Rand(i, 5) * 1.2f;
            Glow(f, Rand(i, 1) * f.Width, Rand(i, 2) * f.Height * 0.9f, size + 0.8f, star, twinkle);
        }

        // Her 7 saniyede bir kayan yıldız; konumu döngü sayısından gelir (durumsuz).
        const float period = 7f;
        var cycle = (int)MathF.Floor(f.Time / period);
        var p = (f.Time - cycle * period) / 1.2f;
        if (p < 1f)
        {
            var sx = (0.15f + Rand(cycle, 7) * 0.5f) * f.Width;
            var sy = (0.05f + Rand(cycle, 8) * 0.3f) * f.Height;
            var hx = sx + p * f.Width * 0.35f;
            var hy = sy + p * f.Height * 0.25f;
            Streak(f, hx - f.Width * 0.12f, hy - f.Height * 0.085f, hx, hy, star, 0.9f * (1f - p));
        }
    }
}

/// <summary>Süzülüp yanıp sönen ışık noktaları.</summary>
internal sealed class FirefliesEffect : BackgroundEffect
{
    public override string Id => "fireflies";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.2f, f.Color(1) * 0.45f);
        var glow = f.Color(2);
        for (var i = 0; i < 26; i++)
        {
            var sp = 0.04f + Rand(i, 3) * 0.05f;
            var x = Fract(Rand(i, 1) + MathF.Sin(f.Time * sp * Tau + Rand(i, 4) * Tau) * 0.08f + f.Time * sp * 0.15f) * f.Width;
            var y = (Rand(i, 2) * 0.85f + 0.1f + MathF.Cos(f.Time * sp * 1.3f * Tau + Rand(i, 5) * Tau) * 0.06f) * f.Height;
            var pulse = MathF.Max(0f, MathF.Sin(f.Time * (0.5f + Rand(i, 6)) + Rand(i, 7) * Tau));
            Glow(f, x, y, f.Height * (0.045f + pulse * 0.05f), glow, 0.25f + pulse * 0.75f);
        }
    }
}

/// <summary>Yavaşça düşen, sağa sola salınan kar taneleri.</summary>
internal sealed class SnowEffect : BackgroundEffect
{
    public override string Id => "snow";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.3f, f.Color(1) * 0.6f);
        var flake = f.Color(2);
        for (var i = 0; i < 70; i++)
        {
            var near = Rand(i, 3);
            var speed = 0.05f + near * 0.09f;
            var y = Fract(Rand(i, 2) + f.Time * speed) * (f.Height + 8) - 4;
            var x = Fract(Rand(i, 1) + MathF.Sin(f.Time * (0.3f + near * 0.4f) + i) * 0.02f) * f.Width;
            Glow(f, x, y, 1f + near * 2.2f, flake, 0.45f + near * 0.5f);
        }
    }
}

/// <summary>Eğik düşen yağmur çizgileri ve hafif sis.</summary>
internal sealed class RainEffect : BackgroundEffect
{
    public override string Id => "rain";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.35f, f.Color(1) * 0.5f);
        var drop = f.Color(2);
        var len = f.Height * 0.09f;
        for (var i = 0; i < 90; i++)
        {
            var near = Rand(i, 3);
            var speed = 0.9f + near * 0.9f;
            var y = Fract(Rand(i, 2) + f.Time * speed) * (f.Height + len * 2) - len;
            var x = Fract(Rand(i, 1) - f.Time * speed * 0.12f) * (f.Width + len) - len * 0.3f;
            var l = len * (0.5f + near);
            Streak(f, x - l * 0.25f, y - l, x, y, drop, 0.18f + near * 0.35f);
        }
    }
}

/// <summary>Odak dışı büyük, yumuşak ışık daireleri.</summary>
internal sealed class BokehEffect : BackgroundEffect
{
    public override string Id => "bokeh";

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.6f, f.Color(1) * 0.8f);
        for (var i = 0; i < 16; i++)
        {
            var sp = 0.01f + Rand(i, 3) * 0.02f;
            var x = Fract(Rand(i, 1) + f.Time * sp) * (f.Width * 1.3f) - f.Width * 0.15f;
            var y = (Rand(i, 2) + MathF.Sin(f.Time * sp * 6f + i) * 0.05f) * f.Height;
            var r = f.Height * (0.12f + Rand(i, 4) * 0.22f);
            var c = f.Color(2 + i % Math.Max(1, f.Palette.Length - 2));
            Glow(f, x, y, r, c, 0.45f + 0.2f * MathF.Sin(f.Time * 0.4f + i));
        }
    }
}

/// <summary>Merkezden dışa akan yıldızlar (ışık hızı).</summary>
internal sealed class WarpEffect : BackgroundEffect
{
    public override string Id => "warp";
    public override int Downscale => 2;

    public override void Render(in EffectFrame f)
    {
        VerticalGradient(f, f.Color(0) * 0.25f, f.Color(0) * 0.25f);
        Glow(f, f.Width * 0.5f, f.Height * 0.5f, f.Height * 0.9f, f.Color(1), 0.35f);
        var star = f.Color(2);
        var cx = f.Width * 0.5f;
        var cy = f.Height * 0.5f;
        var reach = MathF.Sqrt(cx * cx + cy * cy);
        for (var i = 0; i < 80; i++)
        {
            var angle = Rand(i, 1) * Tau;
            var z = Fract(Rand(i, 2) + f.Time * (0.12f + Rand(i, 3) * 0.1f));
            // Uzaklık üstel artar: merkezde yavaş, kenarda hızlı.
            var r0 = reach * z * z;
            var r1 = reach * MathF.Min(1f, (z + 0.06f) * (z + 0.06f));
            var dx = MathF.Cos(angle);
            var dy = MathF.Sin(angle);
            Streak(f, cx + dx * r0, cy + dy * r0, cx + dx * r1, cy + dy * r1, star, 0.25f + z * 0.75f);
        }
    }
}

/// <summary>Rastgele noktalardan yayılan, sönen halkalar (su damlası).</summary>
internal sealed class RipplesEffect : BackgroundEffect
{
    public override string Id => "ripples";
    public override int Downscale => 3;

    public override void Render(in EffectFrame f)
    {
        var bg = f.Color(0) * 0.35f;
        var ring = f.Color(1);
        var hi = f.Color(2);
        var aspect = f.Width / (float)f.Height;
        const int drops = 4;
        const float life = 6f;
        Span<float> dx = stackalloc float[drops];
        Span<float> dy = stackalloc float[drops];
        Span<float> age = stackalloc float[drops];
        for (var i = 0; i < drops; i++)
        {
            // Her damla kendi döngüsünde yeni bir yere düşer.
            var tt = f.Time + i * life / drops;
            var cycle = (int)MathF.Floor(tt / life);
            age[i] = (tt - cycle * life) / life;
            dx[i] = (0.1f + Rand(cycle * drops + i, 1) * 0.8f) * aspect;
            dy[i] = 0.1f + Rand(cycle * drops + i, 2) * 0.8f;
        }

        var inv = 1f / f.Height;
        for (var y = 0; y < f.Height; y++)
        {
            var v = (y + 0.5f) * inv;
            for (var x = 0; x < f.Width; x++)
            {
                var u = (x + 0.5f) * inv;
                var col = bg;
                for (var i = 0; i < drops; i++)
                {
                    var ddx = u - dx[i];
                    var ddy = v - dy[i];
                    var d = MathF.Sqrt(ddx * ddx + ddy * ddy);
                    var front = age[i] * 0.9f;
                    var w = d - front;
                    if (w > 0.02f || w < -0.35f)
                        continue;
                    var wave = MathF.Cos(w * 60f) * MathF.Exp(w * 9f) * (1f - age[i]);
                    if (wave > 0f)
                        col += EffectColor.Mix(ring, hi, wave) * (wave * 0.7f);
                }

                f.Pixels[y * f.Width + x] = col.ToPixel();
            }
        }
    }
}
