using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Koy.Platform.Shell;

/// <summary>PKEY_AppUserModel_ID okuyucu: pencere (SHGetPropertyStoreForWindow) ve kısayol (IShellLink'in özellik deposu) için.</summary>
internal static class AppUserModelIdReader
{
    // PKEY_AppUserModel_ID
    private static readonly PROPERTYKEY Key = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    public static string? ForWindow(HWND hwnd)
    {
        try
        {
            PInvoke.SHGetPropertyStoreForWindow(hwnd, typeof(IPropertyStore).GUID, out var obj).ThrowOnFailure();
            if (obj is not IPropertyStore store)
                return null;

            try
            {
                return Read(store);
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Kabuk kısayolu nesnesinin (ShellLink) AUMID'i; nesne IPropertyStore'u destekler.</summary>
    public static string? ForShellLink(object shellLink)
    {
        try
        {
            return shellLink is IPropertyStore store ? Read(store) : null;
        }
        catch
        {
            return null;
        }
    }

    private static unsafe string? Read(IPropertyStore store)
    {
        store.GetValue(Key, out var value);
        try
        {
            PInvoke.PropVariantToStringAlloc(value, out var str).ThrowOnFailure();
            try
            {
                var s = str.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            finally
            {
                PInvoke.CoTaskMemFree(str.Value);
            }
        }
        finally
        {
            PInvoke.PropVariantClear(ref value);
        }
    }
}
