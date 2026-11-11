namespace Koy.Core.Media;

/// <summary>
/// Kapak baytlarının hafif parmak izi (uzunluk + ilk/son 64 baytın özeti). Aynı kapak için paleti yeniden
/// hesaplamamak içindir; kriptografik değildir, resmin tamamını taramaz.
/// </summary>
public readonly record struct CoverFingerprint(int Length, ulong Hash)
{
    private const int Edge = 64;

    public static CoverFingerprint Of(ReadOnlySpan<byte> bytes)
    {
        // FNV-1a, ilk ve son Edge bayt üstünde.
        var hash = 14695981039346656037UL;
        var head = bytes[..Math.Min(Edge, bytes.Length)];
        var tail = bytes.Length > Edge ? bytes[Math.Max(Edge, bytes.Length - Edge)..] : ReadOnlySpan<byte>.Empty;
        foreach (var b in head)
            hash = (hash ^ b) * 1099511628211UL;
        foreach (var b in tail)
            hash = (hash ^ b) * 1099511628211UL;
        return new CoverFingerprint(bytes.Length, hash);
    }
}
