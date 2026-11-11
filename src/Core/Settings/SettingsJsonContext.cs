using System.Text.Json.Serialization;
using Koy.Core.Display;
using Koy.Core.Dock;

namespace Koy.Core.Settings;

// Kaynak üreticili serileştirme: yansıma yok, açılış ve bellek dostu.
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(DockStyleMode))]
[JsonSerializable(typeof(DockIconAlignment))]
[JsonSerializable(typeof(DockProximityMode))]
[JsonSerializable(typeof(DockStartIcon))]
[JsonSerializable(typeof(Koy.Core.Search.SearchHistoryMode))]
[JsonSerializable(typeof(Koy.Core.Voice.VoiceHarnessType))]
[JsonSerializable(typeof(Koy.Core.Voice.VoiceClaudePermission))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(DockFolder))]
[JsonSerializable(typeof(List<DockFolder>))]
[JsonSerializable(typeof(Dictionary<string, MonitorSettings>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
