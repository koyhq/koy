namespace Koy.Core.Audio;

/// <summary>
/// Çalan sesten müzik kartındaki çubukların yüksekliğini hesaplar: son <see cref="FftSize"/> örneğe Hann penceresi,
/// FFT, logaritmik aralıklı bantlar (bas solda, tiz sağda), dB ölçeği ve yumuşatma (hızlı yükselir, yavaş iner).
/// Saf hesap: ses yakalama ve zamanlama çağıranın işi. Tek iş parçacığından kullanılır.
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int FftSize = 1024;
    private const double MinFrequency = 50;
    private const double MaxFrequency = 14_000;
    private const double FloorDb = -72;
    private const double CeilingDb = -12;
    private const float Attack = 0.65f;
    private const float Decay = 0.18f;

    // Müzikte enerji frekans arttıkça düşer (oktav başına birkaç dB); düzeltilmezse sağdaki tiz çubuklar hep dipte kalır.
    // Bant ortası 250 Hz'den uzaklaştıkça oktav başına bu kadar dB eklenir (altında aynı oranda düşülür).
    private const double TiltDbPerOctave = 3.5;
    private const double TiltPivotHz = 250;

    private readonly float[] _ring = new float[FftSize];
    private int _ringPos;
    private readonly double[] _window = new double[FftSize];
    private readonly double[] _re = new double[FftSize];
    private readonly double[] _im = new double[FftSize];
    private readonly float[] _levels;
    private int[] _bandEdges = [];
    private double[] _bandTiltDb = [];
    private int _edgesForRate;

    public SpectrumAnalyzer(int bandCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bandCount);

        _levels = new float[bandCount];
        for (var i = 0; i < FftSize; i++)
            _window[i] = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1)));
    }

    public int BandCount => _levels.Length;

    /// <summary>Yumuşatılmış bant düzeyleri, 0..1. <see cref="Update"/> her çağrıldığında yenilenir.</summary>
    public ReadOnlySpan<float> Levels => _levels;

    /// <summary>Bütün çubuklar inmiş mi (çizim gereksiz).</summary>
    public bool IsSilent
    {
        get
        {
            foreach (var level in _levels)
            {
                if (level > 0.01f)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Tek kanala indirilmiş örnekleri (−1..1) halka belleğe ekler; en yeni <see cref="FftSize"/> örnek tutulur.</summary>
    public void AddSamples(ReadOnlySpan<float> mono)
    {
        if (mono.Length >= FftSize)
        {
            mono[^FftSize..].CopyTo(_ring);
            _ringPos = 0;
            return;
        }

        foreach (var sample in mono)
        {
            _ring[_ringPos] = sample;
            _ringPos = (_ringPos + 1) % FftSize;
        }
    }

    /// <summary>Ses gelmediği süre için sessizlik ekler (çubuklar doğal biçimde iner).</summary>
    public void AddSilence(int count)
    {
        count = Math.Min(count, FftSize);
        for (var i = 0; i < count; i++)
        {
            _ring[_ringPos] = 0;
            _ringPos = (_ringPos + 1) % FftSize;
        }
    }

    /// <summary>Halka bellekteki son örneklerden bant düzeylerini hesaplar ve yumuşatır.</summary>
    public void Update(int sampleRate)
    {
        if (sampleRate <= 0)
            return;

        if (_edgesForRate != sampleRate)
        {
            _bandEdges = BandEdges(_levels.Length, sampleRate);
            _bandTiltDb = BandTilt(_bandEdges, sampleRate);
            _edgesForRate = sampleRate;
        }

        for (var i = 0; i < FftSize; i++)
        {
            _re[i] = _ring[(_ringPos + i) % FftSize] * _window[i];
            _im[i] = 0;
        }

        Fft(_re, _im);

        // Hann penceresinin genlik kaybı (0.5) ve tek taraflı spektrum (×2) birlikte: tam ölçekli sinüs ≈ 0 dB.
        const double scale = 4.0 / FftSize;
        for (var band = 0; band < _levels.Length; band++)
        {
            var peak = 0.0;
            for (var bin = _bandEdges[band]; bin < _bandEdges[band + 1]; bin++)
            {
                var magnitude = Math.Sqrt(_re[bin] * _re[bin] + _im[bin] * _im[bin]) * scale;
                if (magnitude > peak)
                    peak = magnitude;
            }

            var db = peak > 0 ? 20 * Math.Log10(peak) + _bandTiltDb[band] : FloorDb;
            var target = (float)Math.Clamp((db - FloorDb) / (CeilingDb - FloorDb), 0, 1);
            var current = _levels[band];
            _levels[band] = target > current
                ? current + (target - current) * Attack
                : current + (target - current) * Decay;
        }
    }

    /// <summary>Bant sınırları (FFT kutusu indeksleri, bandCount + 1 tane): log aralıklı, her bant en az bir kutu.</summary>
    public static int[] BandEdges(int bandCount, int sampleRate)
    {
        var binHz = (double)sampleRate / FftSize;
        var maxBin = FftSize / 2;
        var top = Math.Min(MaxFrequency, sampleRate / 2.0 * 0.95);
        var edges = new int[bandCount + 1];
        var ratio = Math.Pow(top / MinFrequency, 1.0 / bandCount);

        var previous = Math.Max(1, (int)Math.Round(MinFrequency / binHz));
        edges[0] = previous;
        for (var i = 1; i <= bandCount; i++)
        {
            var bin = (int)Math.Round(MinFrequency * Math.Pow(ratio, i) / binHz);
            bin = Math.Clamp(Math.Max(bin, previous + 1), 1, maxBin);
            edges[i] = bin;
            previous = bin;
        }

        return edges;
    }

    /// <summary>Her bandın eğim düzeltmesi (dB): bant ortasının 250 Hz'e göre oktav farkı × oktav başına dB.</summary>
    public static double[] BandTilt(int[] edges, int sampleRate)
    {
        var binHz = (double)sampleRate / FftSize;
        var tilt = new double[edges.Length - 1];
        for (var i = 0; i < tilt.Length; i++)
        {
            var center = Math.Sqrt(edges[i] * (double)Math.Max(edges[i + 1], edges[i] + 1)) * binHz;
            tilt[i] = Math.Log2(center / TiltPivotHz) * TiltDbPerOctave;
        }

        return tilt;
    }

    // Yerinde radix-2 Cooley–Tukey FFT.
    private static void Fft(double[] re, double[] im)
    {
        var n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1)
                j ^= bit;
            j ^= bit;

            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wRe = Math.Cos(angle);
            var wIm = Math.Sin(angle);
            for (var start = 0; start < n; start += length)
            {
                double curRe = 1, curIm = 0;
                for (var k = 0; k < length / 2; k++)
                {
                    var a = start + k;
                    var b = a + length / 2;
                    var tRe = re[b] * curRe - im[b] * curIm;
                    var tIm = re[b] * curIm + im[b] * curRe;
                    re[b] = re[a] - tRe;
                    im[b] = im[a] - tIm;
                    re[a] += tRe;
                    im[a] += tIm;
                    var nextRe = curRe * wRe - curIm * wIm;
                    curIm = curRe * wIm + curIm * wRe;
                    curRe = nextRe;
                }
            }
        }
    }
}
