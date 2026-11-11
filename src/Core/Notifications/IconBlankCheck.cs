namespace Koy.Core.Notifications;

/// <summary>Çözülmüş simgenin "boş" olup olmadığına karar verir (kart boş daire yerine zil göstersin).</summary>
public static class IconBlankCheck
{
    /// <summary>Piksel başına bu alfa değerinin altı görünmez sayılır.</summary>
    public const byte VisibleAlpha = 8;

    /// <summary>
    /// BGRA32 resimde görünür piksellerin (alfa &gt; <see cref="VisibleAlpha"/>) sınır kutusu; boşsa null.
    /// Bildirim logoları çoğu zaman saydam kenar boşluğuyla gelir; kart bu kutuya kırpıp daireyi dengeli doldurur.
    /// </summary>
    public static IconBounds? GetVisibleBounds(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            return null;

        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                if (bgra[row + x * 4 + 3] <= VisibleAlpha)
                    continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                maxY = y;
            }
        }

        return maxX < 0 ? null : new IconBounds(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>BGRA32 pikselleri: veri yok ya da hiçbir pikselin alfası görünür değilse boştur.</summary>
    public static bool IsBlank(ReadOnlySpan<byte> bgra)
    {
        for (var i = 3; i < bgra.Length; i += 4)
        {
            if (bgra[i] > VisibleAlpha)
                return false;
        }

        return true;
    }
}

/// <summary>Resim içindeki dikdörtgen (piksel).</summary>
public readonly record struct IconBounds(int X, int Y, int Width, int Height);
