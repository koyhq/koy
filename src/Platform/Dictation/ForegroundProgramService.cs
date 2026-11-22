using Koy.Core.Dictation;
using Koy.Platform.Shell;
using Windows.Win32;

namespace Koy.Platform.Dictation;

/// <summary>Önde olan pencerenin programı (küçük harf, .exe'siz) ve başlığı; yazım profili seçmek için, kayıt başlarken bir kez sorulur.</summary>
public sealed class ForegroundProgramService : IForegroundProgram
{
    public unsafe (string Program, string Title) Current()
    {
        var hwnd = PInvoke.GetForegroundWindow();
        if (hwnd.IsNull)
            return ("", "");

        uint pid = 0;
        _ = PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        var path = WindowTracker.GetProcessPath(pid);
        var program = string.IsNullOrEmpty(path) ? "" : Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

        Span<char> buffer = stackalloc char[256];
        int length;
        fixed (char* text = buffer)
        {
            length = PInvoke.GetWindowText(hwnd, text, 256);
        }

        var title = length > 0 ? buffer[..length].ToString() : "";
        return (program, title);
    }
}
