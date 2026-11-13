using System.Globalization;
using System.Runtime.CompilerServices;

namespace Koy.Core.Notch.Effects;

/// <summary>Efektlerin kullandığı doğrusal renk (0..1). Core WPF'e bağımlı değildir; App piksele kendisi çevirir.</summary>
public readonly record struct EffectColor(float R, float G, float B)
{
    public static EffectColor Black => default;

    public static EffectColor Parse(string hex)
    {
        var s = hex.AsSpan().TrimStart('#');
        if (s.Length == 8)
            s = s[2..];
        if (s.Length != 6 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return Black;
        return new(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f);
    }

    public static EffectColor operator +(EffectColor a, EffectColor b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static EffectColor operator *(EffectColor a, float k) => new(a.R * k, a.G * k, a.B * k);

    public static EffectColor Mix(EffectColor a, EffectColor b, float t) =>
        new(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

    /// <summary>Bgr32 piksel (0xFFRRGGBB); 1'in üstü kırpılır.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ToPixel()
    {
        static uint C(float v) => v <= 0f ? 0u : v >= 1f ? 255u : (uint)(v * 255f + 0.5f);
        return 0xFF000000u | (C(R) << 16) | (C(G) << 8) | C(B);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EffectColor FromPixel(uint p) =>
        new(((p >> 16) & 0xFF) / 255f, ((p >> 8) & 0xFF) / 255f, (p & 0xFF) / 255f);
}

/// <summary>
/// Efekt matematiği: tamsayı karması, değer gürültüsü, fbm, palet örnekleme, yumuşak nokta. Hepsi saf ve belirlenimci:
/// aynı girdi aynı kareyi verir (testler buna dayanır). Teknikler genel bilinen yöntemlerdir; dış koddan alınmadı.
/// </summary>
public static class EffectMath
{
    public const float Tau = MathF.PI * 2f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7FEB352Du;
        x ^= x >> 15;
        x *= 0x846CA68Bu;
        x ^= x >> 16;
        return x;
    }

    /// <summary>Tohumdan 0..1 sayı (parçacık konumları için).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Rand(int seed, int salt = 0) =>
        (Hash((uint)seed * 0x9E3779B9u + (uint)salt * 0x85EBCA6Bu + 0x165667B1u) & 0xFFFFFF) / 16777216f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lattice(int x, int y) =>
        (Hash((uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u) & 0xFFFF) / 65535f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Smooth(float t) => t * t * (3f - 2f * t);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SmoothStep(float a, float b, float x) => Smooth(Math.Clamp((x - a) / (b - a), 0f, 1f));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Fract(float x) => x - MathF.Floor(x);

    /// <summary>2B değer gürültüsü, 0..1.</summary>
    public static float Noise(float x, float y)
    {
        var xi = (int)MathF.Floor(x);
        var yi = (int)MathF.Floor(y);
        var fx = Smooth(x - xi);
        var fy = Smooth(y - yi);
        var a = Lattice(xi, yi);
        var b = Lattice(xi + 1, yi);
        var c = Lattice(xi, yi + 1);
        var d = Lattice(xi + 1, yi + 1);
        return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
    }

    /// <summary>Kesirli Brown hareketi (katmanlı gürültü), 0..1 civarı.</summary>
    public static float Fbm(float x, float y, int octaves)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (var i = 0; i < octaves; i++)
        {
            sum += Noise(x, y) * amp;
            norm += amp;
            // Döndürerek ölçekle: ızgara izi kalmasın.
            var nx = x * 1.6f + y * 1.2f + 17.3f;
            y = -x * 1.2f + y * 1.6f + 4.1f;
            x = nx;
            amp *= 0.5f;
        }

        return sum / norm;
    }

    /// <summary>Paleti döngüsel ve yumuşak örnekler (t herhangi bir sayı).</summary>
    public static EffectColor Palette(ReadOnlySpan<EffectColor> palette, float t)
    {
        if (palette.Length == 0)
            return EffectColor.Black;
        if (palette.Length == 1)
            return palette[0];
        var f = Fract(t) * palette.Length;
        var i = (int)f;
        if (i >= palette.Length)
            i = palette.Length - 1;
        return EffectColor.Mix(palette[i], palette[(i + 1) % palette.Length], Smooth(f - i));
    }

    /// <summary>Dikey geçişli zemin (parçacık efektleri için).</summary>
    public static void VerticalGradient(in EffectFrame f, EffectColor top, EffectColor bottom)
    {
        for (var y = 0; y < f.Height; y++)
        {
            var p = EffectColor.Mix(top, bottom, f.Height <= 1 ? 0f : y / (float)(f.Height - 1)).ToPixel();
            f.Pixels.Slice(y * f.Width, f.Width).Fill(p);
        }
    }

    /// <summary>Toplamalı yumuşak ışık noktası (kenarı sönümlenen daire).</summary>
    public static void Glow(in EffectFrame f, float cx, float cy, float radius, EffectColor color, float intensity)
    {
        if (radius <= 0f || intensity <= 0f)
            return;
        var x0 = Math.Max(0, (int)(cx - radius));
        var x1 = Math.Min(f.Width - 1, (int)(cx + radius) + 1);
        var y0 = Math.Max(0, (int)(cy - radius));
        var y1 = Math.Min(f.Height - 1, (int)(cy + radius) + 1);
        var inv = 1f / (radius * radius);
        for (var y = y0; y <= y1; y++)
        {
            var row = y * f.Width;
            var dy = y + 0.5f - cy;
            for (var x = x0; x <= x1; x++)
            {
                var dx = x + 0.5f - cx;
                var d = 1f - (dx * dx + dy * dy) * inv;
                if (d <= 0f)
                    continue;
                var k = d * d * intensity;
                f.Pixels[row + x] = (EffectColor.FromPixel(f.Pixels[row + x]) + color * k).ToPixel();
            }
        }
    }

    /// <summary>Toplamalı ince çizgi (yağmur damlası, kayan yıldız). Uçtan uca parlaklık head'e doğru artar.</summary>
    public static void Streak(in EffectFrame f, float x0, float y0, float x1, float y1, EffectColor color, float intensity)
    {
        var steps = (int)MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) + 1;
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var x = (int)(x0 + (x1 - x0) * t);
            var y = (int)(y0 + (y1 - y0) * t);
            if ((uint)x >= (uint)f.Width || (uint)y >= (uint)f.Height)
                continue;
            var idx = y * f.Width + x;
            f.Pixels[idx] = (EffectColor.FromPixel(f.Pixels[idx]) + color * (intensity * t)).ToPixel();
        }
    }
}

/// <summary>Bir kare: küçük çözünürlüklü Bgr32 tampon, zaman (sn) ve önayarın paleti.</summary>
public readonly ref struct EffectFrame
{
    public EffectFrame(Span<uint> pixels, int width, int height, float time, ReadOnlySpan<EffectColor> palette)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height)
            throw new ArgumentException("Tampon boyutu genişlik × yükseklikten küçük.", nameof(pixels));
        Pixels = pixels;
        Width = width;
        Height = height;
        Time = time;
        Palette = palette;
    }

    public Span<uint> Pixels { get; }
    public int Width { get; }
    public int Height { get; }
    public float Time { get; }
    public ReadOnlySpan<EffectColor> Palette { get; }

    /// <summary>Paletin i. rengi (palet kısaysa döner); boş palette siyah.</summary>
    public EffectColor Color(int i) => Palette.Length == 0 ? EffectColor.Black : Palette[i % Palette.Length];
}
