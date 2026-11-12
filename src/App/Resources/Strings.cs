using System.Globalization;
using System.Resources;

namespace Koy.App.Resources;

/// <summary>
/// Yerelleştirilmiş metinlere XAML ve C# üzerinden güçlü tipli erişim sağlar.
/// XAML kullanımı: Text="{x:Static res:Strings.Key}"
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager = new("Koy.App.Resources.Strings", typeof(Strings).Assembly);

    public static string Get(string name) => ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;

    public static string Settings => Get(nameof(Settings));
    public static string Shelf => Get(nameof(Shelf));
    public static string NoMediaPlaying => Get(nameof(NoMediaPlaying));
    public static string ClearAll => Get(nameof(ClearAll));
    public static string DragDropPrompt => Get(nameof(DragDropPrompt));
    public static string Work => Get(nameof(Work));
    public static string ShortBreak => Get(nameof(ShortBreak));
    public static string LongBreak => Get(nameof(LongBreak));
    public static string Cancel => Get(nameof(Cancel));
    public static string General => Get(nameof(General));
    public static string WiFi => Get(nameof(WiFi));
    public static string Bluetooth => Get(nameof(Bluetooth));
    public static string DoNotDisturb => Get(nameof(DoNotDisturb));
    public static string On => Get(nameof(On));
    public static string Off => Get(nameof(Off));
    public static string Charging => Get(nameof(Charging));
    public static string Loading => Get(nameof(Loading));
    public static string Reset => Get(nameof(Reset));
    public static string Start => Get(nameof(Start));
    public static string NextPhase => Get(nameof(NextPhase));
    public static string Dock => Get(nameof(Dock));
    public static string LaunchNewInstance => Get(nameof(LaunchNewInstance));
    public static string DockItemChangeIcon => Get(nameof(DockItemChangeIcon));
    public static string DockItemResetIcon => Get(nameof(DockItemResetIcon));
    public static string Close => Get(nameof(Close));
    public static string EndTask => Get(nameof(EndTask));
    public static string StartMenu => Get(nameof(StartMenu));
    public static string DockAddItem => Get(nameof(DockAddItem));
    public static string DockAddSeparator => Get(nameof(DockAddSeparator));
    public static string DockAddSpacer => Get(nameof(DockAddSpacer));
    public static string DockAddClock => Get(nameof(DockAddClock));
    public static string DockAddTrash => Get(nameof(DockAddTrash));
    public static string DockAddCustom => Get(nameof(DockAddCustom));
    public static string VolumeLevel => Get(nameof(VolumeLevel));
    public static string MonitorSummary => Get(nameof(MonitorSummary));
    public static string VolumeMuted => Get(nameof(VolumeMuted));
    public static string BatteryLevel => Get(nameof(BatteryLevel));
    public static string BatteryCharging => Get(nameof(BatteryCharging));
    public static string NetworkWiFi => Get(nameof(NetworkWiFi));
    public static string NetworkEthernet => Get(nameof(NetworkEthernet));
    public static string NetworkCellular => Get(nameof(NetworkCellular));
    public static string NetworkNoInternet => Get(nameof(NetworkNoInternet));
    public static string NetworkDisconnected => Get(nameof(NetworkDisconnected));
    public static string RadioNotFound => Get(nameof(RadioNotFound));
    public static string RadioNoAccess => Get(nameof(RadioNoAccess));
    public static string RadioDisabled => Get(nameof(RadioDisabled));
    public static string RadioToggleFailed => Get(nameof(RadioToggleFailed));
    public static string RadioOpenSettingsHint => Get(nameof(RadioOpenSettingsHint));
    public static string RadioPrivacyHint => Get(nameof(RadioPrivacyHint));
    public static string OpenInSettings => Get(nameof(OpenInSettings));
    public static string WifiConnect => Get(nameof(WifiConnect));
    public static string WifiDisconnect => Get(nameof(WifiDisconnect));
    public static string WifiPasswordHint => Get(nameof(WifiPasswordHint));
    public static string WifiLocationLink => Get(nameof(WifiLocationLink));
    public static string WifiMoreSettings => Get(nameof(WifiMoreSettings));
    public static string BluetoothMoreSettings => Get(nameof(BluetoothMoreSettings));
    public static string BluetoothConnectedDevices => Get(nameof(BluetoothConnectedDevices));
    public static string BluetoothNoConnected => Get(nameof(BluetoothNoConnected));
    public static string ControlBack => Get(nameof(ControlBack));
    public static string ControlOpenWifiList => Get(nameof(ControlOpenWifiList));
    public static string ControlOpenBluetoothList => Get(nameof(ControlOpenBluetoothList));
}
