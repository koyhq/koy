namespace Koy.Core.Notifications;

/// <summary>Çözülmüş simgenin "boş" olup olmadığına karar verir (kart boş daire yerine zil göstersin).</summary>
public static class IconBlankCheck
{
    /// <summary>Piksel başına bu alfa değerinin altı görünmez sayılır.</summary>
    public const byte VisibleAlpha = 8;

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
