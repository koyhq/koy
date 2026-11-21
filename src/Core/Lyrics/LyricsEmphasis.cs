namespace Koy.Core.Lyrics;

/// <summary>
/// Sözler görünümünde bir satırın vurgusu (issue #182): etkin satırın durduğu çizgiye uzaklığına göre saydamlık ve
/// ölçek. Uzaklık satır yüksekliği cinsindendir ve kesirli olabilir: kaydırma yayı ilerlerken satırlar yumuşakça
/// parlayıp söner, ayrı bir animasyon gerekmez.
/// </summary>
public static class LyricsEmphasis
{
    // Uzaklık 0, 1, 2, 3+ satır için değerler. Tasarımdaki 0,5 / 0,28 / 0,18 açık renkli kapakla boyanan kartta
    // okunmuyordu (Burhan, 2026-11-21): soluk satırlar biraz daha görünür.
    private static readonly double[] Opacities = [1.0, 0.6, 0.4, 0.28];
    private static readonly double[] Scales = [1.0, 0.92, 0.9, 0.9];

    public static double OpacityAt(double distanceInLines) => Lerp(Opacities, distanceInLines);

    public static double ScaleAt(double distanceInLines) => Lerp(Scales, distanceInLines);

    private static double Lerp(double[] table, double distance)
    {
        var d = double.IsFinite(distance) ? Math.Abs(distance) : double.MaxValue;
        var last = table.Length - 1;
        if (d >= last)
            return table[last];

        var i = (int)d;
        var t = d - i;
        return table[i] + (table[i + 1] - table[i]) * t;
    }
}
