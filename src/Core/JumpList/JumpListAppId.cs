using System.Text;

namespace Koy.Core.JumpList;

/// <summary>
/// AppID'den atlama listesi dosya adını üretir. Windows, <c>%APPDATA%\Microsoft\Windows\Recent\CustomDestinations</c> ve
/// <c>AutomaticDestinations</c> altında dosya adı olarak AppID'nin büyük harfe çevrilmiş UTF-16LE hâlinin CRC-64'ünü kullanır:
/// polinom 0x92C64265D32139A4 (yansıtılmış), başlangıç değeri tümü 1, son XOR yok.
/// </summary>
public static class JumpListAppId
{
    private const ulong Polynomial = 0x92C64265D32139A4UL;

    private static readonly ulong[] Table = BuildTable();

    /// <summary>
    /// AppID'nin dosya adı karması: küçük harfli onaltılık, <b>başında sıfır doldurma yok</b> (Windows <c>%x</c> kullanır,
    /// diskte 15 haneli adlar var). Boş AppID için null döner (bir dosyaya eşlenemez).
    /// </summary>
    public static string? HashName(string? appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return null;

        return Crc64(Encoding.Unicode.GetBytes(appId.ToUpperInvariant())).ToString("x", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static ulong Crc64(ReadOnlySpan<byte> data)
    {
        var crc = ulong.MaxValue;
        foreach (var b in data)
            crc = Table[(byte)(crc ^ b)] ^ (crc >> 8);
        return crc;
    }

    private static ulong[] BuildTable()
    {
        var table = new ulong[256];
        for (var i = 0; i < 256; i++)
        {
            var c = (ulong)i;
            for (var bit = 0; bit < 8; bit++)
                c = (c & 1) != 0 ? (c >> 1) ^ Polynomial : c >> 1;
            table[i] = c;
        }

        return table;
    }
}
