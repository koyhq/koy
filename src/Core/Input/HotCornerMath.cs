namespace Koy.Core.Input;

/// <summary>Bir ekranın sınırları (sol, üst, genişlik, yükseklik). Sağ ve alt kenar dışarıdadır.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

/// <summary>Çok ekranlı masaüstünde bulunan köşe: hangi ekran (listedeki sıra) ve hangi köşe.</summary>
public readonly record struct CornerHit(int ScreenIndex, ScreenCorner Corner);

/// <summary>
/// Köşe tespiti için saf matematik. Fare kancası ve zamanlayıcı Platform'da; burada yalnız koordinat kararı var.
/// Koordinatlar ve köşe boyu aynı birimde olmalı (fiziksel piksel); ekran başına DPI farkı varsa çağıran köşe boyunu
/// o ekranın ölçeğiyle çarpıp verir.
/// </summary>
public static class HotCornerMath
{
    /// <summary>
    /// Nokta ekranın bir köşesinin <paramref name="cornerSize"/> piksel içindeyse o köşeyi döndürür; değilse null.
    /// Ekran dışı noktalar ve geçersiz boyutlar null verir. Köşe boyu ekranın yarısından büyükse
    /// nokta en yakın yatay ve dikey kenara göre seçilir (iki köşe çakışmaz).
    /// </summary>
    public static ScreenCorner? HitTest(int x, int y, ScreenRect screen, int cornerSize)
    {
        if (cornerSize <= 0 || screen.Width <= 0 || screen.Height <= 0)
            return null;

        // Sağ ve alt kenar sınırın dışındadır (exclusive).
        if (x < screen.Left || x >= screen.Right || y < screen.Top || y >= screen.Bottom)
            return null;

        var left = x - screen.Left <= screen.Right - 1 - x;
        var top = y - screen.Top <= screen.Bottom - 1 - y;

        var dx = left ? x - screen.Left : screen.Right - 1 - x;
        var dy = top ? y - screen.Top : screen.Bottom - 1 - y;
        if (dx >= cornerSize || dy >= cornerSize)
            return null;

        return (top, left) switch
        {
            (true, true) => ScreenCorner.TopLeft,
            (true, false) => ScreenCorner.TopRight,
            (false, true) => ScreenCorner.BottomLeft,
            _ => ScreenCorner.BottomRight
        };
    }

    /// <summary>
    /// Çok ekranlı masaüstü için <see cref="HitTest(int, int, ScreenRect, int)"/>. Komşu ekrana geçilebilen kenardaki köşe
    /// gerçek köşe sayılmaz: yan yana iki ekranda ortak kenarın köşeleri imleci durdurmaz, yanlışlıkla tetiklenirdi.
    /// Yalnız masaüstünün dış köşeleri (ya da komşusuz kenarla oluşan köşeler) döner. Negatif koordinatlar desteklenir.
    /// </summary>
    public static CornerHit? HitTest(int x, int y, IReadOnlyList<ScreenRect> screens, int cornerSize)
    {
        for (var i = 0; i < screens.Count; i++)
        {
            if (HitTest(x, y, screens[i], cornerSize) is not { } corner)
                continue;

            var s = screens[i];
            var right = corner is ScreenCorner.TopRight or ScreenCorner.BottomRight;
            var bottom = corner is ScreenCorner.BottomLeft or ScreenCorner.BottomRight;

            // İmleç ekranın dış kenarına gidip bir piksel ötesine geçebiliyorsa o yönde komşu ekran vardır.
            var edgeX = right ? s.Right : s.Left - 1;
            var edgeY = bottom ? s.Bottom : s.Top - 1;
            if (ContainsPoint(screens, i, edgeX, y) || ContainsPoint(screens, i, x, edgeY))
                return null;

            return new CornerHit(i, corner);
        }

        return null;
    }

    private static bool ContainsPoint(IReadOnlyList<ScreenRect> screens, int skip, int x, int y)
    {
        for (var j = 0; j < screens.Count; j++)
        {
            if (j == skip)
                continue;

            var r = screens[j];
            if (r.Width > 0 && r.Height > 0 && x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom)
                return true;
        }

        return false;
    }

    /// <summary>
    /// İmleç köşeye <paramref name="enteredAtMs"/> anında girdiyse, <paramref name="nowMs"/> anında bekleme süresi doldu mu.
    /// Zamanlayıcı kurmaz; yalnız zamanları karşılaştırır.
    /// </summary>
    public static bool IsDwellComplete(long enteredAtMs, long nowMs, int dwellMs)
    {
        if (nowMs < enteredAtMs)
            return false;
        return nowMs - enteredAtMs >= Math.Max(0, dwellMs);
    }
}
