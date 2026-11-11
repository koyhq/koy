using Koy.Core.Settings;

namespace Koy.Core.Notch;

/// <summary>
/// Ana sayfa kart düzeni. Kullanıcı tüm kartları gizleyebilir, bu yüzden boş liste geçerlidir; yalnız tekrarlar
/// ve bilinmeyen değerler elenir (elle düzenlenmiş dosyaya karşı).
/// </summary>
public static class HomeCardLayout
{
    public static IReadOnlyList<HomeCard> Default { get; } =
        [HomeCard.Music, HomeCard.HeadphoneBattery, HomeCard.SystemNetwork, HomeCard.QuickLaunch, HomeCard.Weather, HomeCard.Timers];

    /// <summary>Sıra korunur, ilk görülen kazanır. <c>null</c> ise varsayılan düzen döner.</summary>
    public static ValueList<HomeCard> Normalize(IEnumerable<HomeCard>? cards)
    {
        if (cards is null)
            return new ValueList<HomeCard>(Default);

        var seen = new HashSet<HomeCard>();
        var result = new ValueList<HomeCard>();
        foreach (var card in cards)
        {
            if (Enum.IsDefined(card) && seen.Add(card))
                result.Add(card);
        }

        return result;
    }
}
