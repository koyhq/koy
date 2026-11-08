using Koy.Core.Bluetooth;
using Koy.Core.Radio;

namespace Koy.Core.Tests;

public class BluetoothListTests
{
    private static BluetoothDeviceInfo Dev(string id, string name, BluetoothDeviceKind kind = BluetoothDeviceKind.Headphones,
        bool connected = false, int? battery = null, string? container = null, bool le = false) =>
        new(id, name, kind, connected, battery, container, le);

    [Theory]
    [InlineData(0x418u, BluetoothDeviceKind.Headphones)] // ses/video, kulaklık
    [InlineData(0x404u, BluetoothDeviceKind.Headphones)] // ses/video, kulaklık takımı
    [InlineData(0x408u, BluetoothDeviceKind.Headphones)] // eller serbest
    [InlineData(0x414u, BluetoothDeviceKind.Speaker)] // hoparlör
    [InlineData(0x41Cu, BluetoothDeviceKind.Speaker)] // taşınabilir ses
    [InlineData(0x540u, BluetoothDeviceKind.Keyboard)]
    [InlineData(0x580u, BluetoothDeviceKind.Mouse)]
    [InlineData(0x5C0u, BluetoothDeviceKind.Keyboard)] // klavye + fare birleşik
    [InlineData(0x500u, BluetoothDeviceKind.Other)] // ayrıntısız çevre birimi
    [InlineData(0x20Cu, BluetoothDeviceKind.Phone)]
    [InlineData(0x11Cu, BluetoothDeviceKind.Tablet)]
    [InlineData(0x104u, BluetoothDeviceKind.Other)] // masaüstü bilgisayar
    [InlineData(0x0u, BluetoothDeviceKind.Other)]
    public void FromClassOfDevice_AnaVeAltSinif(uint cod, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothList.FromClassOfDevice(cod));

    [Theory]
    [InlineData(0x0040u, BluetoothDeviceKind.Phone)]
    [InlineData(0x0087u, BluetoothDeviceKind.Tablet)]
    [InlineData(0x0083u, BluetoothDeviceKind.Other)] // dizüstü
    [InlineData(0x03C1u, BluetoothDeviceKind.Keyboard)]
    [InlineData(0x03C2u, BluetoothDeviceKind.Mouse)]
    [InlineData(0x03C9u, BluetoothDeviceKind.Mouse)] // dokunmatik yüzey
    [InlineData(0x03C4u, BluetoothDeviceKind.Other)] // oyun kolu
    [InlineData(0x0841u, BluetoothDeviceKind.Speaker)]
    [InlineData(0x0943u, BluetoothDeviceKind.Headphones)]
    [InlineData(0x0941u, BluetoothDeviceKind.Headphones)]
    [InlineData(0x0000u, BluetoothDeviceKind.Other)]
    public void FromAppearance_KategoriVeAltKategori(uint appearance, BluetoothDeviceKind expected) =>
        Assert.Equal(expected, BluetoothList.FromAppearance(appearance));

    [Fact]
    public void IsAudio_YalnizKulaklikVeHoparlor()
    {
        Assert.True(Dev("1", "K", BluetoothDeviceKind.Headphones).IsAudio);
        Assert.True(Dev("1", "H", BluetoothDeviceKind.Speaker).IsAudio);
        Assert.False(Dev("1", "F", BluetoothDeviceKind.Mouse).IsAudio);
        Assert.False(Dev("1", "T", BluetoothDeviceKind.Phone).IsAudio);
        Assert.False(Dev("1", "D", BluetoothDeviceKind.Other).IsAudio);
    }

    [Fact]
    public void Normalize_BagliOnce_SonraAdaGore()
    {
        var list = BluetoothList.Normalize([Dev("1", "Zeta"), Dev("2", "Alfa"), Dev("3", "Orta", connected: true)]);

        Assert.Equal(["Orta", "Alfa", "Zeta"], list.Select(d => d.Name));
    }

    [Fact]
    public void Normalize_AdsizKayitlariAtar()
    {
        var list = BluetoothList.Normalize([Dev("1", ""), Dev("2", "  "), Dev("3", "Kulaklık")]);

        Assert.Equal("Kulaklık", Assert.Single(list).Name);
    }

    [Fact]
    public void Normalize_AyniKonteyner_KlasikTercihEdilirDurumBirlesir()
    {
        var classic = Dev("c", "Kulaklık", battery: null, container: "AAA");
        var le = Dev("l", "Kulaklık", connected: true, battery: 86, container: "aaa", le: true);

        var merged = Assert.Single(BluetoothList.Normalize([le, classic]));

        Assert.Equal("c", merged.Id);
        Assert.True(merged.IsConnected);
        Assert.Equal(86, merged.BatteryPercent);
    }

    [Fact]
    public void Normalize_KonteynerYoksa_BirlestirmezAyniAdliIkiCihaz()
    {
        var list = BluetoothList.Normalize([Dev("1", "Fare", BluetoothDeviceKind.Mouse), Dev("2", "Fare", BluetoothDeviceKind.Mouse)]);

        Assert.Equal(2, list.Count);
    }

    [Fact]
    public void PrimaryAudio_BagliSesCihazi_PiliOlanOnce()
    {
        var devices = new[]
        {
            Dev("1", "Fare", BluetoothDeviceKind.Mouse, connected: true, battery: 40),
            Dev("2", "Hoparlör", BluetoothDeviceKind.Speaker, connected: true),
            Dev("3", "Kulaklık", BluetoothDeviceKind.Headphones, connected: true, battery: 86),
            Dev("4", "Eski", BluetoothDeviceKind.Headphones, connected: false, battery: 99)
        };

        Assert.Equal("Kulaklık", BluetoothList.PrimaryAudio(devices)!.Name);
    }

    [Fact]
    public void PrimaryAudio_BagliSesCihaziYoksa_Null() =>
        Assert.Null(BluetoothList.PrimaryAudio([Dev("1", "Fare", BluetoothDeviceKind.Mouse, connected: true), Dev("2", "K", connected: false)]));

    [Theory]
    [InlineData(86, "%86")]
    [InlineData(0, "%0")]
    [InlineData(100, "%100")]
    public void BatteryText_Gecerli(int percent, string expected) => Assert.Equal(expected, BluetoothList.BatteryText(percent, System.Globalization.CultureInfo.GetCultureInfo("tr-TR")));

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(101)]
    public void BatteryText_Gecersiz_Null(int? percent) => Assert.Null(BluetoothList.BatteryText(percent, System.Globalization.CultureInfo.GetCultureInfo("tr-TR")));

    [Fact]
    public void ParseBattery_BaytVeGecersizDegerler()
    {
        Assert.Equal(86, BluetoothList.ParseBattery((byte)86));
        Assert.Equal(55, BluetoothList.ParseBattery(55));
        Assert.Null(BluetoothList.ParseBattery(null));
        Assert.Null(BluetoothList.ParseBattery((byte)255));
        Assert.Null(BluetoothList.ParseBattery("abc"));
        Assert.Null(BluetoothList.ParseBattery(new object()));
    }

    [Fact]
    public void ActionFor_SesCihazi_BaglaYaDaKes_DigeriAyarlar()
    {
        Assert.Equal(BluetoothRowAction.Connect, BluetoothList.ActionFor(Dev("1", "K", connected: false)));
        Assert.Equal(BluetoothRowAction.Disconnect, BluetoothList.ActionFor(Dev("1", "K", connected: true)));
        Assert.Equal(BluetoothRowAction.OpenSettings, BluetoothList.ActionFor(Dev("1", "F", BluetoothDeviceKind.Mouse, connected: true)));
        Assert.Equal(BluetoothRowAction.OpenSettings, BluetoothList.ActionFor(Dev("1", "T", BluetoothDeviceKind.Phone)));
    }

    [Fact]
    public void ErrorKey_DogrulanmisBasariNull_Digerleri_Anahtar()
    {
        Assert.Null(BluetoothList.ErrorKey(BluetoothActionResult.Confirmed, true));
        Assert.Equal("BluetoothErrUnsupported", BluetoothList.ErrorKey(BluetoothActionResult.Unsupported, true));
        Assert.Equal("BluetoothErrConnect", BluetoothList.ErrorKey(BluetoothActionResult.NotConfirmed, true));
        Assert.Equal("BluetoothErrDisconnect", BluetoothList.ErrorKey(BluetoothActionResult.Failed, false));
    }

    [Theory]
    [InlineData(RadioTileStatus.NoAccess, true, 3, BluetoothListState.NoAccess)]
    [InlineData(RadioTileStatus.NotFound, true, 3, BluetoothListState.NotFound)]
    [InlineData(RadioTileStatus.Off, true, 3, BluetoothListState.RadioOff)]
    [InlineData(RadioTileStatus.HardwareOff, true, 3, BluetoothListState.RadioOff)]
    [InlineData(RadioTileStatus.Loading, true, 3, BluetoothListState.Loading)]
    [InlineData(RadioTileStatus.On, false, 0, BluetoothListState.Loading)]
    [InlineData(RadioTileStatus.On, true, 0, BluetoothListState.Empty)]
    [InlineData(RadioTileStatus.On, true, 2, BluetoothListState.Ready)]
    [InlineData(RadioTileStatus.Failed, true, 2, BluetoothListState.Ready)]
    public void Resolve_RadyoVeServisDurumundan(RadioTileStatus radio, bool ready, int count, BluetoothListState expected) =>
        Assert.Equal(expected, BluetoothList.Resolve(radio, ready, count));

    [Fact]
    public void StatusKey_BagliBagliDegil()
    {
        Assert.Equal("BluetoothConnected", BluetoothList.StatusKey(Dev("1", "K", connected: true)));
        Assert.Equal("BluetoothNotConnected", BluetoothList.StatusKey(Dev("1", "K", connected: false)));
    }
}
