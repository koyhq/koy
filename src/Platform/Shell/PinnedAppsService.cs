using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.Dock;
using Windows.Win32;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Koy.Platform.Shell;

/// <summary>
/// Kullanıcının görev çubuğuna sabitlediği uygulamaların kısayollarını okur ve çözümler.
/// </summary>
public sealed class PinnedAppsService : IPinnedAppsService
{
    private static readonly string TaskbarPinnedDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    public IReadOnlyList<PinnedApp> GetPinnedApps()
    {
        var result = new List<PinnedApp>();

        if (!Directory.Exists(TaskbarPinnedDir))
            return result;

        try
        {
            var files = Directory.GetFiles(TaskbarPinnedDir, "*.lnk");
            foreach (var file in files)
            {
                var title = Path.GetFileNameWithoutExtension(file);
                var (targetPath, aumid) = ResolveShortcut(file);
                var iconData = IconHelper.GetIconBytes(file) ??
                               (!string.IsNullOrEmpty(targetPath) ? IconHelper.GetIconBytes(targetPath) : null);

                result.Add(new PinnedApp(
                    Title: title,
                    ShortcutPath: file,
                    TargetPath: targetPath,
                    IconData: iconData,
                    AppUserModelId: aumid));
            }
        }
        catch
        {
            // Klasör okuma veya erişim hatası durumunda boş/kısmi liste döner
        }

        return result;
    }

    /// <summary>Kısayolun hedef yolu ve AppUserModelID'si. File Explorer gibi kabuk öğesi kısayollarında hedef boştur, AUMID dolu gelir.</summary>
    private static unsafe (string? Target, string? Aumid) ResolveShortcut(string shortcutPath)
    {
        object? link = null;
        try
        {
            var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
            if (shellLinkType is null)
                return (null, null);

            link = Activator.CreateInstance(shellLinkType);
            if (link is IPersistFile persistFile && link is IShellLinkW shellLink)
            {
                fixed (char* pFile = shortcutPath)
                {
                    persistFile.Load(pFile, 0);
                }

                Span<char> buffer = stackalloc char[260];
                fixed (char* pBuf = buffer)
                {
                    shellLink.GetPath(pBuf, 260, null, 0);
                    var str = new string(pBuf);
                    return (!string.IsNullOrWhiteSpace(str) ? str : null, AppUserModelIdReader.ForShellLink(link));
                }
            }
        }
        catch
        {
            // Kısayol bozuksa veya çözümlenemiyorsa boş döner
        }
        finally
        {
            // Her yenilemede onlarca kısayol çözülür; RCW'yi GC'ye bırakmadan hemen serbest bırak (COM sızıntısı).
            if (link is not null && Marshal.IsComObject(link))
                Marshal.FinalReleaseComObject(link);
        }

        return (null, null);
    }
}
