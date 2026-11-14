using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.Dock;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Koy.Platform.Shell;

/// <summary>
/// Kullanıcının görev çubuğuna sabitlediği uygulamaların kısayollarını (.lnk) ve Mağaza (UWP / MSIX) pinlerini
/// (<c>Taskband\Favorites</c> AUMID'leri) okur ve çözümler.
/// </summary>
public sealed class PinnedAppsService : IPinnedAppsService
{
    private static readonly string TaskbarPinnedDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    public IReadOnlyList<PinnedApp> GetPinnedApps()
    {
        var result = new List<PinnedApp>();
        var seenAumids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(TaskbarPinnedDir))
        {
            try
            {
                var files = Directory.GetFiles(TaskbarPinnedDir, "*.lnk");
                foreach (var file in files)
                {
                    var title = Path.GetFileNameWithoutExtension(file);
                    var (targetPath, aumid) = ResolveShortcut(file);
                    if (!string.IsNullOrEmpty(aumid))
                        seenAumids.Add(aumid);

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
                // Klasör okuma veya erişim hatası durumunda kısmi listeyle devam edilir
            }
        }

        ReadPackagedPins(result, seenAumids);
        return result;
    }

    private static void ReadPackagedPins(List<PinnedApp> result, HashSet<string> seenAumids)
    {
        var favorites = TaskbandFavoritesReader.Read();
        var aumids = TaskbandPackagedPins.ExtractAumids(favorites);
        foreach (var aumid in aumids)
        {
            if (!seenAumids.Add(aumid))
                continue;

            var shellPath = TaskbandPackagedPins.AppsFolderPath(aumid);
            var title = ResolveShellItemDisplayName(shellPath);
            if (string.IsNullOrWhiteSpace(title))
                continue;

            var iconData = IconHelper.GetIconBytes(shellPath);
            result.Add(new PinnedApp(
                Title: title,
                ShortcutPath: shellPath,
                TargetPath: shellPath,
                IconData: iconData,
                AppUserModelId: aumid));
        }
    }

    /// <summary>
    /// <c>shell:AppsFolder\&lt;AUMID&gt;</c> öğesini çözümler; uygulama kurulu değilse ya da kabuk öğesi bulunamazsa null döner.
    /// </summary>
    private static unsafe string? ResolveShellItemDisplayName(string shellPath)
    {
        object? obj = null;
        try
        {
            var iid = typeof(IShellItem).GUID;
            fixed (char* pPath = shellPath)
                PInvoke.SHCreateItemFromParsingName(new PCWSTR(pPath), null, &iid, out obj).ThrowOnFailure();

            if (obj is not IShellItem item)
                return null;

            item.GetDisplayName(SIGDN.SIGDN_NORMALDISPLAY, out PWSTR value);
            try
            {
                var name = value.ToString();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            finally
            {
                Marshal.FreeCoTaskMem((nint)value.Value);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (obj is not null && Marshal.IsComObject(obj))
                Marshal.FinalReleaseComObject(obj);
        }
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
