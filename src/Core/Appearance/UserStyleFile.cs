namespace Koy.Core.Appearance;

/// <summary>Dosyanın değişip değişmediğini anlamak için parmak izi (var mı, boyut, son yazım). İçerik okunmaz.</summary>
public readonly record struct UserStyleFileStamp(bool Exists, long Length, DateTime LastWriteUtc);

/// <summary>
/// %AppData%\Koy\user-theme.json okuma. Dosya yoksa sonuç boştur ve tanı yoktur (özellik açık ama dosya henüz yazılmamış
/// olabilir). Boyut sınırı dosya belleğe alınmadan, uzunluğa bakılarak uygulanır. Okuma hataları tanıya çevrilir, fırlatmaz.
/// </summary>
public static class UserStyleFile
{
    public const string FileName = "user-theme.json";

    public static string DefaultFolder { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Koy");

    public static string DefaultPath { get; } = System.IO.Path.Combine(DefaultFolder, FileName);

    public static UserStyleFileStamp GetStamp(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new UserStyleFileStamp(true, info.Length, info.LastWriteTimeUtc)
                : default;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return default;
        }
    }

    public static UserStyleParseResult Load(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
                return UserStyleParseResult.Empty;

            if (info.Length > UserStyleParser.MaxBytes)
                return Failure(UserStyleDiagnosticCode.FileTooLarge, $"File is larger than {UserStyleParser.MaxBytes / 1024} KB.");

            // Okuma sırasında büyüyen dosya için ikinci sınır: en çok MaxBytes + 1 bayt alınır.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var buffer = new byte[UserStyleParser.MaxBytes + 1];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0)
                    break;
                read += n;
            }

            if (read > UserStyleParser.MaxBytes)
                return Failure(UserStyleDiagnosticCode.FileTooLarge, $"File is larger than {UserStyleParser.MaxBytes / 1024} KB.");

            // Geçersiz UTF-8 dizileri U+FFFD olur; JSON ayrıştırıcısı bozuk içeriği zaten reddeder.
            var text = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
                .GetString(buffer, 0, read);
            return UserStyleParser.Parse(text.TrimStart('﻿'));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Failure(UserStyleDiagnosticCode.FileUnreadable, "The file could not be read.");
        }
    }

    private static UserStyleParseResult Failure(UserStyleDiagnosticCode code, string reason) =>
        new(UserStyleParseResult.Empty.ValidEntries, [new UserStyleDiagnostic(UserStyleParser.FileKey, reason, code)]);
}
