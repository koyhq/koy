namespace Koy.Core.Media;

/// <summary>
/// Bir video dosyasından sırayla kare okuyan çözücü. Kareler BGRA32 (alfa 255), üst satır önce, satır adımı
/// <c>Width * 4</c>. Dosya bitince başa sarar, döngü kesintisiz devam eder. Tek iş parçacığından kullanılır.
/// Uygulama (Platform) yalnız yazılımla çözer: ekran kartı ve Direct3D kullanılmaz.
/// </summary>
public interface IVideoFrameReader : IDisposable
{
    int Width { get; }
    int Height { get; }

    /// <summary>
    /// Sıradaki kareyi <paramref name="bgra"/>'ya yazar (en az <c>Width * Height * 4</c> bayt). Dosya sonunda başa sarar.
    /// Okunamazsa false döner.
    /// </summary>
    /// <param name="timestamp">Karenin dosyadaki zamanı; başa sarınca yeniden sıfırdan başlar.</param>
    bool ReadNextFrame(Span<byte> bgra, out TimeSpan timestamp);
}

/// <summary>Dosyadan <see cref="IVideoFrameReader"/> açar; dosya açılamaz ya da çözülemezse null.</summary>
public interface IVideoFrameReaderFactory
{
    IVideoFrameReader? Open(string path);
}
