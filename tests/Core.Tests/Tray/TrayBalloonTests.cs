using System.Buffers.Binary;
using System.Text;
using Koy.Core.Tray;

namespace Koy.Core.Tests.Tray;

public class TrayBalloonTests
{
    private static byte[] BuildBalloonData(
        uint message = (uint)TrayMessage.Modify,
        uint hwnd = 0x1234,
        uint id = 7,
        TrayIconFields flags = TrayIconFields.Info,
        string info = "",
        string infoTitle = "",
        TrayBalloonFlags infoFlags = TrayBalloonFlags.None,
        uint timeoutOrVersion = 0,
        uint balloonIcon = 0,
        Guid guid = default,
        int length = 964)
    {
        var full = new byte[964];
        BinaryPrimitives.WriteInt32LittleEndian(full, TrayMessageParser.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(4), message);
        BinaryPrimitives.WriteInt32LittleEndian(full.AsSpan(8), 956);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(12), hwnd);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(16), id);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(20), (uint)flags);
        Encoding.Unicode.GetBytes(info).CopyTo(full, 296);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(808), timeoutOrVersion);
        Encoding.Unicode.GetBytes(infoTitle).CopyTo(full, 812);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(940), (uint)infoFlags);
        guid.TryWriteBytes(full.AsSpan(944));
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(960), balloonIcon);

        var data = new byte[length];
        Array.Copy(full, data, Math.Min(length, full.Length));
        return data;
    }

    [Fact]
    public void TryParse_ReadsBalloonFieldsWhenInfoFlagSet()
    {
        var bytes = BuildBalloonData(
            flags: TrayIconFields.Info | TrayIconFields.Tip,
            info: "Yedekleme tamamlandı",
            infoTitle: "Bulut Sürücü",
            infoFlags: TrayBalloonFlags.User | TrayBalloonFlags.NoSound,
            timeoutOrVersion: 15000,
            balloonIcon: 0xBEEF);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal("Yedekleme tamamlandı", data.Info);
        Assert.Equal("Bulut Sürücü", data.InfoTitle);
        Assert.Equal(TrayBalloonFlags.User | TrayBalloonFlags.NoSound, data.InfoFlags);
        Assert.Equal(15000u, data.Version);
        Assert.Equal((nint)0xBEEF, data.BalloonIconHandle);
    }

    [Fact]
    public void TryParse_IgnoresBalloonFieldsWhenInfoFlagNotSet()
    {
        var bytes = BuildBalloonData(
            flags: TrayIconFields.Tip,
            info: "Gizli kalmalı",
            infoTitle: "Başlık",
            infoFlags: TrayBalloonFlags.Warning,
            balloonIcon: 0x1234);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal("", data.Info);
        Assert.Equal("", data.InfoTitle);
        Assert.Equal(TrayBalloonFlags.None, data.InfoFlags);
        Assert.Equal(0, data.BalloonIconHandle);
    }

    [Fact]
    public void TryParse_ShorterBlockWithInfoFlag_DoesNotThrowAndDefaultsMissingTail()
    {
        // 944 bayt: szInfo (296..808), uTimeout (808..812), szInfoTitle (812..940), dwInfoFlags (940..944) var,
        // ancak guidItem (944..960) ve hBalloonIcon (960..964) yok.
        var bytes = BuildBalloonData(
            flags: TrayIconFields.Info,
            info: "Kısa blok",
            infoTitle: "Başlık",
            infoFlags: TrayBalloonFlags.Error,
            balloonIcon: 0x9999,
            length: 944);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal("Kısa blok", data.Info);
        Assert.Equal("Başlık", data.InfoTitle);
        Assert.Equal(TrayBalloonFlags.Error, data.InfoFlags);
        Assert.Equal(0, data.BalloonIconHandle);
    }

    [Fact]
    public void Filter_RejectsNonInfoAndEmptyDismissMessages()
    {
        var filter = new TrayBalloonFilter(new FakeClock());

        var noFlag = new TrayNotifyData(TrayMessage.Modify, 10, 1, TrayIconFields.Tip, 0, 0, "App", 0, 0, 0, Guid.Empty,
            Info: "Metin", InfoTitle: "Başlık");
        Assert.False(filter.TryAccept(noFlag, out _, out _, out _));

        // Win32 uygulamaları balonu kapatmak için boş szInfo gönderir.
        var emptyDismiss = new TrayNotifyData(TrayMessage.Modify, 10, 1, TrayIconFields.Info, 0, 0, "App", 0, 0, 0, Guid.Empty,
            Info: "   ", InfoTitle: "");
        Assert.False(filter.TryAccept(emptyDismiss, out _, out _, out _));

        var deleteMsg = new TrayNotifyData(TrayMessage.Delete, 10, 1, TrayIconFields.Info, 0, 0, "App", 0, 0, 0, Guid.Empty,
            Info: "Metin", InfoTitle: "Başlık");
        Assert.False(filter.TryAccept(deleteMsg, out _, out _, out _));
    }

    [Fact]
    public void Filter_DeduplicatesWithinCooldownAndAllowsAfterCooldown()
    {
        var clock = new FakeClock();
        var filter = new TrayBalloonFilter(clock);

        var msg = new TrayNotifyData(TrayMessage.Modify, 10, 1, TrayIconFields.Info, 0, 0, "App", 0, 0, 0, Guid.Empty,
            Info: "  Bağlantı kesildi  ", InfoTitle: "  VPN  ");

        Assert.True(filter.TryAccept(msg, out var title, out var body, out _));
        Assert.Equal("VPN", title);
        Assert.Equal("Bağlantı kesildi", body);

        // Aynı içerik bekleme süresi içinde reddedilir.
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.False(filter.TryAccept(msg, out _, out _, out _));

        // Farklı içerik aynı simgeden hemen kabul edilir.
        var different = msg with { Info = "Yeniden bağlandı" };
        Assert.True(filter.TryAccept(different, out _, out _, out _));

        // Bekleme süresi dolunca aynı içerik yeniden kabul edilir.
        clock.Advance(TrayBalloonFilter.DuplicateCooldown);
        Assert.True(filter.TryAccept(different, out _, out _, out _));
    }
}
