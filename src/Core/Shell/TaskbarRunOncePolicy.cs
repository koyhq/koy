namespace Koy.Core.Shell;

/// <summary>
/// Yedek kurtarma: görev çubuğu gizlenince HKCU RunOnce altına gözcüyü "yalnız geri getir" kipinde çalıştıran bir kayıt
/// yazılır, temiz geri yüklemede silinir. Koy ve gözcü birlikte öldürülür ya da oturum kapanırsa bir sonraki oturum
/// açılışında Windows bu kaydı çalıştırır (RunOnce yalnız oturum açılışında çalışır ve kendi kendini siler).
/// </summary>
public static class TaskbarRunOncePolicy
{
    public const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    public const string ValueName = "KoyTaskbarRestore";

    /// <summary>RunOnce komut satırı: tırnaklı gözcü yolu ve geri getirme argümanı.</summary>
    public static string BuildCommand(string watchdogExePath)
        => "\"" + watchdogExePath + "\" " + WatchdogPolicy.RestoreOnlyArgument;

    /// <summary>Kayıt yazılmalı mı: yok ya da farklıysa (yol değişmiş olabilir) evet.</summary>
    public static bool NeedsWrite(string? existing, string desired)
        => !string.Equals(existing, desired, StringComparison.OrdinalIgnoreCase);
}
