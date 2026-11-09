using System.Runtime.CompilerServices;
using Koy.Core.Diagnostics;

namespace Koy.Core.Tests;

/// <summary>
/// Testler hata yolunu bilerek çalıştırır (ör. kaynak hatası). CrashLog'un yazdığı kayıtlar kullanıcının gerçek
/// %LOCALAPPDATA%\Koy\crash.log dosyasına düşmesin; bütün test derlemesi geçici dosyaya yazar.
/// </summary>
internal static class TestLogRedirect
{
    public static readonly string TestLogPath = Path.Combine(Path.GetTempPath(), "koy-tests", "crash.log");

    [ModuleInitializer]
    internal static void Redirect() => CrashLog.LogPath = TestLogPath;
}
