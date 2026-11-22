using System.Buffers.Binary;

namespace Koy.Core.Dictation;

/// <summary>Ham PCM'den WAV dosyası ve blok düzeyleri. Saf; kayıt Platform'da, sayılar burada (testli).</summary>
public static class WavAudio
{
    public const int SampleRate = 16000;
    public const int Channels = 1;
    public const int BitsPerSample = 16;

    /// <summary>16 kHz, tek kanal, 16 bit işaretli PCM verisine 44 baytlık RIFF başlığı ekler.</summary>
    public static byte[] FromPcm16(ReadOnlySpan<byte> pcm)
    {
        var bytesPerSecond = SampleRate * Channels * BitsPerSample / 8;
        var wav = new byte[44 + pcm.Length];
        var span = wav.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + pcm.Length);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], bytesPerSecond);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(Channels * BitsPerSample / 8));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], BitsPerSample);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], pcm.Length);
        pcm.CopyTo(span[44..]);
        return wav;
    }

    /// <summary>PCM verisinin süresi (saniye).</summary>
    public static double Seconds(int pcmBytes) => pcmBytes / (double)(SampleRate * Channels * BitsPerSample / 8);

    /// <summary>Bir bloğun (tepe, RMS) düzeyi, 0..1. Tepe dalga formunu, RMS sessizlik denetimini sürer.</summary>
    public static (double Peak, double Rms) Levels(ReadOnlySpan<byte> pcm16)
    {
        var count = pcm16.Length / 2;
        if (count == 0)
            return (0, 0);

        double sumSquares = 0;
        var peak = 0;
        for (var i = 0; i < count; i++)
        {
            int sample = BinaryPrimitives.ReadInt16LittleEndian(pcm16[(i * 2)..]);
            var abs = Math.Abs(sample);
            if (abs > peak)
                peak = abs;
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        return (Math.Min(1, peak / 32768.0), Math.Sqrt(sumSquares / count));
    }

    /// <summary>
    /// Bir WAV dosyasının süresi ve blok başına RMS düzeyleri. Yalnız 16 bit PCM'in düzeyleri çıkarılır; başka biçimde
    /// düzey listesi boş döner (sessizlik denetimi atlanır), süre yine de verilir. Bozuk başlıkta (0, boş).
    /// </summary>
    public static (double Seconds, IReadOnlyList<double> RmsLevels) ChunkLevels(ReadOnlySpan<byte> wav, double chunkSeconds)
    {
        if (wav.Length < 12 || !wav[..4].SequenceEqual("RIFF"u8) || !wav.Slice(8, 4).SequenceEqual("WAVE"u8))
            return (0, []);

        int format = 0, channels = 0, rate = 0, bits = 0;
        var position = 12;
        while (position + 8 <= wav.Length)
        {
            var id = wav.Slice(position, 4);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(wav[(position + 4)..]);
            var body = position + 8;

            if (id.SequenceEqual("fmt "u8) && body + 16 <= wav.Length)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(wav[body..]);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(wav[(body + 2)..]);
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(wav[(body + 4)..]);
                bits = BinaryPrimitives.ReadUInt16LittleEndian(wav[(body + 14)..]);
            }
            else if (id.SequenceEqual("data"u8))
            {
                // Akışa yazılan dosyada boyut 0 ya da 0xFFFFFFFF olabilir: kalan baytların hepsi veridir.
                var available = wav.Length - body;
                var length = size == 0 || size > available ? available : (int)size;
                if (rate <= 0 || channels <= 0 || bits <= 0)
                    return (0, []);

                var seconds = length / (double)(rate * channels * (bits / 8));
                if (format != 1 || bits != 16)
                    return (seconds, []);

                var chunkBytes = Math.Max(2, (int)(rate * chunkSeconds)) * channels * 2;
                var levels = new List<double>();
                for (var offset = 0; offset < length; offset += chunkBytes)
                    levels.Add(Levels(wav.Slice(body + offset, Math.Min(chunkBytes, length - offset))).Rms);

                return (seconds, levels);
            }

            // Parçalar çift hizalıdır.
            position = body + (int)Math.Min(size + (size & 1), int.MaxValue - body);
        }

        return (0, []);
    }
}
