namespace Koy.Core.Shell;

/// <summary>
/// Başlat menüsü, sistem tepsisi ve süreç başlatma gibi kabuk eylemleri sözleşmesi.
/// </summary>
public interface IShellActionService
{
    /// <summary>
    /// Windows Başlat menüsünü açmak için Win tuşu girdisi gönderir.
    /// </summary>
    void SendWinKey();

    /// <summary>
    /// Windows Sistem Tepsisi taşma menüsünü açmak için Win+B girdisi gönderir.
    /// </summary>
    void ToggleSystemTray();

    /// <summary>
    /// Windows Hızlı Ayarlar panelini (ağ, ses, pil) açmak için Win+A girdisi gönderir.
    /// </summary>
    void OpenQuickSettings();

    /// <summary>
    /// Windows Bildirim Merkezi ve takvimi açmak için Win+N girdisi gönderir.
    /// </summary>
    void OpenNotificationCenter();

    /// <summary>
    /// Masaüstünü göster / geri getir (Windows "Masaüstünü göster" düğmesi; Win+D girdisi gönderir). Varsayılan gövde boştur:
    /// yalnız gerçek kabuk uygulaması (<c>ShellActionService</c>) bir şey yapar, sahte uygulamaların değişmesi gerekmez.
    /// </summary>
    void ShowDesktop()
    {
    }

    /// <summary>
    /// Belirtilen dosya veya kısayolu çalıştırır.
    /// </summary>
    void Launch(string path, string? arguments = null);
}
