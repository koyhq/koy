using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;
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
            {
                if (obj is not null && Marshal.IsComObject(obj))
                    Marshal.ReleaseComObject(obj);
                return null;
            }

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

    /// <summary>
    /// Paketli (MSIX / Mağaza) sürecin uygulama kimliği. Bu uygulamalar pencereye değil pakete AUMID verir
    /// (<c>Aile_yayıncı!Uygulama</c>). Paketsiz süreçte APPMODEL_ERROR_NO_APPLICATION döner: null.
    /// </summary>
    public static unsafe string? ForProcess(uint pid)
    {
        if (pid == 0)
            return null;

        try
        {
            using var process = PInvoke.OpenProcess_SafeHandle(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process.IsInvalid)
                return null;

            Span<char> buffer = stackalloc char[130]; // APPLICATION_USER_MODEL_ID_MAX_LENGTH = 130
            var length = (uint)buffer.Length;
            fixed (char* p = buffer)
            {
                if (PInvoke.GetApplicationUserModelId(process, ref length, p) != WIN32_ERROR.ERROR_SUCCESS || length < 2)
                    return null;
            }

            return buffer[..(int)(length - 1)].ToString(); // sondaki NUL hariç
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
            // AUMID'siz pencerelerde değer VT_EMPTY gelir; dönüştürmeye çalışmak her seferinde istisna fırlatırdı.
            if (value.Anonymous.Anonymous.vt != Windows.Win32.System.Variant.VARENUM.VT_LPWSTR)
                return null;

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
