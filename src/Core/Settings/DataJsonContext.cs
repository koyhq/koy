using System.Text.Json.Serialization;
using Koy.Core.Clipboard;
using Koy.Core.Settings.Profiles;
using Koy.Core.Tasks;

namespace Koy.Core.Settings;

// Ayar dışı veri dosyaları için kaynak üreticili serileştirme (yansıma yok).
[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<ClipboardItem>))]
[JsonSerializable(typeof(List<TaskItem>))]
[JsonSerializable(typeof(List<SavedProfile>))]
internal sealed partial class DataJsonContext : JsonSerializerContext;
