namespace Koy.Core.Notch;

/// <summary>Görselleştirici çubuk sayısı sınırları. Ayar dosyası elle bozulsa bile çizim bu aralıkta kalır.</summary>
public static class NotchVisualizer
{
    public const int MinBarCount = 3;
    public const int MaxBarCount = 16;

    public static int ClampBarCount(int count) => Math.Clamp(count, MinBarCount, MaxBarCount);
}
