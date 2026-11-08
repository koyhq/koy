using System.Runtime.InteropServices;
using Koy.Core.JumpList;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.System.Com.StructuredStorage;
using Windows.Win32.System.Variant;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Koy.Platform.JumpList;

/// <summary>
/// Atlama listesindeki bir ShellLink akışını ya da kabuk öğesini <see cref="JumpListEntry"/>'ye çevirir. Yalnız
/// <see cref="JumpListService"/>'in ayrı STA iş parçacığında çağrılır; açtığı her COM nesnesini kendisi serbest bırakır.
/// </summary>
internal static class ShellLinkEntryReader
{
    private const int TextBuffer = 1024;

    // PKEY_Title
    private static readonly PROPERTYKEY TitleKey = new()
    {
        fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"),
        pid = 2
    };

    // PKEY_AppUserModel_IsDestListSeparator
    private static readonly PROPERTYKEY SeparatorKey = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 6
    };

    private static readonly Guid ShellLinkClsid = new("00021401-0000-0000-C000-000000000046");

    /// <summary>customDestinations-ms içindeki ShellLink akışını (CLSID'siz) çözer. Atlanacak öğede null.</summary>
    public static unsafe JumpListEntry? FromStream(byte[] linkStream)
    {
        object? link = null;
        IStream? stream = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ShellLinkClsid);
            if (type is null)
                return null;

            link = Activator.CreateInstance(type);
            if (link is not IPersistStream persist)
                return null;

            stream = PInvoke.SHCreateMemStream(linkStream);
            if (stream is null)
                return null;

            persist.Load(stream);
            return FromLink(link);
        }
        catch
        {
            return null; // bozuk akış: öğe atlanır, uydurma öğe yok
        }
        finally
        {
            Release(stream);
            Release(link);
        }
    }

    /// <summary>Yüklenmiş bir IShellLinkW nesnesini girdiye çevirir (nesneyi serbest bırakmaz).</summary>
    public static unsafe JumpListEntry? FromLink(object link)
    {
        var store = link as IPropertyStore;
        var title = Resolve(ReadString(store, TitleKey));
        var separator = ReadBool(store, SeparatorKey);

        var target = string.Empty;
        var args = string.Empty;
        var dir = string.Empty;
        if (link is IShellLinkW shellLink)
        {
            target = ReadLinkText(shellLink, LinkText.Path);
            args = ReadLinkText(shellLink, LinkText.Arguments);
            dir = ReadLinkText(shellLink, LinkText.WorkingDirectory);

            if (string.IsNullOrWhiteSpace(target) && !separator)
                target = ParsingNameOfIdList(shellLink) ?? string.Empty;
        }

        return JumpListEntryFactory.FromLink(title, Environment.ExpandEnvironmentVariables(target), args,
            string.IsNullOrWhiteSpace(dir) ? null : Environment.ExpandEnvironmentVariables(dir), separator);
    }

    /// <summary>Son / sık kullanılan öğe (IShellItem): ad ve çalıştırılacak yol / URL.</summary>
    public static unsafe JumpListEntry? FromShellItem(IShellItem item)
    {
        var title = GetName(item, SIGDN.SIGDN_NORMALDISPLAY);
        var target = GetName(item, SIGDN.SIGDN_FILESYSPATH)
                     ?? GetName(item, SIGDN.SIGDN_URL)
                     ?? GetName(item, SIGDN.SIGDN_DESKTOPABSOLUTEPARSING);
        return JumpListEntryFactory.FromDocument(title, target);
    }

    /// <summary><c>@dll,-id</c> ve <c>@{ms-resource…}</c> dizelerini çözer; çözülemezse boş (başlık uydurulmaz).</summary>
    public static unsafe string? Resolve(string? text)
    {
        if (!JumpListText.IsIndirect(text))
            return text;

        try
        {
            Span<char> buffer = stackalloc char[TextBuffer];
            fixed (char* src = text)
            fixed (char* dst = buffer)
            {
                PInvoke.SHLoadIndirectString(src, dst, TextBuffer, null).ThrowOnFailure();
                var resolved = new string(dst);
                return string.IsNullOrWhiteSpace(resolved) ? null : resolved;
            }
        }
        catch
        {
            return null;
        }
    }

    private enum LinkText { Path, Arguments, WorkingDirectory }

    private static unsafe string ReadLinkText(IShellLinkW link, LinkText which)
    {
        Span<char> buffer = stackalloc char[TextBuffer];
        buffer.Clear();
        try
        {
            fixed (char* p = buffer)
            {
                switch (which)
                {
                    case LinkText.Path: link.GetPath(p, TextBuffer, null, 0); break;
                    case LinkText.Arguments: link.GetArguments(p, TextBuffer); break;
                    default: link.GetWorkingDirectory(p, TextBuffer); break;
                }

                return new string(p);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Hedefi yol değil yalnız kimlik listesi olan kısayol (ör. paketli uygulama): kabuk ayrıştırma adı.</summary>
    private static unsafe string? ParsingNameOfIdList(IShellLinkW link)
    {
        ITEMIDLIST* pidl = null;
        try
        {
            link.GetIDList(&pidl);
            if (pidl is null)
                return null;

            PWSTR name = default;
            PInvoke.SHGetNameFromIDList(pidl, SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, &name).ThrowOnFailure();
            try
            {
                return name.ToString();
            }
            finally
            {
                PInvoke.CoTaskMemFree(name.Value);
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            if (pidl is not null)
                PInvoke.CoTaskMemFree(pidl);
        }
    }

    private static unsafe string? GetName(IShellItem item, SIGDN kind)
    {
        try
        {
            PWSTR name = default;
            item.GetDisplayName(kind, &name);
            try
            {
                var s = name.ToString();
                return string.IsNullOrWhiteSpace(s) ? null : s;
            }
            finally
            {
                PInvoke.CoTaskMemFree(name.Value);
            }
        }
        catch
        {
            return null; // bu biçim bu öğe için yok
        }
    }

    private static unsafe string? ReadString(IPropertyStore? store, PROPERTYKEY key)
    {
        if (store is null)
            return null;

        try
        {
            store.GetValue(key, out var value);
            try
            {
                // Boş (VT_EMPTY) değerlerde dönüştürmeye çalışmak her seferinde istisna fırlatırdı.
                if (value.Anonymous.Anonymous.vt is not (VARENUM.VT_LPWSTR or VARENUM.VT_BSTR))
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
        catch
        {
            return null;
        }
    }

    private static bool ReadBool(IPropertyStore? store, PROPERTYKEY key)
    {
        if (store is null)
            return false;

        try
        {
            store.GetValue(key, out var value);
            try
            {
                return PInvoke.PropVariantToBooleanWithDefault(value, false);
            }
            finally
            {
                PInvoke.PropVariantClear(ref value);
            }
        }
        catch
        {
            return false;
        }
    }

    public static void Release(object? com)
    {
        if (com is not null && Marshal.IsComObject(com))
            Marshal.FinalReleaseComObject(com);
    }
}
