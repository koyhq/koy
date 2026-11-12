using Windows.Win32;
using Windows.Win32.Foundation;

namespace Koy.Platform.Wireless;

/// <summary>
/// Windows'un kayıtlı Wi-Fi profil adları (wlanapi). WinRT'nin profil listesi yalnız bağlı ağı verdiği için
/// "kayıtlı profili var mı" sorusu için tek yol bu; yalnız adlar okunur, profil XML'ine (şifre) dokunulmaz.
/// </summary>
internal static unsafe class WlanProfiles
{
    // WLAN_PROFILE_INFO_LIST: DWORD sayı, DWORD dizin, ardından WLAN_PROFILE_INFO dizisi.
    // WLAN_PROFILE_INFO: WCHAR[256] ad + DWORD bayraklar.
    private const int HeaderSize = 8;
    private const int NameChars = 256;
    private const int EntrySize = NameChars * 2 + 4;
    private const int MaxItems = 1024;

    public static HashSet<string> GetNames(Guid interfaceId)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        HANDLE client = default;
        try
        {
            uint negotiated = 0;
            var error = PInvoke.WlanOpenHandle(2, null, &negotiated, &client);
            if (error != 0)
                return names;

            Windows.Win32.NetworkManagement.WiFi.WLAN_PROFILE_INFO_LIST* list = null;
            error = PInvoke.WlanGetProfileList(client, &interfaceId, null, &list);
            if (error != 0 || list is null)
                return names;

            try
            {
                var bytes = (byte*)list;
                var count = Math.Min(*(int*)bytes, MaxItems);
                for (var i = 0; i < count; i++)
                {
                    var entry = (char*)(bytes + HeaderSize + (long)i * EntrySize);
                    var length = 0;
                    while (length < NameChars && entry[length] != '\0')
                        length++;

                    if (length > 0)
                        names.Add(new string(entry, 0, length));
                }
            }
            finally
            {
                PInvoke.WlanFreeMemory(list);
            }
        }
        catch
        {
            // wlanapi kullanılamıyorsa (Wlan AutoConfig durdurulmuş) liste kayıtlı profil olmadan gösterilir.
        }
        finally
        {
            if (client != default)
                _ = PInvoke.WlanCloseHandle(client, null);
        }

        return names;
    }
}
