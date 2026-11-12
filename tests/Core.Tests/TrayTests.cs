using System.Buffers.Binary;
using System.Text;
using Koy.Core.Tray;
using Xunit;

namespace Koy.Core.Tests;

public class TrayMessageParserTests
{
    /// <summary>shell32'nin gönderdiği SHELLTRAYDATA bloğunu (964 bayt) üretir.</summary>
    internal static byte[] BuildShellTrayData(
        uint message, uint hwnd, uint id, uint flags, uint callback = 0, uint icon = 0, string tip = "",
        uint state = 0, uint stateMask = 0, uint version = 0, Guid guid = default, int length = 964)
    {
        var data = new byte[length];
        var full = new byte[964];
        BinaryPrimitives.WriteInt32LittleEndian(full, TrayMessageParser.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(4), message);
        BinaryPrimitives.WriteInt32LittleEndian(full.AsSpan(8), 956);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(12), hwnd);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(16), id);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(20), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(24), callback);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(28), icon);
        Encoding.Unicode.GetBytes(tip).CopyTo(full, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(288), state);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(292), stateMask);
        BinaryPrimitives.WriteUInt32LittleEndian(full.AsSpan(808), version);
        guid.TryWriteBytes(full.AsSpan(944));
        Array.Copy(full, data, Math.Min(length, full.Length));
        return data;
    }

    [Fact]
    public void TryParse_ReadsAllFields()
    {
        var guid = Guid.NewGuid();
        var bytes = BuildShellTrayData(
            message: 1, hwnd: 0x1234, id: 7, flags: 0x2F, callback: 0x8001, icon: 0xABCD,
            tip: "Discord", state: 1, stateMask: 1, version: 4, guid: guid);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal(TrayMessage.Modify, data.Message);
        Assert.Equal((nint)0x1234, data.OwnerHwnd);
        Assert.Equal(7u, data.Id);
        Assert.Equal(TrayIconFields.Message | TrayIconFields.Icon | TrayIconFields.Tip | TrayIconFields.State | TrayIconFields.Guid, data.Fields);
        Assert.Equal(0x8001u, data.CallbackMessage);
        Assert.Equal((nint)0xABCD, data.IconHandle);
        Assert.Equal("Discord", data.Tooltip);
        Assert.Equal(1u, data.State);
        Assert.Equal(1u, data.StateMask);
        Assert.Equal(4u, data.Version);
        Assert.Equal(guid, data.Guid);
    }

    [Fact]
    public void TryParse_SignExtendsHandlesLikeWindows()
    {
        var bytes = BuildShellTrayData(message: 0, hwnd: 0xFFFF1234, id: 1, flags: 0);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal((nint)unchecked((int)0xFFFF1234), data.OwnerHwnd);
    }

    [Fact]
    public void TryParse_RejectsWrongSignatureShortBlockAndUnknownMessage()
    {
        var wrongSignature = BuildShellTrayData(0, 1, 1, 0);
        BinaryPrimitives.WriteInt32LittleEndian(wrongSignature, 0x12345678);

        Assert.False(TrayMessageParser.TryParse(wrongSignature, out _));
        Assert.False(TrayMessageParser.TryParse(BuildShellTrayData(0, 1, 1, 0, length: 100), out _));
        Assert.False(TrayMessageParser.TryParse(BuildShellTrayData(message: 9, hwnd: 1, id: 1, flags: 0), out _));
    }

    [Fact]
    public void TryParse_ShorterBlockWithoutVersionAndGuid_DefaultsThem()
    {
        var bytes = BuildShellTrayData(message: 0, hwnd: 5, id: 2, flags: 0x4, tip: "Steam", version: 4, length: TrayMessageParser.MinimumLength);

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal("Steam", data.Tooltip);
        Assert.Equal(0u, data.Version);
        Assert.Equal(Guid.Empty, data.Guid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TrayMessageParser.MinimumLength - 1)]
    [InlineData(TrayMessageParser.MaximumLength + 1)]
    [InlineData((long)int.MaxValue + 1)]   // cbData işaretsiz 32 bit: (int)'e çevrilince negatif olurdu
    [InlineData(uint.MaxValue)]
    public void IsAcceptableLength_RejectsOutOfRangeSizes(long length) =>
        Assert.False(TrayMessageParser.IsAcceptableLength(length));

    [Theory]
    [InlineData(TrayMessageParser.MinimumLength)]
    [InlineData(964)]
    [InlineData(TrayMessageParser.MaximumLength)]
    public void IsAcceptableLength_AcceptsRealBlockSizes(long length) =>
        Assert.True(TrayMessageParser.IsAcceptableLength(length));

    [Fact]
    public void TryParse_RejectsOversizedBlockEvenWithValidSignature()
    {
        var bytes = BuildShellTrayData(message: 0, hwnd: 1, id: 1, flags: 0, length: TrayMessageParser.MaximumLength + 1);

        Assert.False(TrayMessageParser.TryParse(bytes, out _));
    }

    [Fact]
    public void TryParse_NeverThrowsOnTruncatedOrGarbageBlocks()
    {
        // Başka süreçler Koy'un penceresine her türlü veriyi gönderebilir: hiçbir uzunluk istisna fırlatmamalı.
        var valid = BuildShellTrayData(message: 0, hwnd: 1, id: 1, flags: 0x2F, tip: "X");
        for (var length = 0; length <= valid.Length; length++)
            _ = TrayMessageParser.TryParse(valid.AsSpan(0, length), out _);

        var random = new Random(1234);
        var garbage = new byte[2048];
        for (var round = 0; round < 200; round++)
        {
            random.NextBytes(garbage);
            if (round % 2 == 0)
                BinaryPrimitives.WriteInt32LittleEndian(garbage, TrayMessageParser.Signature);
            _ = TrayMessageParser.TryParse(garbage, out _);
        }
    }

    [Fact]
    public void TryParse_TooltipWithoutTerminatorIsTruncatedToFieldSize()
    {
        var bytes = BuildShellTrayData(message: 0, hwnd: 1, id: 1, flags: 0x4, tip: new string('A', 127));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(32 + 127 * 2), 'B');   // sonlandırıcı yok: alan tam dolu

        Assert.True(TrayMessageParser.TryParse(bytes, out var data));
        Assert.Equal(128, data.Tooltip.Length);
    }
}

public class TrayIconStoreTests
{
    private static TrayNotifyData Msg(
        TrayMessage message, nint hwnd = 100, uint id = 1, TrayIconFields fields = TrayIconFields.Message | TrayIconFields.Icon | TrayIconFields.Tip,
        uint callback = 0x8001, nint icon = 500, string tip = "App", uint state = 0, uint mask = 0, uint version = 0, Guid guid = default) =>
        new(message, hwnd, id, fields, callback, icon, tip, state, mask, version, guid);

    [Fact]
    public void Add_KeepsInsertionOrder()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, hwnd: 1, icon: 11, tip: "A"));
        store.Apply(Msg(TrayMessage.Add, hwnd: 2, icon: 22, tip: "B"));
        store.Apply(Msg(TrayMessage.Add, hwnd: 3, icon: 33, tip: "C"));

        Assert.Equal(["A", "B", "C"], store.Visible.Select(i => i.Tooltip));
    }

    [Fact]
    public void Add_BeyondLimit_RejectsNewIconAndReturnsItsHandle()
    {
        var store = new TrayIconStore();
        for (var i = 1; i <= TrayIconStore.MaxIcons; i++)
            Assert.True(store.Apply(Msg(TrayMessage.Add, hwnd: i, icon: 1000 + i)).Success);

        var result = store.Apply(Msg(TrayMessage.Add, hwnd: TrayIconStore.MaxIcons + 1, icon: 99999));

        Assert.False(result.Success);
        Assert.Equal([(nint)99999], result.ReleasedIcons);   // çağıran kopyayı yok edebilsin (sızıntı olmasın)
        Assert.Equal(TrayIconStore.MaxIcons, store.All.Count);
    }

    [Fact]
    public void Add_AtLimit_StillAllowsUpdatingAndRemovingExistingIcons()
    {
        var store = new TrayIconStore();
        for (var i = 1; i <= TrayIconStore.MaxIcons; i++)
            store.Apply(Msg(TrayMessage.Add, hwnd: i, icon: 1000 + i));

        Assert.True(store.Apply(Msg(TrayMessage.Add, hwnd: 5, icon: 7777, tip: "Yeni")).Success);
        Assert.True(store.Apply(Msg(TrayMessage.Delete, hwnd: 6, fields: TrayIconFields.None)).Success);
        Assert.True(store.Apply(Msg(TrayMessage.Add, hwnd: 9000, icon: 8888)).Success);
    }

    [Fact]
    public void Add_ExistingIcon_UpdatesInPlaceAndReleasesOldIcon()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, hwnd: 1, icon: 11, tip: "A"));
        store.Apply(Msg(TrayMessage.Add, hwnd: 2, icon: 22, tip: "B"));

        var result = store.Apply(Msg(TrayMessage.Add, hwnd: 1, icon: 99, tip: "A2"));

        Assert.True(result.Success);
        Assert.Equal([(nint)11], result.ReleasedIcons);
        Assert.Equal(["A2", "B"], store.Visible.Select(i => i.Tooltip));
        Assert.Equal((nint)99, store.Visible[0].IconHandle);
    }

    [Fact]
    public void Modify_OnlyChangesFlaggedFields()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, icon: 11, tip: "Eski"));

        var result = store.Apply(Msg(TrayMessage.Modify, fields: TrayIconFields.Tip, icon: 77, tip: "Yeni"));

        Assert.True(result.Success);
        Assert.Empty(result.ReleasedIcons);
        var icon = Assert.Single(store.Visible);
        Assert.Equal("Yeni", icon.Tooltip);
        Assert.Equal((nint)11, icon.IconHandle);
        Assert.Equal(0x8001u, icon.CallbackMessage);
    }

    [Fact]
    public void Modify_UnknownIcon_FailsAndReturnsIncomingCopy()
    {
        var store = new TrayIconStore();

        var result = store.Apply(Msg(TrayMessage.Modify, icon: 44));

        Assert.False(result.Success);
        Assert.Equal([(nint)44], result.ReleasedIcons);
        Assert.Empty(store.All);
    }

    [Fact]
    public void Delete_RemovesAndReleasesIcon()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, icon: 11));

        var result = store.Apply(Msg(TrayMessage.Delete, fields: TrayIconFields.None));

        Assert.True(result.Success);
        Assert.Equal([(nint)11], result.ReleasedIcons);
        Assert.Empty(store.All);
        Assert.False(store.Apply(Msg(TrayMessage.Delete, fields: TrayIconFields.None)).Success);
    }

    [Fact]
    public void State_HiddenFlagHidesAndShowsIcon()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, fields: TrayIconFields.Message | TrayIconFields.Icon | TrayIconFields.State, state: 1, mask: 1));
        Assert.Empty(store.Visible);
        Assert.Single(store.All);

        // Maske gizleme bitini içermiyorsa durum değişmez.
        store.Apply(Msg(TrayMessage.Modify, fields: TrayIconFields.State, state: 0, mask: 0));
        Assert.Empty(store.Visible);

        store.Apply(Msg(TrayMessage.Modify, fields: TrayIconFields.State, state: 0, mask: 1));
        Assert.Single(store.Visible);
    }

    [Fact]
    public void IconWithoutImage_IsNotVisible()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, fields: TrayIconFields.Message | TrayIconFields.Tip, icon: 0));

        Assert.Single(store.All);
        Assert.Empty(store.Visible);
    }

    [Fact]
    public void SetVersion_StoresVersion()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add));

        Assert.True(store.Apply(Msg(TrayMessage.SetVersion, fields: TrayIconFields.None, version: 4)).Success);
        Assert.Equal(4u, store.All[0].Version);
    }

    [Fact]
    public void GuidIcon_IsIdentifiedByGuidEvenIfWindowChanges()
    {
        var guid = Guid.NewGuid();
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, hwnd: 1, fields: TrayIconFields.Message | TrayIconFields.Icon | TrayIconFields.Guid, guid: guid, icon: 11));

        store.Apply(Msg(TrayMessage.Modify, hwnd: 2, fields: TrayIconFields.Icon | TrayIconFields.Guid, guid: guid, icon: 22));

        var icon = Assert.Single(store.All);
        Assert.Equal((nint)2, icon.OwnerHwnd);
        Assert.Equal((nint)22, icon.IconHandle);
    }

    [Fact]
    public void RemoveDead_DropsIconsOfClosedWindows()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, hwnd: 1, icon: 11));
        store.Apply(Msg(TrayMessage.Add, hwnd: 2, icon: 22));

        var released = store.RemoveDead(hwnd => hwnd == 2);

        Assert.Equal([(nint)11], released);
        Assert.Equal((nint)2, Assert.Single(store.All).OwnerHwnd);
    }

    [Fact]
    public void Clear_ReleasesEveryIcon()
    {
        var store = new TrayIconStore();
        store.Apply(Msg(TrayMessage.Add, hwnd: 1, icon: 11));
        store.Apply(Msg(TrayMessage.Add, hwnd: 2, icon: 22));

        Assert.Equal([(nint)11, (nint)22], store.Clear());
        Assert.Empty(store.All);
    }
}

public class TrayCallbackTests
{
    private static TrayIcon Icon(uint version, uint id = 3) =>
        new(new TrayIconKey(100, id, Guid.Empty), 100, id, 0x8001, 1, "App", false, version);

    [Fact]
    public void Version4_LeftClick_SendsDownUpSelectWithCursorAndId()
    {
        var messages = TrayCallback.Build(Icon(version: 4, id: 3), TrayMouseAction.LeftClick, 1500, 1040);

        Assert.Equal(3, messages.Count);
        Assert.All(messages, m => Assert.Equal(0x8001u, m.Message));
        Assert.All(messages, m => Assert.Equal((nint)((1040 << 16) | 1500), m.WParam));
        Assert.Equal((nint)((3 << 16) | 0x0201), messages[0].LParam);
        Assert.Equal((nint)((3 << 16) | 0x0202), messages[1].LParam);
        Assert.Equal((nint)((3 << 16) | 0x0400), messages[2].LParam);
    }

    [Fact]
    public void Version4_RightClick_EndsWithContextMenu()
    {
        var messages = TrayCallback.Build(Icon(version: 4), TrayMouseAction.RightClick, 10, 20);

        Assert.Equal([0x0204, 0x0205, 0x007B], messages.Select(m => (int)(m.LParam & 0xFFFF)));
    }

    [Fact]
    public void LegacyVersion_SendsIdAndPlainMouseMessage()
    {
        var messages = TrayCallback.Build(Icon(version: 0, id: 9), TrayMouseAction.LeftClick, 10, 20);

        Assert.Equal(2, messages.Count);
        Assert.All(messages, m => Assert.Equal((nint)9, m.WParam));
        Assert.Equal([(nint)0x0201, (nint)0x0202], messages.Select(m => m.LParam));
    }

    [Fact]
    public void DoubleClick_SendsDoubleClickThenUp()
    {
        var messages = TrayCallback.Build(Icon(version: 0), TrayMouseAction.LeftDoubleClick, 0, 0);

        Assert.Equal([(nint)0x0203, (nint)0x0202], messages.Select(m => m.LParam));
    }

    [Fact]
    public void NegativeCursorOnLeftMonitor_IsPackedAsSignedWords()
    {
        var messages = TrayCallback.Build(Icon(version: 4), TrayMouseAction.LeftClick, -100, 50);

        var w = (uint)messages[0].WParam;
        Assert.Equal(-100, (short)(w & 0xFFFF));
        Assert.Equal(50, (short)(w >> 16));
    }

    [Fact]
    public void IconWithoutCallback_SendsNothing()
    {
        var icon = Icon(version: 4) with { CallbackMessage = 0 };

        Assert.Empty(TrayCallback.Build(icon, TrayMouseAction.LeftClick, 0, 0));
    }
}
