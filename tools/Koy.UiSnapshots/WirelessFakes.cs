using Koy.Core.Bluetooth;
using Koy.Core.Wireless;

namespace Koy.UiSnapshots;

// Wi-Fi ve Bluetooth sahneleri için sahte servisler: gerçek tarama, bağlanma ve KS çağrısı yapılmaz.

internal sealed class FakeWifi(bool locationBlocked = false) : IWifiService
{
    public Task<WifiScanResult> ScanAsync()
    {
        if (locationBlocked)
        {
            // Konum izni yokken Windows ağları adsız döndürür.
            return Task.FromResult(new WifiScanResult(WifiAccess.Allowed, true, false,
                [new WifiNetwork("", 80, true, false, false), new WifiNetwork("", 55, true, false, false)]));
        }

        return Task.FromResult(new WifiScanResult(WifiAccess.Allowed, true, false,
        [
            new WifiNetwork(Sample.T("Ev Ağı", "Home"), 88, true, true, true),
            new WifiNetwork(Sample.T("Komşu_5G", "Neighbor_5G"), 72, true, false, false),
            new WifiNetwork(Sample.T("Kafe Misafir", "Cafe Guest"), 51, false, false, false),
            new WifiNetwork(Sample.T("Ofis", "Office"), 33, true, false, true),
            new WifiNetwork("", 20, true, false, false)
        ]));
    }

    public Task<WifiConnectStatus> ConnectAsync(string ssid, string? password) => Task.FromResult(WifiConnectStatus.Connected);
    public void Disconnect() { }
    public void OpenSettings() { }
    public void OpenLocationSettings() { }
    public void Dispose() { }
}

internal sealed class FakeBluetooth(bool anyAudioConnected = true) : IBluetoothService
{
    public bool IsReady => true;

    public IReadOnlyList<BluetoothDeviceInfo> Devices { get; } =
    [
        new("a", Sample.T("Kulaklık Pro", "Headphones Pro"), BluetoothDeviceKind.Headphones, anyAudioConnected, anyAudioConnected ? 86 : null, "c1"),
        new("b", Sample.T("Sessiz Fare", "Silent Mouse"), BluetoothDeviceKind.Mouse, true, 40, "c2"),
        new("c", Sample.T("Salon Hoparlörü", "Living Room Speaker"), BluetoothDeviceKind.Speaker, false, null, "c3"),
        new("d", Sample.T("Mekanik Klavye", "Mechanical Keyboard"), BluetoothDeviceKind.Keyboard, false, null, "c4"),
        new("e", Sample.T("Telefonum", "My Phone"), BluetoothDeviceKind.Phone, false, null, "c5"),
        new("f", "Bilinmeyen Cihaz", BluetoothDeviceKind.Other, false, null, "c6")
    ];

    public event EventHandler? DevicesChanged { add { } remove { } }
    public Task<BluetoothActionResult> ConnectAsync(string deviceId) => Task.FromResult(BluetoothActionResult.Confirmed);
    public Task<BluetoothActionResult> DisconnectAsync(string deviceId) => Task.FromResult(BluetoothActionResult.Confirmed);
    public void SetBatteryReadsEnabled(bool enabled) { }
    public void OpenSettings() { }
    public void Dispose() { }
}
