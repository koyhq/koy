namespace Koy.Core.Tray;

/// <summary>
/// Uygulamaların sistem tepsisi simgelerini toplayan servis. Dock Windows görev çubuğunu gizlediğinde
/// tepsiye ulaşmanın tek yolu budur.
/// </summary>
public interface ITrayService : IDisposable
{
    /// <summary>Simge listesi değişti. Arka plan iş parçacığından gelebilir.</summary>
    event EventHandler? IconsChanged;

    /// <summary>Bir tepsi simgesi balon bildirimi gönderdi. Arka plan iş parçacığından gelebilir.</summary>
    event EventHandler<TrayBalloonNotification>? BalloonReceived;

    /// <summary>Gösterilecek simgeler (Koy'un kendi simgeleri hariç), ekleniş sırasıyla.</summary>
    IReadOnlyList<TrayIcon> GetVisibleIcons();

    /// <summary>Simgenin PNG görüntüsü; yoksa null.</summary>
    byte[]? GetIconImage(TrayIconKey key);

    /// <summary>Uygulamaya tepsi tıklamasını iletir ve menüsünü öne getirebilmesine izin verir.</summary>
    void SendMouse(TrayIconKey key, TrayMouseAction action, int cursorX, int cursorY);

    /// <summary>Sahibi kapanmış simgeleri temizler.</summary>
    void RemoveDeadIcons();

    /// <summary>Tepsi gerçekten çalışıyor mu. <see cref="Start"/> başarısız olduysa ya da kapandıysa false.</summary>
    bool IsRunning { get; }

    void Start();

    void Stop();
}
