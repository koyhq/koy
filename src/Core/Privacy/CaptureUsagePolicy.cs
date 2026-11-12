namespace Koy.Core.Privacy;

/// <summary>
/// Kamera ve mikrofon gizlilik göstergesi durumu.
/// </summary>
public sealed record PrivacyState(
    bool IsCameraInUse,
    bool IsMicrophoneInUse,
    string? ActiveCameraApp,
    string? ActiveMicrophoneApp)
{
    public static readonly PrivacyState Empty = new(false, false, null, null);

    public bool AnyInUse => IsCameraInUse || IsMicrophoneInUse;
}

/// <summary>
/// Windows ConsentStore kayıtlarına göre kamera ve mikrofon kullanımını değerlendiren saf mantık.
/// </summary>
public static class CaptureUsagePolicy
{
    /// <summary>
    /// Verilen başlangıç ve bitiş zamanlarına (FILETIME) göre aygıtın şu an aktif kullanımda olup olmadığını belirler.
    /// Başlangıç zamanı > 0 ve (bitiş zamanı 0 veya başlangıç zamanı > bitiş zamanı) ise kullanım aktiftir.
    /// </summary>
    public static bool IsActivelyUsing(long startFileTime, long stopFileTime)
    {
        if (startFileTime <= 0)
            return false;

        return stopFileTime == 0 || startFileTime > stopFileTime;
    }

    /// <summary>
    /// Aktif duruma göre kullanıcıya gösterilecek ToolTip metnini üretir. Kalıplar ve "bilinmeyen uygulama" adı
    /// arayüz katmanından gelir (Core resx'e erişemez).
    /// </summary>
    public static string FormatToolTip(
        PrivacyState state,
        string formatCamAndMic,
        string formatCam,
        string formatMic,
        string unknownApp)
    {
        var cam = state.ActiveCameraApp ?? unknownApp;
        var mic = state.ActiveMicrophoneApp ?? unknownApp;

        if (state.IsCameraInUse && state.IsMicrophoneInUse)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, formatCamAndMic, cam, mic);

        if (state.IsCameraInUse)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, formatCam, cam);

        if (state.IsMicrophoneInUse)
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, formatMic, mic);

        return string.Empty;
    }

    /// <summary>
    /// ConsentStore alt anahtar adından okunabilir uygulama adını ayıklar.
    /// Paket adı (UWP) veya NonPackaged yol (# ile ayrılmış) desteklenir. Ad yoksa null döner.
    /// </summary>
    public static string? ExtractAppNameFromKey(string? keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            return null;

        var clean = keyName.Trim();

        // NonPackaged formatı: "C:#Program Files#obs-studio#bin#64bit#obs64.exe"
        var hashIdx = clean.LastIndexOf('#');
        if (hashIdx >= 0 && hashIdx < clean.Length - 1)
        {
            var fileName = clean[(hashIdx + 1)..];
            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^4];
            return fileName;
        }

        // Standart dosya yolu: "C:\path\to\app.exe"
        var slashIdx = Math.Max(clean.LastIndexOf('\\'), clean.LastIndexOf('/'));
        if (slashIdx >= 0 && slashIdx < clean.Length - 1)
        {
            var fileName = clean[(slashIdx + 1)..];
            if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^4];
            return fileName;
        }

        // UWP paket adı: "Microsoft.WindowsCamera_8wekyb3d8bbwe!App" veya "5319275A.WhatsAppDesktop_cv1g1gvanyjgm"
        var exclamIdx = clean.IndexOf('!');
        if (exclamIdx > 0)
            clean = clean[..exclamIdx];

        var underIdx = clean.IndexOf('_');
        if (underIdx > 0)
        {
            clean = clean[..underIdx];
            var dotIdx = clean.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < clean.Length - 1)
                clean = clean[(dotIdx + 1)..];
        }

        return clean;
    }
}
