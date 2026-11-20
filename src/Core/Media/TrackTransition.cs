using Koy.Core.Animation;

namespace Koy.Core.Media;

/// <summary>
/// Müzik sayfasında parça değişimi geçişi (issue #182): eski kapak sola kayıp solar, yeni kapak sağdan
/// 0,94 ölçekten büyüyerek gelir, başlık aşağıdan girer. Tek bir 0 → 1 ilerleme yayı bütün değerleri sürer,
/// böylece öğeler birbirinden kopmaz; hızlı art arda geçişte yay sıfırdan yeniden başlar.
/// Yalnız dönüşüm ve saydamlık değerleri üretir (shader efekti yok, AGENTS #3 ve #182 kısıtı).
/// </summary>
public sealed class TrackTransition
{
    /// <summary>Gelen kapağın başlangıç ölçeği.</summary>
    public const double IncomingStartScale = 0.94;

    /// <summary>Giden kapağın bittiğindeki ölçeği: geri çekiliyormuş gibi hafifçe küçülür.</summary>
    public const double OutgoingEndScale = 0.96;

    /// <summary>Başlığın aşağıdan girdiği mesafe (DIP).</summary>
    public const double TitleRise = 14;

    /// <summary>Kapağın kaydığı mesafe, kapak genişliğinin oranı. Tam genişlik değil: kırpılmış çerçevede
    /// iki kapak yan yana dizilmiş gibi değil, üst üste kayıp geçmiş gibi görünür.</summary>
    public const double SlideFraction = 0.55;

    // ~0,45 sn'de oturur (kritiğin biraz altı, çok hafif sekme). Süre çarpanı Step'e verilen dt ile uygulanır.
    private readonly Spring _progress = new(1) { Stiffness = 170, Damping = 23, RestThreshold = 0.002 };

    public bool IsRunning => !_progress.IsAtRest;

    /// <summary>0 = geçiş başladı, 1 = bitti.</summary>
    public double Progress => _progress.Value;

    /// <summary>Geçişi baştan başlatır (sürmekte olan geçişin üstüne de).</summary>
    public void Start()
    {
        _progress.SnapTo(0);
        _progress.Target = 1;
    }

    /// <summary>Geçişi anında bitirir (sayfa gizlendi, hareket kapandı).</summary>
    public void Finish() => _progress.SnapTo(1);

    /// <summary>Durumu ilerletir; geçiş sürüyorsa true.</summary>
    public bool Step(double dt) => _progress.Step(dt);

    /// <summary>Şu anki karenin değerleri; <paramref name="coverWidth"/> kayma mesafesini belirler.</summary>
    public TrackTransitionFrame Frame(double coverWidth) => FrameAt(_progress.Value, coverWidth);

    public static TrackTransitionFrame FrameAt(double progress, double coverWidth)
    {
        var p = progress;
        var slide = Math.Max(0, coverWidth) * SlideFraction;

        // Giden kapak ilk yarıda kaybolur; gelen kapak hemen görünmeye başlar ama tam opaklığa geç varır.
        var outgoingOpacity = Clamp01(1 - p / 0.6);
        var incomingOpacity = Clamp01(p / 0.7);

        // Başlık kapaktan bir adım sonra gelir: önce kapak, sonra yazı.
        var titleP = Clamp01((p - 0.12) / 0.88);

        return new TrackTransitionFrame(
            IncomingOffsetX: (1 - p) * slide,
            IncomingScale: IncomingStartScale + (1 - IncomingStartScale) * p,
            IncomingOpacity: incomingOpacity,
            OutgoingOffsetX: -Math.Min(p, 1) * slide,
            OutgoingScale: 1 - (1 - OutgoingEndScale) * Clamp01(p),
            OutgoingOpacity: outgoingOpacity,
            TitleOffsetY: (1 - titleP) * TitleRise,
            TitleOpacity: titleP);
    }

    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
}

/// <summary>Bir karede kapakların ve başlığın dönüşüm ve saydamlık değerleri.</summary>
public readonly record struct TrackTransitionFrame(
    double IncomingOffsetX,
    double IncomingScale,
    double IncomingOpacity,
    double OutgoingOffsetX,
    double OutgoingScale,
    double OutgoingOpacity,
    double TitleOffsetY,
    double TitleOpacity);
