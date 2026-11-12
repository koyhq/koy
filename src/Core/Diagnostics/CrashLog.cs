using System.Text;

namespace Koy.Core.Diagnostics;

/// <summary>
/// Yakalanmamış hataları %LOCALAPPDATA%\Koy\crash.log dosyasına yazar.
/// Hiçbir koşulda istisna fırlatmaz: log yazımı başarısız olursa sessizce vazgeçer ki çökme akışı bozulmasın.
/// </summary>
public static class CrashLog
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Koy",
        "crash.log");

    /// <summary>Write/Guard'ın yazdığı dosya. Testler geçici dosyaya yönlendirir.</summary>
    public static string LogPath { get; set; } = DefaultPath;

    /// <summary>Tek kayıt metni: zaman, kaynak, istisna tipi, mesaj ve yığın (iç istisnalar dahil).</summary>
    public static string Format(string source, object? error, DateTime now)
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append("] ").AppendLine(source);

        if (error is Exception ex)
        {
            sb.Append("Tip: ").AppendLine(ex.GetType().FullName);
            sb.Append("Mesaj: ").AppendLine(ex.Message);
            sb.AppendLine("Yığın:");
            sb.AppendLine(ex.ToString());
        }
        else
        {
            sb.Append("Nesne: ").AppendLine(error?.ToString() ?? "(boş)");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    public static void Write(string source, object? error) => WriteTo(LogPath, source, error);

    public static void WriteTo(string path, string source, object? error)
    {
        try
        {
            var text = Format(source, error, DateTime.Now);
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                // Dosya sonsuz büyümesin: eşik aşılınca eskisini .old yap.
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                    File.Move(path, path + ".old", overwrite: true);

                File.AppendAllText(path, text);
            }
        }
        catch
        {
            // Log yazılamıyorsa yapılacak bir şey yok; çökme akışını bozma.
        }
    }

    /// <summary>
    /// Win32 geri çağrıları (WndProc, WinEvent, zamanlayıcı) içinde çalıştır: istisna loga yazılır, işletim sistemine sızmaz.
    /// Sızarsa süreç STATUS_FATAL_USER_CALLBACK_EXCEPTION ile düşer.
    /// </summary>
    public static void Guard(string source, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Write(source, ex);
        }
    }
}
