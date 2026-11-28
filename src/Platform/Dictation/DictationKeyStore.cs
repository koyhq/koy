using System.Runtime.InteropServices;
using System.Text;
using Koy.Core.Settings;
using Koy.Platform.Calendar;

namespace Koy.Platform.Dictation;

/// <summary>
/// Konuşma tanıma / temizleme API anahtarı. Ayar dosyasında değil, Windows DPAPI ile şifreli ayrı bir dosyada
/// (<c>dictation.key</c>) durur: ayarlar dışa aktarılırken ya da paylaşılırken anahtar sızmaz.
/// Yerelde çalışan bir sunucu anahtar istemez; boş anahtar geçerlidir.
/// </summary>
public static class DictationKeyStore
{
    public static string Path { get; } = JsonListFile.InSettingsFolder("dictation.key");

    /// <summary>Kayıtlı anahtar; dosya yoksa ya da çözülemezse (başka kullanıcı, bozuk dosya) boş.</summary>
    public static string Load()
    {
        try
        {
            if (!File.Exists(Path))
                return "";

            var plain = new DpapiTokenProtector().Unprotect(File.ReadAllBytes(Path));
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException)
        {
            Koy.Core.Diagnostics.CrashLog.Write("DictationKeyStore.Load", ex);
            return "";
        }
    }

    /// <summary>Boş anahtar dosyayı siler. Başarılıysa true.</summary>
    public static bool Save(string key)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                if (File.Exists(Path))
                    File.Delete(Path);
                return true;
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var protectedBytes = new DpapiTokenProtector().Protect(Encoding.UTF8.GetBytes(key.Trim()));
            File.WriteAllBytes(Path, protectedBytes);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException)
        {
            Koy.Core.Diagnostics.CrashLog.Write("DictationKeyStore.Save", ex);
            return false;
        }
    }

    public static bool HasKey() => File.Exists(Path);
}
