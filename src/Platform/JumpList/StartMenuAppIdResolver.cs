using System.IO;
using System.Runtime.InteropServices;
using Koy.Core.JumpList;
using Koy.Platform.Shell;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;

namespace Koy.Platform.JumpList;

/// <summary>
/// Paketsiz uygulamanın (Discord gibi, AUMID'i süreç düzeyinde koyan) AppUserModelID'ini Başlat menüsü kısayolundan bulur:
/// hedefi exe olan (ya da Squirrel <c>Update.exe --processStart</c> düzeni) ve AUMID'i olan ilk kısayol. Yalnız sağ tıkta
/// ve <see cref="JumpListService"/>'in kısa ömürlü STA iş parçacığında çağrılır (ShellLink COM'u apartman ister). Tarama
/// sınırlıdır: en çok <see cref="MaxShortcuts"/> kısayol, <see cref="MaxDepth"/> klasör derinliği.
/// </summary>
internal static class StartMenuAppIdResolver
{
    public const int MaxShortcuts = 2000;
    public const int MaxDepth = 4;

    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");
    private const int TextBuffer = 1024;

    public static string? Resolve(string exePath)
    {
        var roots = new List<string>(2);
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.StartMenu));
        AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu));

        object? link = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ShellLinkClsid);
            if (type is null)
                return null;

            link = Activator.CreateInstance(type);
            if (link is not IShellLinkW shellLink || link is not IPersistFile file)
                return null;

            string? squirrel = null;
            var budget = MaxShortcuts;
            foreach (var root in roots)
            {
                foreach (var path in Enumerate(root, 0, () => budget > 0))
                {
                    budget--;
                    if (!TryRead(file, shellLink, path, out var target, out var args))
                        continue;

                    var exact = ShortcutAppIdMatcher.MatchesExact(exePath, target);
                    if (!exact && (squirrel is not null || !ShortcutAppIdMatcher.MatchesSquirrel(exePath, target, args)))
                        continue;

                    var aumid = ReadAumid(type, path);
                    if (aumid is null)
                        continue;

                    if (exact)
                        return aumid;
                    squirrel = aumid;
                }
            }

            return squirrel;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (link is not null && Marshal.IsComObject(link))
                Marshal.FinalReleaseComObject(link);
        }
    }

    /// <summary>
    /// Eşleşen kısayolun AUMID'i. Özellik deposu ShellLink nesnesinde önbelleklendiğinden (sonraki Load'larda
    /// sıfırlanmıyor) her eşleşme için taze nesne kullanılır.
    /// </summary>
    private static unsafe string? ReadAumid(Type shellLinkType, string path)
    {
        object? fresh = null;
        try
        {
            fresh = Activator.CreateInstance(shellLinkType);
            if (fresh is not IPersistFile file)
                return null;

            fixed (char* pp = path)
                file.Load(pp, STGM.STGM_READ);
            return AppUserModelIdReader.ForShellLink(fresh);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (fresh is not null && Marshal.IsComObject(fresh))
                Marshal.FinalReleaseComObject(fresh);
        }
    }

    private static void AddRoot(List<string> roots, string startMenu)
    {
        if (string.IsNullOrEmpty(startMenu))
            return;

        var programs = Path.Combine(startMenu, "Programs");
        if (Directory.Exists(programs))
            roots.Add(programs);
    }

    private static IEnumerable<string> Enumerate(string dir, int depth, Func<bool> hasBudget)
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(dir, "*.lnk");
        }
        catch
        {
            yield break;
        }

        foreach (var f in files)
        {
            if (!hasBudget())
                yield break;
            yield return f;
        }

        if (depth >= MaxDepth)
            yield break;

        string[] subs;
        try
        {
            subs = Directory.GetDirectories(dir);
        }
        catch
        {
            yield break;
        }

        foreach (var sub in subs)
        {
            if (!hasBudget())
                yield break;
            foreach (var f in Enumerate(sub, depth + 1, hasBudget))
                yield return f;
        }
    }

    private static unsafe bool TryRead(IPersistFile file, IShellLinkW link, string path, out string target, out string args)
    {
        target = args = string.Empty;
        try
        {
            fixed (char* pp = path)
                file.Load(pp, STGM.STGM_READ);
            Span<char> buffer = stackalloc char[TextBuffer];
            buffer.Clear();
            fixed (char* p = buffer)
            {
                link.GetPath(p, TextBuffer, null, 0);
                target = new string(p);
            }

            buffer.Clear();
            fixed (char* p = buffer)
            {
                link.GetArguments(p, TextBuffer);
                args = new string(p);
            }

            return target.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
