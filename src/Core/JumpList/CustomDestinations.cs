using System.Buffers.Binary;
using System.Text;

namespace Koy.Core.JumpList;

/// <summary><c>customDestinations-ms</c> içindeki kategori türü.</summary>
public enum CustomDestinationsCategoryKind
{
    /// <summary>Uygulamanın adlandırdığı kategori (tür 0).</summary>
    Custom,
    /// <summary>Bilinen kategori: Windows'un son / sık kullanılanları (tür 1); öğesi yoktur.</summary>
    Known,
    /// <summary>Görevler (tür 2).</summary>
    Tasks,
}

/// <summary>Bilinen kategori kimliği (dosyadaki değer).</summary>
public enum JumpListKnownCategory
{
    Frequent = 1,
    Recent = 2,
}

/// <summary>
/// Dosyadaki bir kategori. <see cref="Items"/> her öğenin ShellLink (MS-SHLLINK) akışıdır; baştaki 16 baytlık CLSID
/// atılmıştır, <c>IPersistStream.Load</c> doğrudan bu baytlarla çağrılır.
/// </summary>
public sealed record CustomDestinationsCategory(
    CustomDestinationsCategoryKind Kind,
    string? Name,
    JumpListKnownCategory? Known,
    IReadOnlyList<byte[]> Items);

/// <summary>Okunmuş dosya. Bozuk ya da bilinmeyen biçim <see cref="Empty"/> olur (kategori yok).</summary>
public sealed class CustomDestinationsFile
{
    /// <summary>Okunamayan (yok, bozuk, bilinmeyen biçim) dosya.</summary>
    public static CustomDestinationsFile Empty { get; } = new([], isValid: false);

    public CustomDestinationsFile(IReadOnlyList<CustomDestinationsCategory> categories, bool isValid = true)
    {
        Categories = categories;
        IsValid = isValid;
    }

    public IReadOnlyList<CustomDestinationsCategory> Categories { get; }

    /// <summary>
    /// Dosya yapısı geçerli okundu. Geçerli ama gösterilecek kategorisi olmayan dosya (yalnız tanımadığımız bilinen
    /// kategori) mümkündür: uygulamanın bir listesi vardır ve son / sık kullanılanları istememiş sayılır.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>Okunmuş gösterilebilir kategori yok.</summary>
    public bool IsEmpty => Categories.Count == 0;

    public bool Declares(JumpListKnownCategory known) =>
        Categories.Any(c => c.Kind == CustomDestinationsCategoryKind.Known && c.Known == known);
}

/// <summary>
/// <c>customDestinations-ms</c> okuyucusu (sürüm 2). Dosya güvenilmeyen ikili veridir: boyut, kategori / öğe sayısı ve
/// dize uzunluğu için üst sınır vardır (<see cref="JumpListLimits"/>); bozuk, kısaltılmış, bilinmeyen sürümlü ya da
/// sınırı aşan girdi istisna fırlatmadan <see cref="CustomDestinationsFile.Empty"/> döner, kısmen okunmuş veri
/// <b>döndürülmez</b> ve hiçbir öğe uydurulmaz.
/// <para>
/// Biçim: üç int32 (sürüm = 2, kategori sayısı, ayrılmış). Her kategori: tür (int32). Tür 0: ad uzunluğu (int16),
/// UTF-16 ad, öğe sayısı (int32), öğeler. Tür 1: bilinen kategori kimliği (int32), öğe yok. Tür 2: öğe sayısı (int32),
/// öğeler. Her kategori 0xBABFFBAB ile biter. Öğe: ShellLink CLSID'i (16 bayt) + ShellLink akışı; akışın uzunluğu yoktur,
/// bu yüzden MS-SHLLINK yapısı (başlık, hedef listesi, bağlantı bilgisi, dizeler, ek bloklar) gezilerek ölçülür.
/// </para>
/// </summary>
public static class CustomDestinationsReader
{
    private const int HeaderBytes = 12;
    private const uint CategoryFooter = 0xBABFFBAB;
    private const int ClsidBytes = 16;
    private const int LinkHeaderBytes = 0x4C;

    // CLSID_ShellLink {00021401-0000-0000-C000-000000000046}, bellekteki (GUID) bayt sırası.
    private static ReadOnlySpan<byte> ShellLinkClsid =>
        [0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    public static CustomDestinationsFile Parse(ReadOnlySpan<byte> data)
    {
        try
        {
            return ParseCore(data) ?? CustomDestinationsFile.Empty;
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or OverflowException)
        {
            // Sınır denetimleri zaten yapılıyor; bu yalnız son güvenlik ağı.
            return CustomDestinationsFile.Empty;
        }
    }

    private static CustomDestinationsFile? ParseCore(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderBytes || data.Length > JumpListLimits.MaxFileBytes)
            return null;

        var pos = 0;
        if (!TryReadInt32(data, ref pos, out var version) || version != 2)
            return null;
        if (!TryReadInt32(data, ref pos, out var categoryCount) || categoryCount < 1 || categoryCount > JumpListLimits.MaxCategories)
            return null;
        pos += 4; // ayrılmış alan

        var categories = new List<CustomDestinationsCategory>(categoryCount);
        var totalItems = 0;

        for (var c = 0; c < categoryCount; c++)
        {
            if (!TryReadInt32(data, ref pos, out var type))
                return null;

            string? name = null;
            JumpListKnownCategory? known = null;
            var kind = CustomDestinationsCategoryKind.Tasks;
            var itemCount = 0;

            switch (type)
            {
                case 0:
                    kind = CustomDestinationsCategoryKind.Custom;
                    if (!TryReadInt16(data, ref pos, out var nameChars) || nameChars < 0 || nameChars > JumpListLimits.MaxCategoryNameChars)
                        return null;
                    var nameBytes = nameChars * 2;
                    if (data.Length - pos < nameBytes)
                        return null;
                    name = Encoding.Unicode.GetString(data.Slice(pos, nameBytes));
                    pos += nameBytes;
                    if (!TryReadInt32(data, ref pos, out itemCount))
                        return null;
                    break;

                case 1:
                    kind = CustomDestinationsCategoryKind.Known;
                    if (!TryReadInt32(data, ref pos, out var knownId))
                        return null;
                    // Tanımadığımız kimlikler (ör. -1) atlanır; yapı yine de geçerlidir.
                    if (knownId is 1 or 2)
                        known = (JumpListKnownCategory)knownId;
                    break;

                case 2:
                    if (!TryReadInt32(data, ref pos, out itemCount))
                        return null;
                    break;

                default:
                    return null; // bilinmeyen kategori türü: yapıyı sürdüremeyiz
            }

            if (itemCount < 0 || itemCount > JumpListLimits.MaxItemsPerCategory)
                return null;
            totalItems += itemCount;
            if (totalItems > JumpListLimits.MaxTotalItems)
                return null;

            var items = new List<byte[]>(itemCount);
            for (var i = 0; i < itemCount; i++)
            {
                if (data.Length - pos < ClsidBytes || !data.Slice(pos, ClsidBytes).SequenceEqual(ShellLinkClsid))
                    return null; // ShellLink olmayan öğeyi çözemeyiz
                pos += ClsidBytes;

                if (!TryMeasureShellLink(data, pos, out var length) || length > JumpListLimits.MaxItemBytes)
                    return null;

                items.Add(data.Slice(pos, length).ToArray());
                pos += length;
            }

            if (!TryReadUInt32(data, ref pos, out var footer) || footer != CategoryFooter)
                return null;

            // Tanınmayan bilinen kategori (known == null) listeye eklenmez: gösterecek bir şey yok.
            if (kind == CustomDestinationsCategoryKind.Known && known is null)
                continue;

            categories.Add(new CustomDestinationsCategory(kind, name, known, items));
        }

        return new CustomDestinationsFile(categories);
    }

    /// <summary>
    /// MS-SHLLINK akışının bayt uzunluğunu ölçer: 0x4C baytlık başlık, (isteğe bağlı) hedef kimlik listesi, bağlantı bilgisi,
    /// sayılı dizeler ve boyutu 4'ten küçük bir bloğa kadar ek bloklar. Her adımda sınır denetimi yapılır.
    /// </summary>
    private static bool TryMeasureShellLink(ReadOnlySpan<byte> data, int start, out int length)
    {
        length = 0;
        var pos = start;

        if (data.Length - pos < LinkHeaderBytes)
            return false;
        if (BinaryPrimitives.ReadUInt32LittleEndian(data[pos..]) != LinkHeaderBytes)
            return false;
        if (!data.Slice(pos + 4, ClsidBytes).SequenceEqual(ShellLinkClsid))
            return false;

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(data[(pos + 0x14)..]);
        pos += LinkHeaderBytes;

        if ((flags & 0x01) != 0) // HasLinkTargetIDList: uint16 boyut + veri
        {
            if (!TryReadUInt16(data, ref pos, out var idListBytes) || data.Length - pos < idListBytes)
                return false;
            pos += idListBytes;
        }

        if ((flags & 0x02) != 0) // HasLinkInfo: uint32 boyut (kendisi dahil)
        {
            if (!TryReadUInt32(data, ref pos, out var infoBytes) || infoBytes < 4)
                return false;
            var skip = (long)infoBytes - 4;
            if (data.Length - pos < skip)
                return false;
            pos += (int)skip;
        }

        var unicode = (flags & 0x80) != 0;
        // Ad, göreli yol, çalışma klasörü, bağımsız değişkenler, simge konumu: her biri uint16 karakter sayısı + karakterler.
        foreach (var bit in (ReadOnlySpan<uint>)[0x04, 0x08, 0x10, 0x20, 0x40])
        {
            if ((flags & bit) == 0)
                continue;
            if (!TryReadUInt16(data, ref pos, out var chars))
                return false;
            var bytes = chars * (unicode ? 2 : 1);
            if (data.Length - pos < bytes)
                return false;
            pos += bytes;
        }

        // Ek bloklar: uint32 boyut; boyutu < 4 olan uç blok akışı bitirir (o 4 bayt akışa dahildir).
        while (true)
        {
            if (!TryReadUInt32(data, ref pos, out var blockSize))
                return false;
            if (blockSize < 4)
                break;
            var skip = (long)blockSize - 4;
            if (data.Length - pos < skip)
                return false;
            pos += (int)skip;
        }

        length = pos - start;
        return true;
    }

    private static bool TryReadInt32(ReadOnlySpan<byte> data, ref int pos, out int value)
    {
        value = 0;
        if (data.Length - pos < 4)
            return false;
        value = BinaryPrimitives.ReadInt32LittleEndian(data[pos..]);
        pos += 4;
        return true;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> data, ref int pos, out uint value)
    {
        value = 0;
        if (data.Length - pos < 4)
            return false;
        value = BinaryPrimitives.ReadUInt32LittleEndian(data[pos..]);
        pos += 4;
        return true;
    }

    private static bool TryReadInt16(ReadOnlySpan<byte> data, ref int pos, out int value)
    {
        value = 0;
        if (data.Length - pos < 2)
            return false;
        value = BinaryPrimitives.ReadInt16LittleEndian(data[pos..]);
        pos += 2;
        return true;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> data, ref int pos, out int value)
    {
        value = 0;
        if (data.Length - pos < 2)
            return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(data[pos..]);
        pos += 2;
        return true;
    }
}
