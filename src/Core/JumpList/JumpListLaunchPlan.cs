namespace Koy.Core.JumpList;

/// <summary>Bir girdinin çalıştırılma biçimi: <see cref="File"/> kabukla (ShellExecute) açılır.</summary>
public sealed record JumpListLaunchPlan(string File, string? Arguments, string? WorkingDirectory)
{
    /// <summary>Paketli uygulamaların pencere sahipleri: exe yolu uygulamayı temsil etmez, belge bu exe ile açılmaz.</summary>
    private static readonly string[] HostExecutables = ["ApplicationFrameHost.exe"];

    /// <summary>
    /// Çalıştırma planı. Görev kısayolu olduğu gibi çalışır. Belge girdisi (son / sık kullanılan) mümkünse uygulamanın
    /// kendi exe'siyle açılır: Windows'un atlama listesi de belgeyi o uygulamada açar (ör. VS Code'un son klasörü
    /// Gezgin'de değil VS Code'da açılmalı). Kabuk kısayolu (.lnk, .url), exe, paketli uygulama yolu ve exe'si bilinmeyen
    /// uygulamalarda hedef varsayılan ilişkiyle açılır.
    /// </summary>
    public static JumpListLaunchPlan For(JumpListLaunch launch, string? appExePath)
    {
        if (!launch.IsDocument || !CanOpenWithApp(launch.Target, appExePath))
            return new JumpListLaunchPlan(launch.Target, launch.Arguments, launch.WorkingDirectory);

        // Sondaki ters bölü (D:\) kapanış tırnağını kaçırır; komut satırı kuralı gereği ikilenir.
        var target = launch.Target.EndsWith('\\') ? launch.Target + "\\" : launch.Target;
        return new JumpListLaunchPlan(appExePath!, "\"" + target + "\"", launch.WorkingDirectory);
    }

    private static bool CanOpenWithApp(string target, string? appExePath)
    {
        if (string.IsNullOrWhiteSpace(appExePath) ||
            !appExePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            appExePath.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase))
            return false;

        var exeName = Path.GetFileName(appExePath);
        if (HostExecutables.Any(h => string.Equals(h, exeName, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Tırnak içine alacağımız hedefte tırnak olamaz (Windows yollarında zaten geçersiz); URL olmayan kabuk adlarını
        // (::{CLSID}, shell:) ve kısayol / program dosyalarını ilişkilendirmeye bırak.
        if (target.Contains('"') ||
            target.StartsWith("::", StringComparison.Ordinal) ||
            target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return false;

        var ext = Path.GetExtension(target);
        return !ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase) &&
               !ext.Equals(".url", StringComparison.OrdinalIgnoreCase) &&
               !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }
}
