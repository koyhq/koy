namespace Koy.Core.Dock;

/// <summary>
/// Dock öğesine basınca simgenin geri bildirimi. Tek bir ilerleme değeri (0 = bırakılmış, 1 = tam basılı) yayla sürülür;
/// ölçek ve koyulaşma bundan türetilir. Ölçek, fare üstü büyütmesinden AYRI bir çarpandır (ikisi birbirini ezmez).
/// Hareket azaltmada ölçek hiç değişmez, yalnız renk (koyulaşma) değişir.
/// </summary>
public static class DockPress
{
    /// <summary>Tam basılıyken simgenin ölçeği.</summary>
    public const double PressedScale = 0.92;

    /// <summary>Tam basılıyken simgenin opaklığı (dock koyu yüzey olduğundan düşük opaklık koyulaşma verir).</summary>
    public const double PressedOpacity = 0.72;

    /// <summary>Basma çarpanı: 1 (basılı değil) ile <see cref="PressedScale"/> arası; yayın aşımı doğal olarak çevreye taşar.</summary>
    public static double Scale(double progress, bool motionEnabled) =>
        motionEnabled ? 1.0 - (1.0 - PressedScale) * progress : 1.0;

    /// <summary>Simge opaklığı: ilerleme 0..1 aralığına kırpılır (aşım koyulaşmayı geçersiz kılmaz).</summary>
    public static double Opacity(double progress) =>
        1.0 - (1.0 - PressedOpacity) * Math.Clamp(progress, 0.0, 1.0);
}
