using Koy.Core.Bluetooth;

namespace Koy.Core.Tests.Bluetooth;

public class BluetoothHudPolicyTests
{
    private static BluetoothDeviceInfo CreateDevice(
        string id,
        string name,
        BluetoothDeviceKind kind = BluetoothDeviceKind.Headphones,
        bool isConnected = false,
        int? batteryPercent = null,
        string? containerId = null) =>
        new(id, name, kind, isConnected, batteryPercent, containerId);

    [Fact]
    public void InitialCall_SeedsState_DoesNotEmitEvents()
    {
        var policy = new BluetoothHudPolicy();
        var devices = new[]
        {
            CreateDevice("dev1", "Sony WH-1000XM4", BluetoothDeviceKind.Headphones, isConnected: true, batteryPercent: 85),
            CreateDevice("dev2", "AirPods Pro", BluetoothDeviceKind.Headphones, isConnected: false)
        };

        var events = policy.Update(devices, TimeSpan.FromSeconds(1));

        Assert.Empty(events);
    }

    [Fact]
    public void AudioDevice_Connect_EmitsConnectedEvent()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "Sony WH-1000XM4", BluetoothDeviceKind.Headphones, isConnected: false);

        // Seeding
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Connects
        var connected = dev with { IsConnected = true, BatteryPercent = 90 };
        var events = policy.Update([connected], TimeSpan.FromSeconds(1));

        var evt = Assert.Single(events);
        Assert.Equal("Sony WH-1000XM4", evt.Name);
        Assert.True(evt.Connected);
        Assert.Equal(90, evt.BatteryPercent);
    }

    [Fact]
    public void AudioDevice_Disconnect_EmitsDisconnectedEvent()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "JBL Flip 6", BluetoothDeviceKind.Speaker, isConnected: true, batteryPercent: 70);

        // Seeding
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Disconnects
        var disconnected = dev with { IsConnected = false, BatteryPercent = null };
        var events = policy.Update([disconnected], TimeSpan.FromSeconds(1));

        var evt = Assert.Single(events);
        Assert.Equal("JBL Flip 6", evt.Name);
        Assert.False(evt.Connected);
        Assert.Null(evt.BatteryPercent);
    }

    [Fact]
    public void NonAudioDevice_Ignored_ProducesNoEvents()
    {
        var policy = new BluetoothHudPolicy();
        var mouse = CreateDevice("mouse1", "Logitech MX Master 3", BluetoothDeviceKind.Mouse, isConnected: false);
        var keyboard = CreateDevice("kb1", "Keychron K2", BluetoothDeviceKind.Keyboard, isConnected: false);

        // Seeding
        policy.Update([mouse, keyboard], TimeSpan.FromSeconds(0));

        // Connect both non-audio devices
        var mouseConn = mouse with { IsConnected = true, BatteryPercent = 80 };
        var kbConn = keyboard with { IsConnected = true, BatteryPercent = 60 };
        var events = policy.Update([mouseConn, kbConn], TimeSpan.FromSeconds(1));

        Assert.Empty(events);
    }

    [Fact]
    public void Debounce_RapidBounce_SuppressesSecondTransition()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "Bose QC45", BluetoothDeviceKind.Headphones, isConnected: false);

        // Seeding at t=0
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Connects at t=1.0s -> emits
        var conn = dev with { IsConnected = true, BatteryPercent = 100 };
        var events1 = policy.Update([conn], TimeSpan.FromSeconds(1.0));
        var evt1 = Assert.Single(events1);
        Assert.True(evt1.Connected);

        // Rapid disconnect at t=2.0s (1.0s later, within 3.0s window) -> suppressed
        var disconn = dev with { IsConnected = false };
        var events2 = policy.Update([disconn], TimeSpan.FromSeconds(2.0));
        Assert.Empty(events2);

        // Still disconnected at t=3.5s (2.5s later from t=1.0s, still < 3.0s) -> suppressed
        var events3 = policy.Update([disconn], TimeSpan.FromSeconds(3.5));
        Assert.Empty(events3);
    }

    [Fact]
    public void Debounce_ValidEventAfter3Seconds_Emitted()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "Bose QC45", BluetoothDeviceKind.Headphones, isConnected: false);

        // Seeding at t=0
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Connects at t=1.0s -> emits
        var conn = dev with { IsConnected = true, BatteryPercent = 100 };
        var events1 = policy.Update([conn], TimeSpan.FromSeconds(1.0));
        Assert.Single(events1);

        // Disconnects at t=4.5s (3.5s later, > 3.0s window) -> should emit
        var disconn = dev with { IsConnected = false };
        var events2 = policy.Update([disconn], TimeSpan.FromSeconds(4.5));
        var evt2 = Assert.Single(events2);
        Assert.False(evt2.Connected);
        Assert.Equal("Bose QC45", evt2.Name);
    }

    [Fact]
    public void BatteryChangeWhileConnected_DoesNotEmit()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "AirPods Max", BluetoothDeviceKind.Headphones, isConnected: true, batteryPercent: 90);

        // Seeding
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Battery updates from 90% to 85%, connection state remains connected
        var updated = dev with { BatteryPercent = 85 };
        var events = policy.Update([updated], TimeSpan.FromSeconds(5));

        Assert.Empty(events);
    }

    [Fact]
    public void MultipleDevices_TrackedIndependently()
    {
        var policy = new BluetoothHudPolicy();
        var dev1 = CreateDevice("dev1", "Headphones 1", BluetoothDeviceKind.Headphones, isConnected: false);
        var dev2 = CreateDevice("dev2", "Speaker 2", BluetoothDeviceKind.Speaker, isConnected: false);

        // Seeding
        policy.Update([dev1, dev2], TimeSpan.FromSeconds(0));

        // Dev1 connects at t=1.0s -> emits
        var events1 = policy.Update([dev1 with { IsConnected = true }, dev2], TimeSpan.FromSeconds(1.0));
        var evt1 = Assert.Single(events1);
        Assert.Equal("Headphones 1", evt1.Name);
        Assert.True(evt1.Connected);

        // Dev2 connects at t=2.0s (1.0s after dev1, but dev2 has independent debounce) -> emits
        var events2 = policy.Update([dev1 with { IsConnected = true }, dev2 with { IsConnected = true }], TimeSpan.FromSeconds(2.0));
        var evt2 = Assert.Single(events2);
        Assert.Equal("Speaker 2", evt2.Name);
        Assert.True(evt2.Connected);

        // Dev1 rapidly disconnects at t=2.5s (1.5s after dev1 connect) -> dev1 suppressed
        var events3 = policy.Update([dev1 with { IsConnected = false }, dev2 with { IsConnected = true }], TimeSpan.FromSeconds(2.5));
        Assert.Empty(events3);
    }

    [Fact]
    public void DeviceDisappearedWhileConnected_EmitsDisconnected()
    {
        var policy = new BluetoothHudPolicy();
        var dev = CreateDevice("dev1", "Sony LinkBuds", BluetoothDeviceKind.Headphones, isConnected: true, batteryPercent: 50);

        // Seeding
        policy.Update([dev], TimeSpan.FromSeconds(0));

        // Device completely disappears from scan at t=5.0s (e.g. out of range / turned off)
        var events = policy.Update([], TimeSpan.FromSeconds(5.0));

        var evt = Assert.Single(events);
        Assert.Equal("Sony LinkBuds", evt.Name);
        Assert.False(evt.Connected);
        Assert.Null(evt.BatteryPercent);
    }

    [Fact]
    public void DualModeDevice_NormalizedByContainerId_TracksUnifiedDevice()
    {
        var policy = new BluetoothHudPolicy();
        var classic = CreateDevice("dev_classic", "Marshall Major IV", BluetoothDeviceKind.Headphones, isConnected: false, containerId: "container-1");
        var le = CreateDevice("dev_le", "Marshall Major IV", BluetoothDeviceKind.Headphones, isConnected: false, containerId: "container-1") with { IsLowEnergy = true };

        // Seeding
        policy.Update([classic, le], TimeSpan.FromSeconds(0));

        // Both update to connected
        var classicConn = classic with { IsConnected = true, BatteryPercent = 75 };
        var leConn = le with { IsConnected = true, BatteryPercent = 75 };
        var events = policy.Update([classicConn, leConn], TimeSpan.FromSeconds(1));

        // Only one normalized event emitted
        var evt = Assert.Single(events);
        Assert.Equal("Marshall Major IV", evt.Name);
        Assert.True(evt.Connected);
        Assert.Equal(75, evt.BatteryPercent);
    }

    [Fact]
    public void FakeClock_TimeProvider_Integration()
    {
        var clock = new FakeClock();
        var policy = new BluetoothHudPolicy(clock);

        var dev = CreateDevice("dev1", "Pixel Buds Pro", BluetoothDeviceKind.Headphones, isConnected: false);

        // Seeding via parameterless Update()
        policy.Update([dev]);

        clock.Advance(TimeSpan.FromSeconds(1));
        var events1 = policy.Update([dev with { IsConnected = true, BatteryPercent = 95 }]);
        var evt1 = Assert.Single(events1);
        Assert.True(evt1.Connected);

        // Flap within 2s -> suppressed
        clock.Advance(TimeSpan.FromSeconds(2));
        var events2 = policy.Update([dev with { IsConnected = false }]);
        Assert.Empty(events2);

        // Advance 2 more seconds (total 4s from connect) -> emits disconnect
        clock.Advance(TimeSpan.FromSeconds(2));
        var events3 = policy.Update([dev with { IsConnected = false }]);
        var evt3 = Assert.Single(events3);
        Assert.False(evt3.Connected);
    }
}
