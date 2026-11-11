using System.Text.Json.Serialization;
using Koy.Core.Display;
using Koy.Core.Dock;
using Koy.Core.Tray;

namespace Koy.Core.Settings;

// Kaynak üreticili serileştirme: yansıma yok, açılış ve bellek dostu.
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(GeneralSettings))]
[JsonSerializable(typeof(AppearanceSettings))]
[JsonSerializable(typeof(DockSettings))]
[JsonSerializable(typeof(DockItemsSettings))]
[JsonSerializable(typeof(NotchSettings))]
[JsonSerializable(typeof(MusicSettings))]
[JsonSerializable(typeof(DisplaysSettings))]
[JsonSerializable(typeof(ShortcutsSettings))]
[JsonSerializable(typeof(VoiceSettings))]
[JsonSerializable(typeof(SearchSettings))]
[JsonSerializable(typeof(FocusSettings))]
[JsonSerializable(typeof(WeatherSettings))]
[JsonSerializable(typeof(ClipboardSettings))]
[JsonSerializable(typeof(QuickLaunchSettings))]
[JsonSerializable(typeof(ValueList<string>))]
[JsonSerializable(typeof(ValueList<DockFolder>))]
[JsonSerializable(typeof(ValueDictionary<MonitorSettings>))]
[JsonSerializable(typeof(DockFolder))]
[JsonSerializable(typeof(ValueDictionary<TrayIconVisibility>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
