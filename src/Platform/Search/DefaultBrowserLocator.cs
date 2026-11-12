using Koy.Core.Diagnostics;
using Koy.Core.Search;
using Microsoft.Win32;

namespace Koy.Platform.Search;

/// <summary>
/// Kullanıcının varsayılan tarayıcısını kayıt defterinden okur: https (yoksa http) için seçilen ProgId'nin "açma" komutu.
/// Tanıma kararı Core'da (<see cref="DefaultBrowserSearch"/>).
/// </summary>
public static class DefaultBrowserLocator
{
    private static readonly string[] Schemes = ["https", "http"];

    public static DefaultBrowser? Find()
    {
        try
        {
            foreach (var scheme in Schemes)
            {
                using var choice = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\{scheme}\UserChoice");
                if (choice?.GetValue("ProgId") is not string { Length: > 0 } progId)
                    continue;

                using var command = Registry.ClassesRoot.OpenSubKey(progId + @"\shell\open\command");
                if (DefaultBrowserSearch.FromOpenCommand(command?.GetValue(null) as string) is { } browser && File.Exists(browser.Executable))
                    return browser;
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            CrashLog.Write("Varsayılan tarayıcı", ex);
        }

        return null;
    }
}
