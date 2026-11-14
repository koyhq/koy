using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Koy.Core.Tray;

/// <summary>
/// Shell_NotifyIcon'un <c>Shell_TrayWnd</c> penceresine WM_COPYDATA ile gönderdiği SHELLTRAYDATA bloğunu çözer.
/// shell32 çağıranın yapısını her zaman sabit, 32 bit tanıtıcılı tam NOTIFYICONDATA düzenine çevirir;
/// bu yüzden alanlar 32 ve 64 bit uygulamalar için aynı konumdadır.
/// </summary>
public static class TrayMessageParser
{
    /// <summary>WM_COPYDATA'daki dwData değeri: tepsi mesajı.</summary>
    public const int CopyDataTrayMessage = 1;

    /// <summary>SHELLTRAYDATA başındaki sabit imza.</summary>
    public const int Signature = 0x34753423;

    // Konumlar SHELLTRAYDATA başına göredir (imza 4 + mesaj 4 = 8 bayt başlık).
    private const int OffMessage = 4;
    private const int OffHwnd = 12;
    private const int OffId = 16;
    private const int OffFlags = 20;
    private const int OffCallback = 24;
    private const int OffIcon = 28;
    private const int OffTip = 32;
    private const int TipChars = 128;
    private const int OffState = 288;
    private const int OffStateMask = 292;
    private const int OffInfo = 296;
    private const int InfoChars = 256;
    private const int OffVersion = 808;
    private const int OffInfoTitle = 812;
    private const int InfoTitleChars = 64;
    private const int OffInfoFlags = 940;
    private const int OffGuid = 944;
    private const int OffBalloonIcon = 960;

    /// <summary>İpucu alanının sonuna kadar olan en küçük geçerli blok.</summary>
    public const int MinimumLength = OffTip + TipChars * 2;

    /// <summary>
    /// Kabul edilen en büyük blok. Gerçek SHELLTRAYDATA ~1 KB'tır; başka bir süreç Koy'un penceresine
    /// WM_COPYDATA ile keyfi boyutta veri gönderebilir, bu yüzden üst sınır konur.
    /// </summary>
    public const int MaximumLength = 4096;

    /// <summary>
    /// WM_COPYDATA'daki <c>cbData</c> bu çözümleyiciye verilebilir mi? Platform işaretsiz 32 bit değeri
    /// <c>int</c>'e çevirip span kurmadan önce bunu sorar (devasa değer negatif <c>int</c> olup çökertirdi).
    /// </summary>
    public static bool IsAcceptableLength(long byteCount) => byteCount is >= MinimumLength and <= MaximumLength;

    public static bool TryParse(ReadOnlySpan<byte> data, out TrayNotifyData result)
    {
        result = null!;
        if (!IsAcceptableLength(data.Length))
            return false;

        if (BinaryPrimitives.ReadInt32LittleEndian(data) != Signature)
            return false;

        var message = BinaryPrimitives.ReadUInt32LittleEndian(data[OffMessage..]);
        if (message > (uint)TrayMessage.SetVersion)
            return false;

        var fields = (TrayIconFields)BinaryPrimitives.ReadUInt32LittleEndian(data[OffFlags..]);
        var hasInfo = (fields & TrayIconFields.Info) != 0;

        result = new TrayNotifyData(
            Message: (TrayMessage)message,
            OwnerHwnd: ToHandle(BinaryPrimitives.ReadUInt32LittleEndian(data[OffHwnd..])),
            Id: BinaryPrimitives.ReadUInt32LittleEndian(data[OffId..]),
            Fields: fields,
            CallbackMessage: BinaryPrimitives.ReadUInt32LittleEndian(data[OffCallback..]),
            IconHandle: ToHandle(BinaryPrimitives.ReadUInt32LittleEndian(data[OffIcon..])),
            Tooltip: ReadString(data.Slice(OffTip, TipChars * 2)),
            State: ReadUInt32OrZero(data, OffState),
            StateMask: ReadUInt32OrZero(data, OffStateMask),
            Version: ReadUInt32OrZero(data, OffVersion),
            Guid: data.Length >= OffGuid + 16 ? new Guid(data.Slice(OffGuid, 16)) : Guid.Empty,
            Info: hasInfo ? ReadStringOrEmpty(data, OffInfo, InfoChars * 2) : "",
            InfoTitle: hasInfo ? ReadStringOrEmpty(data, OffInfoTitle, InfoTitleChars * 2) : "",
            InfoFlags: hasInfo ? (TrayBalloonFlags)ReadUInt32OrZero(data, OffInfoFlags) : TrayBalloonFlags.None,
            BalloonIconHandle: hasInfo ? ToHandle(ReadUInt32OrZero(data, OffBalloonIcon)) : 0);
        return true;
    }

    // 64 bit Windows'ta tanıtıcılar 32 bite sığar ve işaretli genişletilir (LongToHandle).
    private static nint ToHandle(uint value) => (nint)(int)value;

    private static uint ReadUInt32OrZero(ReadOnlySpan<byte> data, int offset) =>
        data.Length >= offset + 4 ? BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]) : 0;

    private static string ReadStringOrEmpty(ReadOnlySpan<byte> data, int offset, int byteCount) =>
        data.Length >= offset + byteCount ? ReadString(data.Slice(offset, byteCount)) : "";

    private static string ReadString(ReadOnlySpan<byte> utf16)
    {
        var chars = MemoryMarshal.Cast<byte, char>(utf16);
        var end = chars.IndexOf('\0');
        return (end >= 0 ? chars[..end] : chars).ToString();
    }
}
