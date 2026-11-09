using Koy.Core.Bluetooth;

namespace Koy.Core.Tests;

public class BluetoothBatteryTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(86, 86)]
    [InlineData(100, 100)]
    public void GattBatteryParser_ValidBytes_ReturnsExpectedPercent(byte raw, int expected)
    {
        byte[] data = [raw];
        var result = GattBatteryParser.Parse(data);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(150)]
    [InlineData(255)]
    public void GattBatteryParser_ReservedOrInvalidBytes_ReturnsNull(byte raw)
    {
        byte[] data = [raw];
        var result = GattBatteryParser.Parse(data);
        Assert.Null(result);
    }

    [Fact]
    public void GattBatteryParser_EmptySpan_ReturnsNull()
    {
        var result = GattBatteryParser.Parse(ReadOnlySpan<byte>.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void GattBatteryParser_MultiBytePayload_UsesFirstByte()
    {
        byte[] data = [77, 0x01, 0x02];
        var result = GattBatteryParser.Parse(data);
        Assert.Equal(77, result);
    }

    [Fact]
    public void GattBatteryParser_ParseLowest_PicksMinimumValid()
    {
        ReadOnlyMemory<byte>[] readings =
        [
            new byte[] { 85 },
            new byte[] { 62 },
            new byte[] { 90 },
            new byte[] { 255 } // geçersiz, atlanmalı
        ];

        var lowest = GattBatteryParser.ParseLowest(readings);
        Assert.Equal(62, lowest);
    }

    [Fact]
    public void GattBatteryParser_ParseLowest_AllInvalid_ReturnsNull()
    {
        ReadOnlyMemory<byte>[] readings =
        [
            new byte[] { 105 },
            new byte[] { 200 }
        ];

        var lowest = GattBatteryParser.ParseLowest(readings);
        Assert.Null(lowest);
    }

    [Fact]
    public void BluetoothBatteryFilter_FirstReading_ReturnsSampleImmediately()
    {
        var filter = new BluetoothBatteryFilter();
        var val = filter.Filter("dev-1", 75);
        Assert.Equal(75, val);
    }

    [Fact]
    public void BluetoothBatteryFilter_ClampsOutOfBoundsInput()
    {
        var filter = new BluetoothBatteryFilter();
        Assert.Equal(0, filter.Filter("dev-1", -15));
        filter.Reset("dev-1");
        Assert.Equal(100, filter.Filter("dev-1", 130));
    }

    [Fact]
    public void BluetoothBatteryFilter_OutlierVoltageDrop_SuppressedByMedian()
    {
        // 3'lük pencerede tekil voltaj çöküşü (örn. RGB ya da sensör hareketiyle anlık 40'a düşüş)
        var filter = new BluetoothBatteryFilter(windowSize: 3, hysteresisThreshold: 2);
        
        Assert.Equal(80, filter.Filter("dev-mouse", 80));
        Assert.Equal(80, filter.Filter("dev-mouse", 80));
        
        // Tekil voltaj çöküşü: örnekler [80, 80, 40] -> medyan = 80
        Assert.Equal(80, filter.Filter("dev-mouse", 40));
        
        // Tekrar normal okuma: örnekler [80, 40, 80] -> medyan = 80
        Assert.Equal(80, filter.Filter("dev-mouse", 80));
    }

    [Fact]
    public void BluetoothBatteryFilter_SingleSpike_SuppressedByMedian()
    {
        // Yük kalkınca anlık voltaj toparlanması sıçraması
        var filter = new BluetoothBatteryFilter(windowSize: 3, hysteresisThreshold: 2);
        
        Assert.Equal(40, filter.Filter("dev-1", 40));
        Assert.Equal(40, filter.Filter("dev-1", 40));
        
        // Anlık sıçrama: örnekler [40, 40, 75] -> medyan = 40
        Assert.Equal(40, filter.Filter("dev-1", 75));
        
        // Örnekler [40, 75, 40] -> medyan = 40
        Assert.Equal(40, filter.Filter("dev-1", 40));
    }

    [Fact]
    public void BluetoothBatteryFilter_Hysteresis_SuppressesSmallJitter()
    {
        // 70 ile 71 arasında tek haneli ADC titreşimi
        var filter = new BluetoothBatteryFilter(windowSize: 3, hysteresisThreshold: 2, consecutiveThreshold: 3);
        
        Assert.Equal(70, filter.Filter("dev-kb", 70));
        Assert.Equal(70, filter.Filter("dev-kb", 70));
        Assert.Equal(70, filter.Filter("dev-kb", 70));
        
        // 71 geldiğinde medyan 70..71 olur ama fark 1 (< 2) olduğu için bastırılır
        Assert.Equal(70, filter.Filter("dev-kb", 71));
        Assert.Equal(70, filter.Filter("dev-kb", 70));
        Assert.Equal(70, filter.Filter("dev-kb", 71));
    }

    [Fact]
    public void BluetoothBatteryFilter_PersistentSmallChange_AcceptedAfterConsecutiveCount()
    {
        // 70'den 69'a kalıcı olarak düşüyor (1 birim fark), 3 ardışık okuma sonra güncellenmeli
        var filter = new BluetoothBatteryFilter(windowSize: 1, hysteresisThreshold: 2, consecutiveThreshold: 3);
        
        Assert.Equal(70, filter.Filter("dev-1", 70));
        
        // 1. okuma (delta = -1, eşik 2 altında, beklemede)
        Assert.Equal(70, filter.Filter("dev-1", 69));
        // 2. okuma (aynı yönde 2. kez)
        Assert.Equal(70, filter.Filter("dev-1", 69));
        // 3. okuma (aynı yönde 3. kez -> artık güncellenir)
        Assert.Equal(69, filter.Filter("dev-1", 69));
    }

    [Fact]
    public void BluetoothBatteryFilter_LargeDelta_UpdatesImmediately()
    {
        // Şarja takıldı: 40 -> 60 (+20 birim)
        var filter = new BluetoothBatteryFilter(windowSize: 1, hysteresisThreshold: 2);
        
        Assert.Equal(40, filter.Filter("dev-1", 40));
        Assert.Equal(60, filter.Filter("dev-1", 60));
    }

    [Fact]
    public void BluetoothBatteryFilter_MultipleDevices_Isolated()
    {
        var filter = new BluetoothBatteryFilter();
        
        Assert.Equal(95, filter.Filter("dev-mouse", 95));
        Assert.Equal(30, filter.Filter("dev-keyboard", 30));
        
        Assert.Equal(95, filter.Filter("dev-mouse", 95));
        Assert.Equal(30, filter.Filter("dev-keyboard", 30));
    }

    [Fact]
    public void BluetoothBatteryFilter_Reset_ClearsDeviceState()
    {
        var filter = new BluetoothBatteryFilter(windowSize: 3, hysteresisThreshold: 2);
        
        Assert.Equal(80, filter.Filter("dev-1", 80));
        Assert.Equal(80, filter.Filter("dev-1", 80));
        
        filter.Reset("dev-1");
        
        // Reset sonrası ilk okuma hemen yeni değer olarak atanır
        Assert.Equal(50, filter.Filter("dev-1", 50));
    }

    [Fact]
    public void BluetoothBatteryFilter_Hysteresis_ThresholdOne_UpdatesImmediatelyOnDirectionChange()
    {
        // consecutiveThreshold = 1 iken yön değiştiğinde ilk okumada güncellenmeli
        var filter = new BluetoothBatteryFilter(windowSize: 1, hysteresisThreshold: 2, consecutiveThreshold: 1);

        Assert.Equal(70, filter.Filter("dev-1", 70));
        // Delta = -1, consecutiveThreshold = 1 olduğu için anında güncellenir
        Assert.Equal(69, filter.Filter("dev-1", 69));
    }

    [Fact]
    public void GattBatteryParser_ParseLowest_NullReadings_ReturnsNull()
    {
        var result = GattBatteryParser.ParseLowest(null);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65)]
    [InlineData(100)]
    public void BluetoothBatteryFilter_InvalidWindowSize_ThrowsArgumentOutOfRangeException(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BluetoothBatteryFilter(windowSize: size));
    }
}
