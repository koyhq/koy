namespace Koy.Core.Input;

/// <summary>Bir ekranın sınırları (sol, üst, genişlik, yükseklik). Sağ ve alt kenar dışarıdadır.</summary>
public readonly record struct ScreenRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

/// <summary>
/// Köşe tespiti için saf matematik. Fare kancası ve zamanlayıcı Platform'da; burada yalnız koordinat kararı var.
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
