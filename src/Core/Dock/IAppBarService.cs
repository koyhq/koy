namespace Koy.Core.Dock;

/// <summary>
/// Dock penceresini Windows AppBar olarak kaydederek ekranın altında çalışma alanı ayırma sözleşmesi.
/// </summary>
public interface IAppBarService : IDisposable
{
    /// <summary>
    /// Pencereyi ekranın altında AppBar olarak kaydeder ve çalışma alanını ayırır.
    /// </summary>
    void Register(nint windowHandle, double heightDip, double dpiScale = 1.0);

    /// <summary>
    /// Ekran veya çözünürlük değiştiğinde AppBar konumunu günceller.
    /// </summary>
    void UpdatePosition(nint windowHandle, double heightDip, double dpiScale = 1.0);

    /// <summary>
    /// Kaydı sıfırdan yapar. Explorer yeniden başlayınca eski kayıtlar kaybolur; çalışma alanı yeniden ayrılmalıdır.
    /// </summary>
    void Reregister(nint windowHandle, double heightDip, double dpiScale = 1.0);

    /// <summary>
    /// AppBar kaydını kaldırır ve masaüstü çalışma alanını eski haline getirir.
    /// </summary>
    void Unregister(nint windowHandle);
}
