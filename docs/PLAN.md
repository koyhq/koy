# Koy — Hedefler, Mimari, Teknoloji

Windows 10 ve 11 için hafif bir notch (koy/ada) ve görev çubuğunun yerine geçen bir dock.
Nectar'dan ilham alıyor ama kodu, tasarımı ve görselleri bize ait olacak; Nectar'dan kod kopyalanmayacak.

## Verilen kararlar (2026-11-03)

| Konu | Karar |
| --- | --- |
| Kapsam | Notch + Dock. Önce notch, dock sonra. |
| Bellek hedefi | Boşta 100–150 MB (çok ekranla 200 MB'a kadar kabul) → C# + WPF. İlk hedef 60 MB idi; 2026-11-06 kararıyla gevşetildi (bkz. `docs/MEMORY.md`) |
| Windows | Windows 10 2004 (19041) ve üstü, Windows 11 |
| Yayın | Açık kaynak, GitHub, Türkçe + İngilizce |
| Lisans | GPLv3 |
| Hava durumu | Open-Meteo (anahtarsız, ücretsiz) |
| Takvim | v1'de yerel takvim + zamanlayıcı + Pomodoro; Google/Outlook sonraki sürümde |

## 1. Hedefler

### Ne yapacak
- **Notch:** Ekranın üst ortasında, yay animasyonuyla büyüyüp küçülen ada. Kaydırarak panel değişir.
  - Müzik: kapak, şarkı bilgisi, kontroller, birden fazla çalan uygulama arasında geçiş.
  - Kontrol Merkezi: Wi-Fi, Bluetooth, Rahatsız Etme, ses, parlaklık.
  - Durum: pil, hava durumu.
  - Takvim: gün/hafta görünümü, zamanlayıcı, Pomodoro.
  - Raf: dosya ya da metin bırak, sonra sürükleyip al.
- **Dock:** Windows görev çubuğunu gizleyip yerine geçer. Sabitlenmiş uygulamalar, açık pencereler, sistem tepsisi.
- **Görünürlük:** Sabit, Akıllı (üstüne pencere gelince gizlenir) ve Gözetle (kenara gelince çıkar) modları. Tam ekranda ikisi de gizlenir.
- **Ayarlar:** Ayar penceresi, ayrıca düz bir `settings.json`.

### Ölçülebilir hedefler
| Hedef | Değer | Nasıl ölçülür |
| --- | --- | --- |
| Bellek (boşta) | 100–150 MB private bytes (200 MB'a kadar kabul) | Process Explorer / `dotnet-counters` / `scripts/measure-memory.ps1` |
| CPU (boşta) | ~%0 | Görev Yöneticisi, 1 dk ortalama |
| Açılış | < 1 sn | Uygulama içi zaman ölçümü |
| Animasyon | 60 fps, takılma yok | PerfView / göz |
| Güvenlik | Çökse bile görev çubuğu geri gelir | Elle test: işlemi öldür, yeniden aç |

### Şimdilik yapılmayacaklar
- Windows 10, macOS, Linux.
- Eklenti sistemi, tema mağazası.
- Bulut hesabı, telemetri.

## 2. Teknoloji

| Alan | Seçim | Neden |
| --- | --- | --- |
| Çalışma zamanı | .NET 10 (LTS), C# | Uzun destek, projenin ana dili |
| Arayüz | WPF | Olgun, hafif, tarayıcı yok; WinUI 3'ün paketleme dertleri yok |
| MVVM | CommunityToolkit.Mvvm | Kaynak üreticisiyle az kod, sıfır yansıma |
| Windows API (WinRT) | `net10.0-windows10.0.22621.0` hedefi | Medya, radyo, pil API'leri doğrudan |
| Win32 çağrıları | Microsoft.Windows.CsWin32 | P/Invoke'ları elle yazmadan, tip güvenli |
| Ses | CoreAudio (CsWin32 ile COM) | Ek kütüphane yok |
| Ayarlar | System.Text.Json (kaynak üreticili) | Hızlı, yansımasız |
| Dil | .resx (tr, en) | WPF'in yerleşik yolu, tip güvenli erişim |
| Kurulum + güncelleme | Velopack | Kurulum dosyası + GitHub Releases'tan otomatik güncelleme |
| Test | xUnit | Çekirdek mantık (fizik, ayarlar, durum makineleri) |
| CI | GitHub Actions | Derle, test et, sürüm çıkar |

Bilerek **kullanmadıklarımız:** WebView2, Electron/Tauri (bellek), büyük DI ve loglama kütüphaneleri (Serilog vb.), Windows Forms.

## 3. Mimari

### Proje yapısı
```
src/
  App/        WPF uygulaması: pencereler, görünümler, ViewModel'ler, kaynaklar
  Core/       Saf C# (WPF yok): modeller, ayarlar, yay fiziği, görünürlük durum makinesi
  Platform/   Windows API sarmalayıcıları (servisler)
tests/
  Core.Tests/
```
`Core` hiçbir Windows API'sine dokunmaz; bu yüzden test edilebilir. `Platform` her Windows özelliğini küçük bir servisin arkasına saklar.

### Tek süreç, az pencere
Nectar'ın belleğinin asıl sebebi pencere başına ayrı tarayıcı süreci. Biz **tek süreçte** çalışırız:
- Her monitör için bir `NotchWindow` ve bir `DockWindow`; hepsi aynı süreçte, aynı kaynakları paylaşır.
- Ayarlar penceresi yalnız açılınca oluşturulur, kapanınca bellekten atılır.

### Servisler (Platform)
| Servis | Görev | Windows API |
| --- | --- | --- |
| `MediaService` | Çalan şarkı, kapak, oynat/durdur | `GlobalSystemMediaTransportControlsSessionManager` |
| `AudioService` | Ses seviyesi, sessiz | CoreAudio `IAudioEndpointVolume` |
| `RadioService` | Wi-Fi, Bluetooth aç/kapa | `Windows.Devices.Radios` |
| `BrightnessService` | Parlaklık | WMI (dahili), DDC/CI (harici) |
| `PowerService` | Pil | `Windows.System.Power` |
| `FullscreenService` | Tam ekran uygulama algılama | `SHQueryUserNotificationState` + pencere boyutu |
| `WindowTracker` | Açık pencereler, odak değişimi | `SetWinEventHook` |
| `AppBarService` | Dock için ekranda yer ayırma | `SHAppBarMessage` |
| `TaskbarService` | Windows görev çubuğunu gizle / geri getir | `Shell_TrayWnd` |
| `PinnedAppsService` | Sabitlenmiş uygulamalar | `User Pinned\TaskBar` kısayolları |

Kural: **servis yalnız gerektiğinde çalışır.** Notch gizliyken müzik görselleştiricisi, fare takibi ve küçük resim üretimi durur.

### Veri akışı
```
Windows olayı → Servis (arka plan) → olay → ViewModel (UI iş parçacığına geçer) → Görünüm
Kullanıcı tıklar → ViewModel komutu → Servis → Windows API
```

### Animasyon
- Kendi `SpringAnimator` sınıfımız (`Core` içinde, test edilebilir): kütle, sertlik, sönüm.
- `CompositionTarget.Rendering` yalnız animasyon sürerken bağlanır; bitince çözülür. Boşta kare çizilmez, CPU harcanmaz.

### Görev çubuğu güvenliği
- Açılışta: önceki oturum çöktüyse görev çubuğu önce geri getirilir.
- Kapanışta ve yakalanmamış hatada: görev çubuğu geri getirilir.
- Kaldırmada: kurulum programı görev çubuğunu geri getirir.

### Bellek teknikleri
- **Yazılımla çizim** (`RenderMode.SoftwareOnly`): GPU sürücüsü sürece ~135 MB ekliyordu (ölçüm `docs/MEMORY.md`).
- Workstation GC, eşzamanlı GC kapalı; GC ayarları ölçüme göre düzenlenir.
- Albüm kapakları gösterilecek boyutta çözülür (`DecodePixelWidth`), dondurulur (`Freeze`).
- Tek bir `ResourceDictionary`; stiller paylaşılır, pencere başına kopyalanmaz.
- Her aşamanın sonunda bellek ölçülür, 200 MB'ı aşan değişiklik birleştirilmez.

## 4. Aşamalar

| Aşama | İçerik | Bitti sayılır |
| --- | --- | --- |
| M0 ✅ | Çözüm iskeleti, CI, boş notch penceresi | Ölçüldü: 34 MB private |
| M1 | Notch kabuğu + yay animasyonu + Müzik paneli + görünürlük modları | Spotify / tarayıcı müziği kontrol ediliyor, < 60 MB |
| M2 | Kontrol Merkezi + Durum | Wi-Fi, BT, ses, parlaklık çalışıyor |
| M3 | Takvim, Pomodoro, Raf, Ayarlar penceresi, tr/en | Ayarlar `settings.json`'a yazılıyor |
| M4 | Dock: görev çubuğunu değiştirme, sabitli uygulamalar, açık pencereler | Görev çubuğu her durumda geri geliyor |
| M5 | Çoklu monitör, Velopack kurulum + güncelleme, ilk GitHub sürümü | v0.1.0 yayında |

## 5. Riskler

| Risk | Etkisi | Önlem |
| --- | --- | --- |
| Dock'un görev çubuğunu değiştirmesi zor | Yüksek | Dock en sona; görev çubuğu geri getirme ilk günden test edilir |
| Sistem tepsisi simgeleri için resmi API yok | Orta | Koy kendi `Shell_TrayWnd` penceresini kurar, kayıtları toplar ve Windows'a da iletir (PR #11; yalnız görev çubuğu gizliyken, elle denenmedi) |
| Sabitli uygulama sırası belgelenmemiş kayıt defterinde | Orta | v1'de kısayol klasörü sırası; sonra iyileştirme |
| WPF'te akıcı yay animasyonu | Orta | M1'de erken prototip, fps ölçümü |
| İmzasız kurulum dosyasına SmartScreen uyarısı | Düşük | SHA256 listesi; ileride kod imzalama |

## 6. Alınan İsim Kararı (2026-11-04)
- **Proje adı:** **Koy** (`Koy.slnx`, `Koy.*` namespace, `%AppData%\Koy`). Detaylar: `docs/NAMING.md`.
