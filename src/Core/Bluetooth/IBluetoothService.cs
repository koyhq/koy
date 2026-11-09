namespace Koy.Core.Bluetooth;

/// <summary>
/// Eşleşmiş Bluetooth cihazlarının listesi, bağlantı durumu ve pili. Servis yaşadığı sürece cihaz izleyicisi çalışır
/// (olay tabanlı, yoklama yok). Çentik bağlan/kes göstergesi için servisi sürekli tutar; liste sayfaları yalnız görünürken alır.
/// </summary>
public interface IBluetoothService : IDisposable
{
    /// <summary>Şu anki cihazlar (ham; birleştirme ve sıralama <see cref="BluetoothList"/> ile).</summary>
    IReadOnlyList<BluetoothDeviceInfo> Devices { get; }

    /// <summary>Başlangıç taraması bitti mi (liste güvenilir).</summary>
    bool IsReady { get; }

    /// <summary>Cihaz eklendi, çıktı ya da özelliği (bağlantı, pil) değişti. Arka plan iş parçacığından gelir.</summary>
    event EventHandler? DevicesChanged;

    /// <summary>Ses cihazına bağlanmayı dener ve sonucu bağlantı durumundan okur.</summary>
    Task<BluetoothActionResult> ConnectAsync(string deviceId);

    /// <summary>Ses cihazının bağlantısını keser ve sonucu bağlantı durumundan okur.</summary>
    Task<BluetoothActionResult> DisconnectAsync(string deviceId);

    /// <summary>
    /// GATT pil okumasını açar ya da kapatır. Yalnız Bluetooth listesini izleyen bir sayfa (Ana, Kontrol) açıkken açık olmalı;
    /// servis çentikte kalıcı tutulsa da okuma yapılmaz (AGENTS.md kural #5).
    /// </summary>
    void SetBatteryReadsEnabled(bool enabled);

    /// <summary>Windows'ta Bluetooth ayarlarını açar.</summary>
    void OpenSettings();
}
