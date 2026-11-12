using System.Text;

namespace Koy.Core.Tests.JumpList;

/// <summary>
/// Testler için tamamen sentetik <c>customDestinations-ms</c> üretir. Gerçek kullanıcı dosyası hiçbir testte kullanılmaz.
/// ShellLink akışı yalnız yapıyı taşır (başlık, isteğe bağlı kimlik listesi / bağlantı bilgisi, dizeler, uç blok);
/// içeriği anlamlı bir kısayol değildir.
/// </summary>
internal sealed class CustomDestinationsBuilder : IDisposable
{
    public void Dispose() => _body.Dispose();
    public static readonly byte[] ShellLinkClsid =
        [0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    public const uint Footer = 0xBABFFBAB;

    private readonly MemoryStream _body = new();
    private int _categories;

    public CustomDestinationsBuilder Custom(string name, params byte[][] items)
    {
        WriteInt(0);
        var nameBytes = Encoding.Unicode.GetBytes(name);
        WriteShort((short)name.Length);
        _body.Write(nameBytes);
        WriteItems(items);
        return this;
    }

    public CustomDestinationsBuilder Known(int id)
    {
        WriteInt(1);
        WriteInt(id);
        WriteUInt(Footer);
        _categories++;
        return this;
    }

    public CustomDestinationsBuilder Tasks(params byte[][] items)
    {
        WriteInt(2);
        WriteItems(items);
        return this;
    }

    private void WriteItems(byte[][] items)
    {
        WriteInt(items.Length);
        foreach (var item in items)
        {
            _body.Write(ShellLinkClsid);
            _body.Write(item);
        }

        WriteUInt(Footer);
        _categories++;
    }

    public byte[] Build(int version = 2, int? categoryCount = null)
    {
        using var ms = new MemoryStream();
        ms.Write(BitConverter.GetBytes(version));
        ms.Write(BitConverter.GetBytes(categoryCount ?? _categories));
        ms.Write(BitConverter.GetBytes(0));
        ms.Write(_body.ToArray());
        return ms.ToArray();
    }

    /// <summary>MS-SHLLINK biçiminde sentetik akış. <paramref name="stringFlags"/>: 0x04 ad, 0x08 göreli yol, 0x10 klasör, 0x20 bağımsız değişken, 0x40 simge.</summary>
    public static byte[] Link(
        string[]? strings = null, uint stringFlags = 0x20, bool unicode = true, int idListBytes = -1, int linkInfoBytes = -1, int extraBlocks = 0)
    {
        using var ms = new MemoryStream();
        var flags = stringFlags | (unicode ? 0x80u : 0u);
        if (idListBytes >= 0) flags |= 0x01;
        if (linkInfoBytes >= 0) flags |= 0x02;

        var header = new byte[0x4C];
        BitConverter.GetBytes(0x4Cu).CopyTo(header, 0);
        ShellLinkClsid.CopyTo(header, 4);
        BitConverter.GetBytes(flags).CopyTo(header, 0x14);
        ms.Write(header);

        if (idListBytes >= 0)
        {
            ms.Write(BitConverter.GetBytes((ushort)idListBytes));
            ms.Write(new byte[idListBytes]);
        }

        if (linkInfoBytes >= 0)
        {
            var size = linkInfoBytes + 4;
            ms.Write(BitConverter.GetBytes((uint)size));
            ms.Write(new byte[linkInfoBytes]);
        }

        var values = strings ?? ["--yeni-pencere"];
        var index = 0;
        foreach (var bit in new uint[] { 0x04, 0x08, 0x10, 0x20, 0x40 })
        {
            if ((stringFlags & bit) == 0)
                continue;
            var text = index < values.Length ? values[index] : string.Empty;
            index++;
            ms.Write(BitConverter.GetBytes((ushort)text.Length));
            ms.Write(unicode ? Encoding.Unicode.GetBytes(text) : Encoding.ASCII.GetBytes(text));
        }

        for (var i = 0; i < extraBlocks; i++)
        {
            ms.Write(BitConverter.GetBytes(12u)); // boyut, imza, 4 bayt veri
            ms.Write(BitConverter.GetBytes(0xA0000001u));
            ms.Write(new byte[4]);
        }

        ms.Write(BitConverter.GetBytes(0u)); // uç blok
        return ms.ToArray();
    }

    private void WriteInt(int value) => _body.Write(BitConverter.GetBytes(value));
    private void WriteShort(short value) => _body.Write(BitConverter.GetBytes(value));
    private void WriteUInt(uint value) => _body.Write(BitConverter.GetBytes(value));
}
