# ROADMAP — dosya dosya yapılacaklar

Her aşama kendi dalında yapılır, sonunda bellek ölçülür (`docs/MEMORY.md`).
Kutuları iş bittikçe işaretle. Dosya yolları önerilir; daha iyi bir ad varsa kullan ama yapıyı (Core / Platform / App) bozma.

---

## M0 — İskelet ✅ (2026-11-03)
- [x] Çözüm `Koy.slnx`, 4 proje, `global.json` (.NET 10), `Directory.Build.props`, `.editorconfig`, CI (`.github/workflows/ci.yml`)
- [x] `Core/Animation/Spring.cs` + testler
- [x] `Core/Settings/AppSettings.cs`, `SettingsStore.cs`, `SettingsJsonContext.cs` + testler
- [x] `Platform/Shell/TaskbarService.cs` (gizle / geri getir)
- [x] `Platform/Media/IMediaService.cs` (yalnız arayüz)
- [x] `App/Features/Notch/NotchWindow` — üst ortada hap, fareyle yay animasyonlu büyüme, sağ tık → Çıkış
- [x] Bellek ölçümü: 34 MB private (yazılımla çizim)
- [x] Mimari düzeltme (2026-11-04): `IMediaService` → `Core/Media/`; App `Features/` yapısına geçti (`IFeature`, `Features/Notch/NotchFeature`); `AppSettings.NotchEnabled / DockEnabled`; notch sabit tuval 500×260. Kurallar AGENTS.md #11–13.

> **Yol kuralı:** Aşağıda sözleşme (arayüz + model) her zaman `src/Core/<Alan>/`, Windows uygulaması `src/Platform/<Alan>/`, görsel + ViewModel `src/App/Features/<Özellik>/` içindedir.

---

## M1 — Notch kabuğu + Müzik paneli ✅ (2026-11-04)
**Hedef:** Spotify / tarayıcıda çalan müzik notch'ta görünür ve kontrol edilir. < 60 MB.

> **M1 Tasarım Mutabakatı (2026-11-04):**
> - **Geometri:** Ekrana sıfır yapışık (`Top = 0`), üst sağ/sol köşeleri içbükey (*concave*) kulaklı özel `NotchShape` (`Path`).
> - **Durum Geçişleri:** Hover → `Peek`, Tık → `Expanded`. Şarkı değişince 3 sn otomatik `Peek` → `Collapsed`. `WS_EX_NOACTIVATE` yüzünden `Deactivated` olayı çalışmayacağından; `MouseLeave` anında `Peek` derhal, `Expanded` ise ~400 ms tolerans gecikmesiyle `Collapsed`'a döner (veya üst şeride tekrar tıklanınca kapanır).
> - **Boşta %0 CPU Kuralı:** `Collapsed` durumda müzik çalarken sol yanda küçük kapak, sağ yanda **hareketsiz (statik)** ses dalgası vektör ikonu durur (sürekli animasyon çizilmez). Hareketli çubuklar yalnız `Peek` ve `Expanded` durumlarında çalışır.
> - **Sözleşme (`IMediaService`):** Çoklu oturum (`Sessions`, `SelectSessionAsync`) ve interaktif seek (`MediaTimeline`, `SeekAsync`) ile genişletilir. İlerleme çubuğu zamanlayıcısı (1 sn) yalnız `Expanded` + `Music` açıkken çalışır.

### 1.1 Pencereyi sağlamlaştır
- [x] `Platform/Shell/WindowStyles.cs`: `WS_EX_TOOLWINDOW` (Alt+Tab'da görünmesin) ve `WS_EX_NOACTIVATE` (tıklayınca odak çalmasın). `NotchWindow.OnSourceInitialized` içinde uygula. NativeMethods.txt: `GetWindowLong`, `SetWindowLong`, `WINDOW_EX_STYLE`.
- [x] Topmost kaybolmasın: tam ekran olmayan durumda periyodik değil, `WindowTracker` olayıyla (1.4) yeniden `Topmost` ver.
- [x] Çentik şekli: `Border` yerine alt köşeleri yuvarlak, üst kenarları içe kıvrık bir `Path` (`App/Features/Notch/NotchShape.cs`, `Width`/`Height`/`CornerRadius` bağımlılık özellikleri). Animasyon bu kontrolün boyutunu değiştirir; pencere sabit kalır (AGENTS.md #13).
- [x] Panel açılışında fps ve CPU ölç (PerfView ya da Görev Yöneticisi), sonucu `docs/MEMORY.md`'ye not et.

### 1.2 Panel sistemi
- [x] `Core/Notch/NotchPanel.cs`: `enum NotchPanel { Music, ControlCenter, Status, Calendar, Shelf }`
- [x] `Core/Notch/NotchState.cs`: `Collapsed`, `Peek` (küçük bilgi, ör. şarkı adı), `Expanded`. Geçişler saf fonksiyon; testleri `tests/Core.Tests/NotchStateTests.cs`.
- [x] `Core/Notch/NotchController.cs`: panel sırası, durum geçişleri, medya olayında Peek'e geçme kararı (saf mantık, testli).
- [x] `App/Features/Notch/NotchViewModel.cs`: `NotchController`'ı bağlar; `CurrentPanel`, `State`, `NextPanelCommand`, `PreviousPanelCommand`.
- [x] `NotchWindow.xaml`: içerik `ContentControl` + `DataTemplate` ile panel ViewModel'ine göre seçilir. Fare tekerleği / dokunmatik kaydırma → panel değişir.
- [x] Panel geçişi: `Spring` ile yatay kayma + opaklık.

### 1.3 Müzik
- [x] `Platform/Media/MediaService.cs : Core.Media.IMediaService` — `GlobalSystemMediaTransportControlsSessionManager.RequestAsync()`; `CurrentSessionChanged`, `SessionsChanged`, oturum başına `MediaPropertiesChanged`, `PlaybackInfoChanged`. Olaylar arka plan iş parçacığından gelir; ViewModel `Dispatcher` ile UI'a geçirir.
- [x] Kapak: `MediaProperties.Thumbnail.OpenReadAsync()` → byte[]; App tarafında `BitmapImage { DecodePixelWidth = 96 }` + `Freeze()`.
- [x] Birden fazla oturum: `GetSessions()` listesi, panelde küçük uygulama simgeleri arasında geçiş.
- [x] `App/Features/Music/MusicPanel.xaml` + `MusicViewModel.cs`: kapak, başlık, sanatçı, önceki / oynat-duraklat / sonraki, ilerleme çubuğu (`TimelineProperties`; saniyede bir güncelleme yalnız panel açıkken).
- [x] Collapsed durumda müzik çalıyorsa hapın iki yanında küçük kapak + basit dalga simgesi (gerçek ses analizi değil; M1'de animasyonlu çubuk yeter).
- [x] Ses görselleştirici: Ana müzik kartında 24 çubuk (tasarım H). WASAPI loopback (`SpectrumCaptureService`, ayrı iş parçacığı, ~30 kare/sn) + `SpectrumAnalyzer` (1024 FFT, log bantlar, testli). Yalnız çentik açık, Ana görünür ve müzik çalarken; "kapak ve dalga" ayarına bağlı, pil tasarrufunda kapalı. Ses inince olay kesilir (çizim yok). Deneme programında ölçüm: +1 MB özel bellek, çalarken 3 sn'de 16 ms CPU. Gerçek müzikle Koy içinde elle sınanmadı; varsayılan çıkış aygıtı değişince yakalama eski aygıtta kalır (kart yeniden açılınca düzelir).

### 1.4 Görünürlük modları
- [x] `Core/Shell/IWindowTracker.cs` + `IFullscreenDetector.cs` (sözleşmeler, pencere kimliği `nint`).
- [x] `Platform/Shell/WindowTracker.cs`: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_LOCATIONCHANGE)`; ön plandaki pencerenin dikdörtgeni değişince olay. Hook delegesini alan olarak tut (GC toplamasın).
- [x] `Platform/Shell/FullscreenDetector.cs`: `SHQueryUserNotificationState` (`QUNS_BUSY`, `QUNS_RUNNING_D3D_FULL_SCREEN`, `QUNS_PRESENTATION_MODE`) + ön plan penceresi monitörü tamamen kaplıyor mu.
- [x] `Core/Visibility/VisibilityController.cs`: girdi (mod, pencere üst üste mi, fare kenarda mı, tam ekran mı, medya olayı) → çıktı (görünür / gizli / tıklama geçirgen). Saf mantık, testli.
- [x] Gizlenme: yukarı kayarak (`Spring`), gizliyken `WS_EX_TRANSPARENT` ile tıklama geçirgen.
- [x] Peek: fare ekranın üst kenarına 2 px yaklaşınca çık. Fare konumu için `GetCursorPos` yalnız gizliyken ve 100 ms aralıkla; görünürken `MouseLeave` kullan.

### 1.5 Kabul
- [x] Spotify, YouTube (Edge/Chrome) ve Windows Media Player'da çalışıyor.
- [x] Tam ekran oyun / video sırasında notch görünmüyor.
- [x] Alt+Tab'da görünmüyor, tıklayınca odak çalmıyor.
- [x] Bellek ölçüldü, < 60 MB. `docs/MEMORY.md` güncellendi.

---

## M2 — Kontrol Merkezi + Durum (2026-11-04, kısmen tamamlandı)
- [x] Sözleşmeler: `Core/Audio/IAudioService.cs`, `Core/Radio/IRadioService.cs`, `Core/Display/IBrightnessService.cs`, `Core/Power/IPowerService.cs`, `Core/Weather/IWeatherService.cs` (+ modeller).
- [x] `Platform/Audio/AudioService.cs`: CoreAudio `IMMDeviceEnumerator` → `IAudioEndpointVolume` (seviye, sessiz, değişim bildirimi `IAudioEndpointVolumeCallback`). CsWin32 ile COM.
- [x] `Platform/Radio/RadioService.cs`: `Windows.Devices.Radios.Radio.GetRadiosAsync()` — Wi-Fi ve Bluetooth aç/kapa. İlk çağrıda `RequestAccessAsync()`.
- [x] `Platform/Display/BrightnessService.cs`: dahili ekran WMI `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods`; harici ekran DDC/CI (`GetPhysicalMonitorsFromHMONITOR`, `SetVCPFeature` kod 0x10). Desteklenmiyorsa kaydırıcı gizlenir.
- [x] `Platform/Shell/FocusAssistService.cs`: Rahatsız Etme durumu (okuma); açma/kapama için resmi API yoksa yalnız Windows ayarını açan kısayol.
- [x] `Platform/Power/PowerService.cs`: `Windows.System.Power.PowerManager` (pil yüzdesi, şarj); pil yoksa panelde gizle.
- [x] `Platform/Weather/WeatherService.cs`: Open-Meteo (`https://api.open-meteo.com/v1/forecast?latitude=..&longitude=..&current=temperature_2m,weather_code`). Konum: ayarlardan şehir (Open-Meteo geocoding API) — konum izni isteme. 30 dk önbellek, `HttpClient` tek örnek. Konum kurulum sihirbazında sorulur (cihaz konumu, açılışta günde en fazla bir kez) ya da Ayarlar › Çentik'ten şehir aranır (`Core/Weather/WeatherLocationPolicy`, `OpenMeteoParser`, `Platform/Weather/LocationService`, `GeocodingService`). Konum yoksa hava durumu gösterilmez; Hakkında'da Open-Meteo atfı.
- [x] `App/Features/ControlCenter/ControlCenterPanel.xaml` + ViewModel: döşemeler (Wi-Fi, BT, Rahatsız Etme), kaydırıcılar (ses, parlaklık).
- [x] Döşeme düzenini sürükleyerek değiştirme: düzen `AppSettings.ControlCenterLayout` (liste) olarak saklanır; sıralama mantığı `Core/Notch/ControlCenterTiles`, sürükleme `ControlCenterPanel` (yay animasyonlu).
- [x] `App/Features/Status/StatusPanel.xaml` + ViewModel: pil, hava durumu, saat.
- [x] Kabul: her kontrol gerçek donanımda denendi; bellek ölçüldü (40.9 MB < 60 MB bütçe).

---

## M3 — Takvim, Pomodoro, Raf, Ayarlar, Dil (2026-11-04, kısmen tamamlandı)
- [x] `Core/Calendar/MonthGrid.cs`: ay görünümü için gün ızgarası (Pazartesi başlangıçlı, kültüre göre). Testli.
- [x] `Core/Timers/Pomodoro.cs`: durum makinesi (çalışma / kısa mola / uzun mola), süreler ayardan (Paket C'de bağlandı). Testli; zaman `TimeProvider` ile verilir ki test edilebilsin.
- [x] `App/Features/Calendar/CalendarPanel.xaml`: gün / hafta / ay, zamanlayıcı, kronometre, Pomodoro. Yalnız yerel (`%AppData%\Koy\events.json`); Google/Outlook sonraki sürüm. Durum makineleri `Core/Timers/CountdownTimer`, `LapStopwatch` (TimeProvider, testli); etkinlik mantığı `Core/Calendar/EventAgenda`, `EventBook`.
- [x] Bildirim: zamanlayıcı bitince notch kısa süre Peek durumuna geçer ve titrer (yay animasyonu).
- [x] `App/Features/Shelf/ShelfPanel.xaml`: notch'a dosya / metin sürükle-bırak (`AllowDrop`), listeden dışarı sürükleme (`DragDrop.DoDragDrop`). Dosyalar kopyalanmaz, yol tutulur; liste `%AppData%\Koy\shelf.json`.
- [x] Ayarlar penceresi: M6.5'te `KoyWindow` oldu, eski `SettingsWindow` silindi. Paket C ve D1 sonrası vurgu rengi, tema ve katı/yarı saydam malzeme görünüme etki ediyor.
- [x] Başlangıçta çalış: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- [x] Cam görünüm: `BackdropService` artık `KoyWindow`'da `Material` ayarına göre kullanılıyor (D1). **Doğrulanmadı:** `AllowsTransparency` penceresinde DWM acrylic'in gerçek cihazda etkisi; etkisizse %88 opak yüzeylerle sınırlı kalır.
- [x] Dil: `Resources/Strings.resx` (en) + `Strings.tr.resx`; XAML'de `{x:Static res:Strings.Key}`. Bütün sabit Türkçe metinler buraya taşınır.
- [x] Tepsi simgesi: `TrayIconService` (CsWin32, WinForms yok); Çıkış yalnız Koy penceresi ve sağ alt menüde (notch sağ tık menüsünden kaldırıldı).

---

## M4 — Dock ✅ (2026-11-04)
**En riskli aşama. Görev çubuğu güvenliği kuralı (AGENTS.md #7) burada kritik.**
- [x] `App/Features/Dock/DockFeature.cs : IFeature`; `App.xaml.cs` yalnız `settings.DockEnabled` ise ekler. Kapalıyken hiçbir dock servisi ya da hook'u kurulmaz (AGENTS.md #12).
- [x] Sözleşmeler: `Core/Dock/IAppBarService.cs`, `IPinnedAppsService.cs`, dock öğe modeli; sıralama ve gruplama mantığı `Core/Dock/` içinde testli.
- [x] `Platform/Shell/AppBarService.cs`: `SHAppBarMessage(ABM_NEW / ABM_QUERYPOS / ABM_SETPOS / ABM_REMOVE)` ile ekranın altında yer ayırma (yalnız Fixed modda). Kapanışta mutlaka `ABM_REMOVE`.
- [x] Görev çubuğunu gizleme: `TaskbarService.Hide()` + `ABM_SETSTATE(ABS_AUTOHIDE)` ile çalışma alanını geri alma; kapanışta eski durumu geri yükle. Eski durumu `%AppData%\Koy\taskbar-state.json` dosyasına yaz ki çökme sonrası açılışta geri yüklenebilsin.
- [x] `Platform/Shell/PinnedAppsService.cs`: `%AppData%\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\*.lnk`; simge `SHGetFileInfo` / `IShellItemImageFactory` (32 px, önbellekli). Sıra v1'de klasör sırası.
- [x] `Platform/Shell/WindowTracker.cs` genişlet: açık üst düzey pencereler (`EnumWindows` + görünür, sahipsiz, `WS_EX_TOOLWINDOW` değil), oluşma / kapanma olayları (`EVENT_OBJECT_CREATE / DESTROY / SHOW / HIDE`), yanıp sönme (`HSHELL_FLASH` için `RegisterShellHookWindow`).
- [x] `App/Features/Dock/DockWindow.xaml` + `DockViewModel.cs`: sabitli + açık uygulamalar, çalışan göstergesi, tıklayınca öne getir / küçült (`SetForegroundWindow`, `ShowWindow`), orta tık yeni pencere, sağ tık menüsü (Kapat, Görev Yöneticisi'nde sonlandır = `TerminateProcess` onaylı).
- [x] Fareyle büyüme (macOS tarzı) `Spring` ile; isteğe bağlı, ayardan kapatılabilir.
- [x] Başlat düğmesi: `Win` tuşu gönder (`SendInput`), sistem tepsisi düğmesi: `Win+B` ya da `Shell_TrayWnd` taşma penceresi. Tepsi simgelerini taşımak v1 dışındaydı; PR #11 ile geldi (aşağıda "Dock sistem tepsisi").
- [x] Kabul: süreci öldür → yeniden aç → görev çubuğu geri. Kaldır → görev çubuğu geri. Tam ekranda dock gizli. Bellek ölçüldü.

---

## M5 — Çoklu monitör, kurulum, ilk sürüm (2026-11-04, kısmen tamamlandı)
- [x] `Platform/Display/MonitorService.cs`: `EnumDisplayMonitors`, `GetMonitorInfo`, `GetDpiForMonitor`; ekran değişince (`WM_DISPLAYCHANGE`) pencereleri yeniden yerleştir. Pencere konumları fiziksel pikselden DIP'e çevrilir.
- [x] Ayar: notch / dock ekran başına `MonitorSettings` ile (kararlı monitör kimliği), Koy penceresindeki Ekranlar sayfasından seçilir (D2). Eski `MonitorTarget` göçü testli.
- [x] Velopack: `vpk pack` ile kurulum + `UpdateManager` ile GitHub Releases'tan güncelleme. Kaldırma kancasında `TaskbarService.Restore()`.
- [x] `.github/workflows/release.yml`: etiket (`v*`) itilince derle, `vpk pack`, `SHA256SUMS.txt`, GitHub Release'e yükle.
- [x] README'ye ekran görüntüleri, kurulum, SHA256 doğrulama.
- [x] v0.1.0.

---

## M6 — Koy'u Windows'tan ayır: dock yeniden, çoklu ekran, kendi arayüzü (2026-11-04, açık)
**Neden:** Windows görev çubuğunun aynısı olan tam genişlik çubuk, Koy'u ayırt edici kılmıyor. Çoklu ekranda yalnız birincil ekranda çalışıyor. M5'teki `All` monitör ayarı kodda yok: `MonitorTarget` yalnız tek bir ekran seçiyor. 

### 6.1 Arayüz tasarımı (önce bu, proje sahibi seçer)
- [x] Dock, çentik HUD'u ve Koy penceresi için tasarım taslakları: proje sahibi onayladı (2026-11-04). Özet ve ölçüler `docs/DESIGN.md`'de, görseller `docs/design/index.html`'de.

### 6.2 Çoklu ekran
- [x] `Core`: monitör başına durum modeli. Hangi ekranda ne gösterileceği (`Primary` / `All` / `Specific`) saf mantıkla belirlenir ve test edilir.
- [x] Her monitörde ayrı `NotchWindow` ve `DockWindow`, her biri kendi DPI'ında. Ekran takılıp çıkarılınca (`WM_DISPLAYCHANGE`) pencereler eklenir, kaldırılır ve yeniden yerleşir; kullanıcı Ekranlar sayfasından açar. **Doğrulanmadı:** gerçek çok monitörlü / karışık DPI donanım, ikincil AppBar.
- [x] Tam ekran algılama monitör başına: yan ekrandaki tam ekran, ana ekrandaki Koy'u gizlemez. `SHQueryUserNotificationState` tüm sistemi kapsıyor, bu yüzden tek başına karar vermemeli. Testli.
- [x] Görev çubuğu gizleme bütün ekranlarda geçerli olmalı (`Shell_SecondaryTrayWnd` da). Geri yükleme de hepsinde çalışmalı.

### 6.3 Dock yeniden (Seelen modeli)
- [x] Dock düzeni ayardan seçilir: yüzen kapsül ya da tam genişlik.
- [x] "Pencere yaklaşınca" davranışı ayardan seçilir: **Yer ayır** (dock alt kenara oturur, çalışma alanı ayrılır), **Aşağı yapış** (normal boy pencere yaklaşınca dock alt kenara iner, incelir, pencereyi örtmez; varsayılan), **Çekil** (büyütülmüş pencerede gizlenir, fare alt kenara inince gelir), **Hep gizli**. Tam ekranda her modda gizli. Büyütülmüş pencere ile dock arasında boşluk kalmamalı. Paket C'de varsayılan `DockDown` oldu ve her yüklemede `Dodge`'a çeviren göç kaldırıldı. Not: eski bir kurulumda `settings.json`'da `Dodge` yazılı kalmış olabilir; Dock sayfasından Aşağı yapış seçilir.
- [x] Simgenin üzerine gelince pencere önizlemesi (DWM thumbnail, `DwmRegisterThumbnail`). Yalnız üzerine gelindiğinde kurulur, bellek ölçülür.
- [x] İsteğe bağlı ayırıcı: solda uygulamalar, sağda klasörler, son kullanılanlar ve küçültülmüş pencereler. Ayırıcı, İndirilenler klasörü, küçültülmüş pencereler ve son kullanılanlar yığını var (Windows Son Kullanılanlar klasörü, en çok 10 öğe, yalnız tıklanınca okunur); hepsi Dock sayfasından kapatılır.
- [x] Saat, pil, ses ve ağ dock'tan çentiğe taşınır (tasarıma göre). Dock'ta durum alanı yok; saat, pil ve ağ durumu çentik H arayüzünde (üst durum satırı + Ana sağ sütunu), ses kontrol merkezinde.

### 6.3b Dock: "Hep havada" yakınlık modu (issue #25)
- [x] `DockProximityMode.AlwaysFloating`, enum'un sonuna eklendi. Ayar dosyasında adıyla yazılır (`UseStringEnumConverter`), eski dosyalar göçsüz okunur; testli.
- [x] `DockPositionResolver`: pencere yakınlığından ve büyütülmüşlükten etkilenmeden hep yüzen kapsül geometrisi (60 px, alttan 10 px, AppBar payı yok). Tam ekranda gizlenme kuralları aynı. Testli.
- [x] `DockProximityMapping`: tam genişlikte seçilemez, etkin karşılığı Yer ayır. `DockSettingsDiff` AppBar'ı yalnız Yer ayır'a girip çıkışta yeniden kurar. Testli.
- [x] Ayarlar › Dock: yeni seçenek ve tek cümlelik açıklaması; Çekil "Pencereden çekil" olarak, Hep gizli açıklamasıyla farkları net olacak şekilde yeniden adlandırıldı/anlatıldı (davranışları değişmedi, enum değerleri korundu).
- [ ] Elle sınama: Hep havada seçiliyken büyütülmüş pencere, pencereyi dock'a sürükleme, Alt+Tab, tam ekran (oyun/F11), "Tam ekranda gizle" kapalıyken, akıllı görünürlük açıkken, çok monitör; Yer ayır ↔ Hep havada geçişinde çalışma alanının yerinde kalması.
- [ ] Bellek ölçümü (`scripts/measure-memory.ps1`, AGENTS #1) ve `docs/MEMORY.md` güncellemesi.

### 6.4 Çentik genişler
- [x] Ses ve parlaklık değişince çentikte gösterge: Paket C'de `AudioService` ve `BrightnessService` olaylarına bağlandı, ayrı ayrı kapatılır. Windows'un kendi göstergesini gizleme hâlâ deneysel.
- [x] Dosya rafı ve takvim çentikte zaten var (M3). Kapalı çentikten erişimi gözden geçirilir.

### 6.5 Koy'un kendi arayüzü
- [x] Sağ altta Koy simgesi: dock açıkken dock'un sağ ucunda, dock kapalıyken bildirim alanında (`Shell_NotifyIcon`, CsWin32 ile, WinForms yok). Tıklayınca Koy penceresi açılır. Dock penceresi varken bildirim alanı simgesi kaldırılır (tek simge), dock kapanınca ya da duraklayınca geri gelir; "1 saat duraklat" gerçekten çentik ve dock'u durdurur; Explorer yeniden başlayınca simge (gizli değilse) yeniden eklenir. **Elle denenmedi** (dock açma/kapama/duraklatma, Explorer yeniden başlatma).
- [x] Çıkış yalnız sağ alt menüden ve Koy penceresindeki "Koy'dan çık" düğmesinden yapılır; çentik ve dock sağ tık menülerindeki "Çıkış" kaldırıldı (Paket C).
- [x] Ayarlar penceresi yeni tasarıma göre "Koy penceresi" olur: orta boy (880 px), sekiz sayfa (Genel, Çentik, Dock, Ekranlar, Görünüm, Kısayollar, Performans, Hakkında). Sayfaların ayarları tasarım galerisinde. "Hakkında"da "Windows görev çubuğunu geri getir" acil durum düğmesi bulunur. Paket C ve D ile ayarlar gerçekten bağlandı; Hakkında sayfasına ayrıca "Koy'u kaldır" eklendi.
- [x] İlk açılış: üç adımlık kurulum (ne gösterilsin: çentik ve dock / yalnız çentik / yalnız dock).

### 6.6 Kabul
- [x] İki ekranla dene: yan ekranda tam ekran video oynarken ana ekranda çentik ve dock duruyor. **Kısmi:** mantık monitör başına ve testli, kullanıcı Ekranlar sayfasından açabiliyor; gerçek iki ekranla elle kabul testi yapılmadı. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Ekranı çıkar ve tak: pencereler doğru yere geliyor, görev çubuğu bütün ekranlarda gizli. **Kısmi:** kod var, gerçek donanımda denenmeli. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Bellek ölçüldü: 82.7 MB private. Bütçe kararı 2026-11-06'da verildi: boşta 100–150 MB hedef, 200 MB'a kadar kabul (bkz. `docs/MEMORY.md`).

---

## Revizyon paketleri (2026-11-05)
Kod incelemesinden çıkan işler paketlere bölündü.
- [x] **A, stabilite:** SagAltMenu `Measure` çökmesi, kenar zamanlayıcısı thread erişimi, global hata yakalama ve `crash.log`, görev çubuğu çift gizleme ve Explorer yeniden başlama, tek `WindowTracker` ve `MediaService` paylaşımı.
- [x] **B, temizlik:** ölü kod, PDB yol gizleme, belge hijyeni.
- [x] **C, ayarları bağlama:** etkisiz ayarlar bağlandı ya da kaldırıldı; `DockDown` varsayılan; kısayollar (`RegisterHotKey`, özelleştirilebilir, çakışma bildirimi); OSD HUD bağlandı; gerçek "1 saat duraklat"; Çıkış yalnız Koy penceresi ve sağ alt menüde.
- [x] **D1, görünüm:** Koyu/Açık tema ve token sistemi, Lucide tabanlı ikonlar, hareket (`Motion`), malzeme ayarı. Arayüzü PNG'ye çizen `tools/Koy.UiSnapshots`.
- [x] **D2, Ekranlar:** monitör haritası, ekran başına çentik ve dock, kararlı monitör kimliği, gerçek çoklu ekran penceresi yönetimi.
- [x] **F, sürüm ve CI:** release-please, SHA'ya sabitlenmiş action'lar, pinli `vpk`.
- [x] **Kaldırma deneyimi:** Hakkında'da "Koy'u kaldır" + onay görünümü; Windows'tan kaldırmada kanca (görev çubuğu, başlatma kaydı, durum dosyaları; ayarlar korunur).
- [x] **Belge son geçişi:** AGENTS, README, DESIGN, CHANGELOG gerçek duruma getirildi.

## Sıradaki
**E paketi adayları** (bütçe kararı verildi: 100–150 MB hedef, 200 MB'a kadar kabul; her biri yeni servis demek, önce ölç):
- [x] Pano geçmişi (yalnız metin, sınırlı ve silinebilir) — H servisleri ve Çentik H arayüzü altında tamamlandı.
- [x] Mikrofon / kamera kullanım göstergesi (çentikte nokta, #29).
- [x] Bluetooth cihaz HUD'u (kulaklık bağlandı / pil seviyesi, `BluetoothHudPolicy` + `OsdIndicatorViewModel.ShowBluetoothDevice`).
- [x] Pil HUD'u tamamlama (şarja takıldı/çıkarıldı, %20/%10 düşük pil, %100 tam şarj uyarıları; `BatteryNotificationPolicy` + `OsdIndicatorViewModel.ShowBattery`).
- [x] **RAM optimizasyonu:** #14 radyo servisini açılışta kuruyor; yalnız boşta bağlantı simgesi (`IdleDisplay.ShowRadio`) açıkken açılışta kurulsun, aksi halde Kontrol Merkezi ilk açılana kadar ertelensin (`NotchViewModel`).
- [x] **Bellek bütçesi kararı (2026-11-06):** boşta 100–150 MB private hedef, çok ekran / açık panellerle 200 MB'a kadar kabul. Kurulu v0.1.1 ölçümü: 99–100 MB private, Görev Yöneticisi "Bellek" 73,6 MB (`docs/MEMORY.md`).

**Katkıcı PR'ları (xdaxer):**
- [x] #8 pilsiz makinede pil gizleme, #10 dock pencere önizlemesi, #12 Kontrol Merkezi Wi-Fi/Bluetooth yönetimi, #13 açılış zemini: `main`'e alındı (bizim düzeltmelerimizle).
- [x] #14 çentik sekmeleri ve Wi-Fi/Bluetooth durum simgeleri: `main`'e alındı (bizim düzeltmelerimizle: "Hiçbiri" modu, token/ikonlar, hareket politikası, paylaşımlı radyo servisi). Bellek ölçümü yapılmadı, aşağıdaki RAM optimizasyonu işine bırakıldı.
- [x] #11: `main`'e entegre edildi ve PR birleştirildi (aşağıda "Dock sistem tepsisi"); kalan kapsam (taşma simgeleri, balon bildirimleri) "Sonraya kalanlar"da.
- [x] #9: `main`'de farklı bir yapıyla (Ekranlar sayfası, D2) çözüldü ve kapatıldı.

**Hâlâ açık olan eksikler:** iki sütunlu ana görünüm ve kapalı/Peek çentikte tarih ve ağ simgesi (tasarım bekliyor; veri tarafı hazır: `HomeViewModel`, `NotchLayout.ShowPeekDetails`); UWP uygulama sabitlemeleri (AUMID); ses görselleştirici; gerçek çoklu monitör ve karışık DPI doğrulaması; bellek ölçümü.

---

## Dock sistem tepsisi (PR #11, xdaxer; entegre edildi, elle denenmedi)
- [x] `Core/Tray/`: SHELLTRAYDATA çözümleyici (boyut sınırlı), simge deposu (üst sınır 256), tıklama geri çağrısı, `TrayHostPolicy` (ne zaman barındırılır, Koy-süreç filtresi). Testli.
- [x] `Platform/Shell/TrayHostService.cs`: kendi iş parçacığında gizli `Shell_TrayWnd` penceresi, her kaydı Windows'a da iletir. `ShellTrayLocator.FindExplorerTray` görev çubuğu gizleme/geri getirme kodunda Koy'un kendi penceresini atlar.
- [x] `SharedServices.AcquireTrayHost`: bütün monitörlerdeki dock'lar aynı örneği paylaşır; yalnız Windows görev çubuğu gizliyken (ayar açık ve en az bir dock penceresi var) kurulur. Ayar: Dock sayfası "Sistem tepsisini dock'ta göster" (`DockShowTray`, varsayılan açık).
- [x] Koy kapanır / duraklatılır / dock kapanır / ayar kapanır → pencere kapanır ve Windows tepsisi geri gelir; çökme işleyicileri görev çubuğunu ve tepsi penceresini birlikte kapatır.
- [x] Sağ tıkta açılır pencere kapanmaz: uygulamanın bağlam menüsü açıkken açık kalır; menüden seçim yapılınca ya da pencere ve menünün dışına tıklanınca yay animasyonuyla kapanır (`TrayPopupDismissPolicy`, odak kaybı kapatma sebebi değil).
- [x] **Elle sınama** (proje sahibi): Discord/Steam/Tailscale tıklama ve menü; Görev Yöneticisi'nden süreci öldür → Windows tepsisi ve görev çubuğu geri; Explorer'ı yeniden başlat → simgeler geri; çok monitör. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [ ] Bellek ölçümü (AGENTS #1): main ile main + tepsi karşılaştırması, `docs/MEMORY.md`.

## Dock ve tepsi paketi (2026-11-06)
- [x] **Gözcü süreç:** `src/Watchdog/` (`Koy.Watchdog.exe`, Native AOT, pencere yok). Dock görev çubuğunu gizlediği sürece tek gözcü çalışır, Koy'un PID'ini bekler; Koy öldürülürse görev çubuğunu geri getirir (`taskbar-state.json`), temiz çıkışta dokunmaz. Görev çubuğu mantığı `TaskbarService.cs` / `ShellTrayLocator.cs` dosyalarını bağlayarak paylaşılır; karar mantığı `Core/Shell/WatchdogPolicy.cs`. `scripts/pack.ps1` ve `release.yml` exe'yi Koy'un klasörüne koyar. **AOT yayın bu makinede (C++ araçları yok) üretilemedi; elle sınama bekliyor.**
- [x] **Sabitli uygulama sırası:** sıra `AppSettings.DockPinnedOrder`'da; ilk açılışta Taskband `Favorites` tohum (okunamazsa klasör sırası); dock'ta sürükleyerek sıralama (yay ile kayma). Mantık `Core/Dock/PinnedOrder.cs`, testli.
- [x] **Alt kenardan açılma ve tam ekran:** büyütülmüş pencerede dock gizliyse fare alt kenarda ~300 ms durunca (tuş basılı değilken) çıkar, "Alt kenardan aç" ayarıyla kapatılır; gerçek tam ekranda asla çıkmaz. Tam ekran üç işaretin birleşimi, monitör başına (`FullscreenRule`, `EdgeRevealTracker`, `DockPositionResolver`), testli.
- [x] **Tepsi açılır penceresi** sağ tıkta kapanmıyor (yukarıda).
- [x] **Tek tepsi simgesi** (6.5).
- [x] **Dock'ta son kullanılanlar** (6.3).
- [x] **Çoklu ekran varsayılanı:** yeni ekran varsayılanı çentik ve dock açık (`MonitorPolicy.DefaultNewScreenMode`), testli.
- [x] **Belgeler:** bellek bütçesi kararı, ölçüm kaydı, CHANGELOG.
- [x] **Tepsi düğmesi kaybolması düzeltildi:** ilk kurulum hatası kalıcı bayrağa dönüşüyordu (`_trayAcquireFailed`) ve ölen tepsi penceresi yalnız bir sonraki pencere olayında fark ediliyordu. Karar `Core/Tray/TrayHostRecovery` (üstel beklemeli yeniden deneme), sayaç `TrayHostLease`; `TrayHostService.Exited` beklenmedik kapanışı bildirir ve `crash.log`'a yazar. Testli.
- [x] **Tepsi menüsü dock'un altında kalması düzeltildi:** `DockFeature` ön plan değişiminde dock'u `ReassertTopmost` ile menünün üstüne çıkarıyordu. Menü (`#32768`) varken dock öne alınmaz, `PopupMenuProbe` yoklaması (yalnız tepsi penceresi açıkken, 50 ms) menüleri odak çalmadan topmost'un önüne alır, menü çıpası `Core/Tray/TrayMenuAnchor` ile dock'un üst kenarının üstüne sıkıştırılır. Testli.
- [x] **Elle sınama** (proje sahibi): Görev Yöneticisi'nden Koy'u öldür → görev çubuğu birkaç sn içinde kendiliğinden gelmeli; Koy'dan çık / dock'u kapat → gözcü kalmamalı (Görev Yöneticisi'nde `Koy.Watchdog.exe`); simge sürükleyerek sırala, yeniden başlatınca sıra korunmalı; büyütülmüş pencerede alt kenara git (tuş basılı değilken ~300 ms sonra çıkar), oyun / F11 / tam ekran videoda çıkmasın; tepsi simgesine sağ tık → pencere açık kalsın, menüden seçince / dışarı tıklayınca kapansın; dock açıkken bildirim alanında Koy simgesi olmasın, dock kapanınca gelsin; son kullanılanlar yığını; ikinci ekran takınca çentik ve dock açılsın. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [ ] Gözcü AOT paketinin gerçek CI/`pack.ps1` çıktısından boyut ve bellek ölçümü; paket sonrası Koy belleğinin yeniden ölçümü (`docs/MEMORY.md`).

## Üçüncü test düzeltmeleri (2026-11-07)
- [x] **Küçültülmüş pencere düğmesi kaldırıldı:** komuta bağlı değildi; `DockShowMinimized` ayarı da silindi (eski ayar dosyasındaki alan yok sayılır).
- [x] **Tepsi simgeleri net:** Windows tepsisi gibi 16 DIP, kendi piksel boyutunda (önce 16 px simge 20 DIP'e büyütülüyordu); HICON verisi düz alfa sayılır (`IconHelper`).
- [x] **Başlat düğmesinde gerçek Windows 11 logosu** (`WindowsLogoImage`, Styles.xaml; kapsül ve tam genişlik aynı kaynak).
- [x] **Win tuşu / Başlat açıkken dock görünür:** kabuk açılır pencereleri (`Core/Shell/ShellFlyouts`: Başlat, Arama, bildirim merkezi) dock'u çektirmez, açıldığı ekranda dock'u gösterir. Testli.
- [x] **Koy düğmesi dock'u gizliyordu:** pencere gösterilince (`OnWindowsChanged`) Koy'un kendi menüsü dış pencere sayılıp açıldığı ekranın dock'unu çektiriyordu; Koy süreci denetimi `RecheckForegroundState` içine taşındı.
- [x] **Koy penceresi sayfa geçişi:** satır satır gecikmeli giriş ve kayma kaldırıldı (gecikmede satırlar önce görünüp sonra sönüyordu); tek 200 ms solma %50 → %100 (Corvus gibi), hemen başlar.
- [x] **Bellek göstergesi Görev Yöneticisi'yle aynı:** özel çalışma kümesi (`Platform/Diagnostics/ProcessMemory`); ayrılmış özel bellek (private bytes) yalnız okunamazsa.
- [x] **Yan ekrandaki büyütülmüş pencere öbür dock'u yapışık moda geçiriyordu:** ön plan sınırları bütün dock'lara veriliyordu; büyütülmüş pencerenin 8 px kenar taşması yan ekranın alt şeridine değiyordu. Pencere artık yalnız kendi ekranının dock'unu etkiler (`Core/Dock/DockForegroundFilter`, testli).
- [x] **Tek örnek:** ikinci Koy açılışı ilk sürecin Koy penceresini açar ve çıkar (`App/SingleInstance.cs`, oturum başına adlı mutex). 2026-11-07 çökmesi: Başlat menüsündeki kısayol kurulu eski v0.1.0'ı (`%LOCALAPPDATA%\Koy`) açtı, geliştirme derlemesiyle birlikte çalıştı; v0.1.0'daki kenar zamanlayıcısı iş parçacığı havuzundan dock'a dokunup çöktü (yeni kodda `DispatcherTimer`), gözcü olmadığı için görev çubuğu gelmedi.
- [x] **Elle sınama** (proje sahibi): tepsi simgeleri; Win tuşu tek ve iki ekranda (büyütülmüş pencere varken de); Koy düğmesi iki ekranda; sayfa geçişi; Koy'daki bellek sayısı Görev Yöneticisi'yle aynı mı. **Proje sahibi sınadı (2026-11-07), sorun yok.**
## Dördüncü test düzeltmeleri (2026-11-07)
- [x] **Dock varsayılanı Yer ayır:** yeni kurulumlarda dock Windows görev çubuğu gibi hep görünür, büyütülmüş pencereler onun üstünde biter (`AppSettings.DockProximity = ReserveSpace`). Ayar dosyasında kayıtlı değer olduğu gibi korunur, göç yok; dosyada alan yoksa yeni varsayılan gelir (testli). `DockProximityMode` yorumları ve `docs/DESIGN.md` güncellendi.
- [x] **"Pencere dock'a yaklaşınca" anlaşılır:** başlık netleşti; seçicinin altında seçili seçeneğin ne yaptığını anlatan tek cümlelik açıklama (seçim değişince değişir; TR ve EN).
- [x] **Parlaklık, kök neden:** `BrightnessService` açılışta bir kez kurulup kaynağı (WMI ya da DDC/CI) o anda kalıcı seçiyordu ve DDC için yalnız BİRİNCİL ekrana bakıyordu. İkinci ekran kapanınca (ya da dahili panel o sırada WMI'de etkin değilken kurulunca) servis eski kaynakta kalıyor, kaydırıcı etkin görünüp hiçbir şeyi değiştirmiyordu. Üstelik `SetBrightness` her kaydırıcı adımında UI iş parçacığında, eşzamanlı DDC/CI çağrısı yapıyordu (yüzlerce ms; ölü monitörde daha uzun): çentik donuyordu.
- [x] **Parlaklık, çözüm:** kaynak her açılışta ve ekran düzeni değişince (ekran eklendi/çıkarıldı) arka planda yeniden belirlenir; karar çentiğin bulunduğu ekrana göre (`Core/Display/BrightnessSourcePicker`: dahili panel WMI, harici DDC/CI, harici ekranda WMI kullanılmaz; DisplayConfig çıkış teknolojisiyle dahili/harici ayrımı). Monitör tanıtıcısı tutulmaz, her çağrıda güncel ekrandan çözülür. Ayar çağrıları UI'yi bloklamaz: `BrightnessWriteQueue` art arda değerleri birleştirir (yalnız son değer yazılır, sürerken yenisi birikmez, tek işçi). Yazım başarısız olursa kaynak yeniden belirlenir. DDC aralığı (min..max) yüzdeye ölçeklenir. Hiçbir yol yoksa kaydırıcı soluk ve devre dışı, altında "Bu ekranın parlaklığı ayarlanamıyor." yazar. Testli (`BrightnessTests`).
- [x] **Sistem sayfası "–" göstermiyor:** servis yalnız sayfa görünürken tutulduğundan her dönüşte sıfırdan kuruluyor ve ilk örneğe kadar boştu. Şimdi servis başlarken anlık değerler (RAM, Koy belleği, ağ adı) hemen okunur (`SystemSnapshot.RatesReady`), CPU / en çok süreç / ağ hızı ilk yeni örneğe (400 ms) kadar son bilinen değerle gösterilir (`Core/SystemStats/SystemSnapshotMerge`, testli; Ana ve Sistem aynı modeli kullanır). "–" yalnız hiç değer gelmediyse (ilk açılış) görünür. Servis hâlâ yalnız görünürken alınır, boşta %0 CPU.
- [x] **Hızlı başlat "+" çentiği kapatıyordu:** dosya seçici sahipsiz açılıyordu (Koy ön planda değilken görünmez kalabiliyor, görev çubuğu gizli) ve açıkken çentiğe tıklamak çentiği kapatıp seçiciyi yetim bırakıyordu; fare çıkınca kapanmayı yalnız iğne engelliyordu. Şimdi çentik denetleyicisinde sayaçlı "tutma" var (`NotchController.HoldOpen/ReleaseHold`: fare ayrılsa da tıklansa da kapanmaz; iğneden ayrı, iğne simgesi yanmaz); seçici kapanınca normal davranış döner (fare dışarıdaysa 400 ms sonra kapanır). Seçicinin sahibi çentik penceresi: çentik seçici için geçici etkinleştirilir, diyalog onun üstünde açılır. Ekleme yolu (`Add` kaydeder ve `Changed` yükseltir) testli.
- [x] **Klasör ekleme:** hızlı başlata klasör seçici (`OpenFolderDialog`) ve bağlam menüsü eklendi (bkz. İş sırası #7).
- [x] **Elle sınama (proje sahibi):** laptop + ikinci ekranla parlaklık (ikinci ekran açıkken ve kapatınca; dahili panel kaydırıcısı değişiyor mu, çentik donmuyor mu; harici monitörde DDC/CI; ekran çıkarılıp takılınca kaynağın yenilenmesi; hiç yol yokken kaydırıcı devre dışı mı); Sistem ve Ana sekmesinden çıkıp dönünce değerlerin hemen görünmesi; Hızlı başlat "+": diyalog çentiğin üstünde açılıyor mu, açıkken çentik kapanmıyor mu, seçilen öğe eklenip kalıyor mu, kapanınca çentik normale dönüyor mu; yeni kurulumda dock varsayılanı Yer ayır, mevcut ayar korunuyor mu. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [ ] **Gerçek donanımda sınanamadı:** WMI / DDC/CI yolları, DisplayConfig dahili-harici ayrımı ve seçicinin ön plana gelmesi bu geliştirmede çalıştırılamadı (uygulama çalıştırılmadı; yalnız derleme, testler ve `tools/Koy.UiSnapshots`).

## H servisleri (çentik H tasarımının veri katmanı; arayüz ayrı iş)
Servisler Core'da sözleşme + Platform'da uygulama; arayüz bağlanmadı. `SharedServices` üzerinden alınır.
- [x] **Pano geçmişi** (`Core/Clipboard/`, `Platform/Clipboard/`): `AddClipboardFormatListener` ile gizli mesaj penceresi (yoklama yok), yalnız metin; sınıflandırma (Bağlantı/Renk/Kod/Metin) saf ve testli; en çok 50 öğe (sabitler sayılmaz), tekrar başa taşınır, 10 000 karakter kesme, arama, tür filtresi, sabitle/çöz, sil, hepsini temizle (sabitler kalır), panoya geri yaz (kendi yazımı yeniden kaydedilmez, sıra numarası aralığıyla). Yalnız sabitler `clipboard-pinned.json`'a. Parola yöneticisi biçimleri (`ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory=0`, `Clipboard Viewer Ignore`) kaydedilmez. Ayar: `ClipboardHistoryEnabled` (varsayılan açık).
- [x] **Sistem verisi** (`Core/SystemStats/`, `Platform/SystemStats/`, `Platform/Diagnostics/ProcessMemory`): CPU (`GetSystemTimes`), RAM (`GlobalMemoryStatusEx`), ağ hızı (`GetIfTable2`, yalnız fiziksel arayüzler), Koy özel çalışma kümesi, ağ adı (`NetworkStatus.Name`). Zamanlayıcı yalnız Start ile, Stop söker. En çok CPU süreci 3 sn'de bir (süreç taraması pahalı). Hesaplar ve Türkçe biçimlendirme (`SystemFormat`) saf ve testli.
- [x] **Hızlı başlat** (`Core/QuickLaunch/`): en çok 8, tekrarsız, sıra korunur, `AppSettings.QuickLaunchItems`'a kalıcı; ekle/kaldır/taşı/başlat (`IShellActionService`). Simge `IconHelper.GetIconBytes`.
- [x] **Odak görevleri** (`Core/Tasks/`): `tasks.json`; Bugün görünümü (saatli → saatsiz → tamamlanan; eski günün tamamlananı gizli, silinmez), gün ve aralık sorgusu, en çok 200 kayıt (eski tamamlananlar önce budanır).
- [x] **Elle sınama** (proje sahibi): tarayıcı/Notepad'den kopyala → listede görünüyor mu; aynı metin başa taşınıyor mu; parola yöneticisinden (Bitwarden/KeePass) kopyalanan kaydedilmiyor mu; panoya geri yaz → başka uygulamaya yapıştır ve listede yeniden çıkmadığını doğrula; Windows Pano Geçmişi (Win+V) ile birlikte çalışıyor mu. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] **Elle sınama**: Sistem verisi çentik açıkken Görev Yöneticisi ile uyuşuyor mu (CPU, RAM, Koy belleği = "Bellek" sütunu, ağ hızı); VPN/Hyper-V varken hız çift sayılmıyor mu; çentik kapanınca `Koy` CPU'su boşta %0. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [ ] Bellek ve CPU ölçümü (AGENTS #1): bu iş ölçülmedi, yalnız tasarım gereği hesaplandı (uygulama çalıştırılmadı). Arayüz bağlanınca `scripts/measure-memory.ps1` ile çentik kapalı/açık karşılaştır.
- [x] Arayüz bağlama: Pano, Sistem, Ana sağ sütunu (CPU / RAM, ağ, hızlı başlat), Odak görev listesi ve Koy penceresinde Pano geçmişi anahtarı bağlandı (aşağıdaki "Çentik H arayüzü" maddeleri). Gün · Hafta · Ay `TaskViews.Load` ile `GetToday` / `GetRange`'e bağlı.

## Çentik H arayüzü
Kaynak tasarım: `docs/design/source/CentikH.dc.html`. Pano, Sistem, görev listesi ve hızlı başlat servisleri ayrı yazılıyor; bu bölüm onlar olmadan H iskeletini ve mevcut paneller içindeki yerlerini kapsar.
- [x] H iskeleti: açık çentik 620×352 (tuval 640×364, AGENTS #13 güncellendi), üstte durum satırı (saat · gün tarih, hava, pil, Bildirim Merkezi, açık tut), altta ikonlu sekme çubuğu; seçili sekme sayfa rengini alır. Sekmeler `NotchTabs` listesinden üretilir.
- [x] Ana: müzik kartı / selamlama + saat, hava durumu kartı (yer adı sıcaklığın üstünde), çalışan zamanlayıcı satırı.
- [x] Odak: Odak · Mola · Sayaç · Kronometre alt sekmeleri, ilerleme halkası, tur bilgisi; sayaç hazır süreleri, kronometre turları.
- [x] Raf ve Kontrol H içine yerleştirildi; Kontrol döşeme sürükleyerek sıralama özelliği ve kayıtlı sıra korundu.
- [x] Kapalı çentikte zamanlayıcı çalışırken pil / Wi-Fi / Bluetooth yerine kalan süre (saniyede bir, yalnız çalışırken).
- [x] Zamanlayıcı sekme değişince durmuyor: sayaç durumu görünümden bağımsız `Core/Timers/FocusTimers` içinde, NotchFeature tek örnek kurar (birden çok monitörde ve pencere yeniden kurulunca da aynı süre). Bulunan gerçek hata: Pomodoro duraklat ya da sıfırla sonrası yeniden başlatılınca süre bir daha akmıyordu (zamanlayıcı sonsuza çekilip yeniden kurulmuyordu); düzeltildi, testli.
- [x] Görsel kanıt: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix h --scene centik --theme dark` (Ana, Odak, Raf, Kontrol, kapalı çentik).
- [x] Elle sına (uygulama çalıştırılarak): sekme geçişinde kayma ve solma; tekerlekle sekme değişimi; "çentiği açık tut"; Bildirim Merkezi düğmesi (Win+N); çalışan zamanlayıcıyla çentik kapanıp açılınca süre; kapalı çentikte süre ve bitişte Peek + titreme; Raf'a dosya sürükleyince Raf'ın açılması; iki monitörde aynı zamanlayıcı; karışık DPI'da 640×364 tuvalin yerleşimi. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Görsel kanıt (H bağlama): `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix hb --scene centik --theme dark` (Ana müzikli / müziksiz / zamanlayıcılı, Odak görevleri ve hafta kapsamı, Takvim ay / hafta / gün, Pano, Sistem).
- [ ] Bellek ve CPU ölçümü: tuval 500×260'dan 640×364'e çıktı; `scripts/measure-memory.ps1` uygulamayı başlattığı (görev çubuğunu gizleyebildiği) için bu çalışmada ölçülmedi. Önce ve sonra ölçüp `docs/MEMORY.md` dosyasına yaz; açık çentik açılış / kapanışında fps ve CPU'ya bak.
- [x] Pano sekmesi: arama, süzgeçler, kart listesi (geri kopyala, sabitle / çöz, sil), Temizle, açıklama satırı. Servis yalnız `ClipboardHistoryEnabled` açıkken alınır (NotchFeature.Start), Stop'ta bırakılır; Koy penceresi › Çentik'te anahtar var, değişince çentik yeniden kurulur. Ayar kapalıyken sekme görünmez.
- [x] Elle sına (Pano): kopyalanan metin listede görünüyor mu; karta tıklayınca panoya yazılıyor mu ve listede yeniden çıkmıyor mu; arama / süzgeç; iğne ile sabitle, çentik yeniden kurulunca sabit kalıyor mu; Temizle; 50+ öğede kaydırma ve tekerleğin sekme değiştirmemesi; ayarı kapatınca sekmenin kaybolması. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Sistem sekmesi: iki halka (işlemci + en çok kullanan, bellek), ağ kartı, Koy belleği. Servis yaşam döngüsü `SystemStatsViewModel.SetActive`: yalnız çentik açık, görünür ve Ana ya da Sistem sekmesindeyken `AcquireSystemStats`, aksi halde `ReleaseSystemStats`.
- [x] Elle sına (Sistem): sayılar Görev Yöneticisi'yle uyuşuyor mu; Ana ile Sistem arasında geçerken servis kesintisiz kalıyor mu; başka sekmeye geçince ve çentik kapanınca `Koy` CPU'su %0'a dönüyor mu. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Odak görev listesi: Odak / Mola sağ kartı (Bugün, sıradaki "şimdi", saatli, tamamla, sil, başlık + isteğe bağlı saatle ekle, Gün · Hafta · Ay). Eski "oturum sayısı" kartı kalktı; sonraki aşamaya geç düğmesi sol karta taşındı.
- [x] Elle sına (Odak görevleri): metin kutusuna tıklayınca klavye çalışıyor mu (NotchWindow pencereyi etkinleştirir); Enter ile ekleme; geçersiz saatte (25:00) eklenmemesi; satıra tıklayınca tamamlanması; Hafta / Ay seçicisi; gün değişince eski günün tamamlananlarının gizlenmesi. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Ana: sağ sütun tasarımdaki gibi (hava kartı kalktı, hava yalnız durum satırında; "Konum seç" durum satırında): CPU / RAM + ağ kartı, hızlı başlat şeridi (tıkla başlat, "+" ile seç, sağ tık kaldır).
- [x] Kulaklık kartı (Ana) ve Kontrol'de "Bağlı cihazlar": aşağıdaki "Wi-Fi ve Bluetooth listeleri" bölümünde yapıldı.
- [x] Elle sına (Ana): hızlı başlat simgeleri, tıklayınca başlatma; "+" dosya seçicisi açılırken çentik kapanmıyor mu (seçici süresince açık tutulur); sağ tık menüsü çentiği kapatmıyor mu; 8 öğede "+" gizlenmesi; durum satırındaki hava (tıklayınca yenilenme) ve "Konum seç". **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Takvim sekmesi (proje sahibi kararı: H içinde yeniden kullanılır): yedinci sekme, `NotchPanel.Calendar` (sıra Ana, Odak, Takvim, Raf, Pano, Sistem, Kontrol), sayfa rengi `#38BDF8`. Ay / hafta / gün görünümü ve yerel etkinlik ekleme-silme (`Core/Calendar` aynen); eski panelin zamanlayıcı araçları kaldırıldı (Odak'ta). Ayarlarda aç / kapat; eski ayar dosyalarındaki `calendar` anahtarı artık Takvim'e gider, H öncesi dosyalarda (`music` / `status` anahtarı olanlar) eskiden Pomodoro da taşıdığı için Odak de açılır (testli).
- [x] Elle sına (Takvim): etkinlik ekle / sil, uygulamayı yeniden aç; iki monitörde aynı liste; ay / hafta / gün gezinmesi; gece yarısını geçince bugünün güncellenmesi. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [x] Cihaz konumunda yer adı: OpenStreetMap Nominatim ters konumlama (`PlaceNameParser`, `PlaceNameService`). Koordinat iki ondalığa (~1 km) yuvarlanır, yalnız çentik açılınca ve koordinat başına oturumda bir kez sorulur; ad çözülemezse "Konumum". Hava isteği de iki ondalığa indi. Hakkında sayfasına OSM (ODbL) atfı, sihirbaz gizlilik metnine OSM eklendi.

## Wi-Fi ve Bluetooth listeleri
Kontrol'deki Wi-Fi / Bluetooth döşemeleri Windows 11 hızlı ayarları gibi bölünmüş: sol bölüm aç/kapa, sağda dar ok (hover'da parlar, basınca vurgulanır); ok çentik içinde (sekme çubuğu kalır) liste sayfasını açar. Döşeme sırası özelliği aynen çalışır.
- [x] `Core/Wireless/` (WifiList: RSSI→kalite, 0-4 çubuk, SSID birleştirme, sıralama, durum, satır eylemi, hata anahtarları; IWifiService) ve `Core/Bluetooth/` (BluetoothList: ClassOfDevice / BLE Appearance → tür, klasik+LE birleştirme, pil biçimi, durum; IBluetoothService). Testli (`WifiListTests`, `BluetoothListTests`).
- [x] Wi-Fi: `Platform/Wireless/WifiService` = `Windows.Devices.WiFi.WiFiAdapter` (RequestAccessAsync, FindAllAdaptersAsync, ScanAsync, NetworkReport, ConnectAsync, Disconnect). Gerekçe: projeli WinRT, ek paket ve el yazısı yapı yok; wlanapi'de BSS listesi, tarama bildirimleri ve profil XML'i (şifre içerir) elle kurulmalı. wlanapi yalnız kayıtlı profil ADLARI için (`WlanProfiles`, WinRT yalnız bağlı ağın profilini verir). Şifre yalnız bellekte (satır alanı → kimlik nesnesi), loglanmaz. Konum izni yoksa (hepsi adsız) açık mesaj + `ms-settings:privacy-location`.
- [x] Bluetooth: `Platform/Bluetooth/BluetoothService` = iki `DeviceWatcher` (klasik + LE, eşleşmiş), pil `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`, bağlantı `System.Devices.Aep.IsConnected`, tür `FromIdAsync` ile bir kez. Bağlan/kes: yalnız ses cihazları, `BluetoothAudioKs` (ses uç noktası → IDeviceTopology → IConnector → IPart → IKsControl, KSPROPSETID_BtAudio ONESHOT_RECONNECT/DISCONNECT); "bağlandı" yalnız bağlantı durumu doğrulayınca (8 sn). Ses dışı cihazlarda satır `ms-settings:bluetooth` açar ve satırda küçük yazıyla belirtilir.
- [x] Servis yaşam döngüsü: `SharedServices.AcquireWifiService/AcquireBluetoothService` sayaçlı. Wi-Fi yalnız liste sayfası açıkken (açılışta bir tarama + 10 sn'de bir; sayfa kapanınca zamanlayıcı ve servis bırakılır). Bluetooth yalnız çentik açıkken Ana ya da Kontrol görünürken ("home" / "control" sahipleri); sahipsiz kalınca izleyiciler durur ve listeler boşalır. Olaylar arka plan iş parçacığından Dispatcher'a geçer.
- [x] Yeni Bluetooth rune simgesi (`IconBluetooth`, 14 px'te açık), Ana kulaklık kartı (bağlı ses cihazı yoksa görünmez; varken CPU kartının ayrıntı satırları gizlenir), Kontrol "Bağlı cihazlar" (ad + pil).
- [x] Görsel kanıt: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix wb --scene centik --theme dark` (kontrol-bolunmus, kontrol-ok-hover, kontrol-ok-basili, wifi-liste, wifi-sifre, wifi-baglaniyor-hata, wifi-izin-yok, bluetooth-liste, ana-kulaklik, ana-kulaklik-yok).
- [x] Elle sınama (gerçek donanım; uygulama çalıştırılmadı): Wi-Fi listesi ve sinyal; bağlı ağ üstte; kayıtlı ağa doğrudan bağlanma (WiFiAdapter kayıtlı profili şifresiz kullanıyor mu); şifre kutusuyla yeni ağ, yanlış şifre; açık ağ; bağlantıyı kes; konum izni kapalıyken mesaj; profil adı SSID'den farklıysa "Kayıtlı" doğru mu; Bluetooth listesi, tür simgeleri, pil yüzdesi; kulaklık bağlan/kes (KS yolu belgesiz ortamda; hangi sürücülerde çalıştığı belirsiz, çalışmazsa "Bağlanamadı"/"Koy bu cihaza bağlanamıyor" görünür); fare/klavye satırı Ayarlar'ı açıyor mu; LE ses cihazları; çift modlu cihaz tek satır mı; çentik kapanınca/sekme değişince Wi-Fi taramasının ve izleyicilerin durması (CPU %0); sekme çubuğu liste sayfasında kalıyor mu; tekerlek. **Proje sahibi sınadı (2026-11-07), sorun yok.**
- [ ] Bellek ve CPU ölçümü (AGENTS #1): ölçülmedi (measure-memory çalıştırılmadı).

## Müzik kartını büyütme (2026-11-07)
- [x] Ana'daki müzik kartına (düğme ve ilerleme dışında) tıklanınca kart Ana gövdesinin tamamını kaplar: 172 px kapak, tam başlık, geniş çubuklar, büyük denetimler; sağ sütun gizlenir, çentik boyutu değişmez. Tekrar tıklayınca eski boyutuna döner. Seçim çentik kapanıp açılınca korunur; parça yokken kartta saat vardır, büyütme etkisizdir.
- [x] Ana kart düğmelerinin boşlukları da tıklanır (hızlı başlat "+" ortasına tıklayınca çentik kapanıyordu).
- [x] Elle sınama: büyütme/küçültme tıklaması, düğmeler ve ilerlemeye tıklayınca kartın büyümemesi, hızlı başlata ekleme. **Proje sahibi sınadı (2026-11-07), sorun yok.**

## Dock'ta uygulama bazlı sağ tık menüsü (atlama listesi) — AÇIK, almak isteyen alabilir

Windows görev çubuğunda bir uygulamaya sağ tıklayınca açılan menünün aynısı. Örnekler: VS Code'da "Yeni Pencere" ve son klasörler, Edge'de "Yeni InPrivate penceresi", Steam'de Mağaza, Kütüphane ve son oynanan oyunlar. Bugün dock menüsünde yalnız yeni pencere, kapat ve görevi sonlandır var.

Araştırma bitti; Windows 11 build 26200'de deneme programıyla doğrulandı. Kod yazılmadı. Tahmini iş ~1,5 gün.

**Doğrulanan bilgiler:**
- **Son / sık kullanılanlar:** belgeli `IApplicationDocumentLists` ile okunuyor (CLSID `86bec222-30f2-47e0-9f25-60d11cd75c28`; `SetAppID`, sonra `GetList(ADLT_RECENT | ADLT_FREQUENT)`).
  - Öğeler `IShellItem` olarak geliyor.
  - Explorer, Chrome, Edge ve VS Code'da dolu; Notepad ve Terminal'de boş.
- **Görevler ve uygulamanın kendi kategorileri:** `%APPDATA%\Microsoft\Windows\Recent\CustomDestinations\<hash>.customDestinations-ms`.
  - Dosya adı, AppID'nin büyük harfe çevrilmiş UTF-16LE hâlinin CRC-64'ü: polinom `0x92C64265D32139A4`, yansıtılmış, başlangıç değeri tümü 1, son XOR yok. Doğrulanan örnekler: `Chrome` → `5d696d521de238c3`, `MSEdge` → `ccba5a5986c77e43`, `Microsoft.VisualStudioCode` → `1ced32d74a95c7bc`.
  - Biçim (sürüm 2): başlıkta sürüm, kategori sayısı ve ayrılmış alan (3 × int32). Kategori türleri:
    - 0: özel kategori. Ad uzunluğu (int16), UTF-16 ad, sayı, öğeler.
    - 1: bilinen kategori. 1 sık kullanılanlar, 2 son kullanılanlar; öğesi yok.
    - 2: görevler. Sayı ve öğeler.
  - Öğe yapısı: CLSID (16 bayt) ve ardından nesnenin `IPersistStream` verisi. Windows'un ShellLink'iyle `Load` edilir. Her kategori `0xBABFFBAB` ile biter.
  - Başlık `PKEY_Title`'da; `@dll,-id` biçimindeyse `SHLoadIndirectString` ile çözülür. Başlığı ve hedefi boş öğe ayırıcıdır.
- **Pencereden AppID'ye:** önce `SHGetPropertyStoreForWindow` → `PKEY_AppUserModel_ID` (Koy'da var). Sabitli uygulamada kısayolun AUMID'i. İkisi de yoksa Windows'un exe yolundan türettiği kimlik; bu yol doğrulanmadı.

**Yapılacaklar:**
- [x] Core (`Core/JumpList/`): AppID hash'i (`JumpListAppId`; dosya adı `%x` ile yazılır, baştaki sıfırlar atılır, diskte 15 haneli adlar var), customDestinations okuyucu (`CustomDestinationsReader`; bozuk / kısaltılmış / bilinmeyen biçim / sınır aşımı boş döner, uydurma öğe yok; dosya, kategori, öğe ve dize için üst sınırlar `JumpListLimits`'te), bölüm sırası ve sınırlar (`JumpListLayout`), sınırlı önbellek (`JumpListCache`), başlık / hedef kuralları (`JumpListEntryFactory`). Testleriyle (`tests/Core.Tests/JumpList/`, `Dock/DockItemAppIdTests`). `DockItem.AppUserModelId` eklendi (çalışan pencerenin AUMID'i, yoksa sabitli kısayolun; exe yolundan türetilmez).
- [x] Platform: `Platform/JumpList/JumpListService`. Yalnız menü açılınca okunur, iki kaynak dosyanın boyut + yazılma zamanına göre en çok 16 uygulama önbelleklenir; zamanlayıcı, izleyici, kalıcı iş parçacığı yok. Her okuma bitince biten kısa ömürlü STA iş parçacığında (kabuk nesneleri apartman ister). Son / sık: `IApplicationDocumentLists`; görevler ve kategoriler: `customDestinations-ms` + `IPersistStream.Load` (ShellLink). Son / sık yalnız uygulamanın listesi istiyorsa (ya da hiç listesi yoksa son kullanılanlar). Başlık `PKEY_Title` (BSTR ya da LPWSTR), `@dll,-id` ise `SHLoadIndirectString`; ayırıcı = boş başlık + boş hedef ya da `IsDestListSeparator`; başlığı boş ama hedefi olan öğe atlanır. COM nesneleri `FinalReleaseComObject` ile bırakılır.
- [x] App: dock menüsü bölümlere ayrıldı (`DockJumpListMenu`). Sıra: uygulamanın kategorileri → sık → son kullanılanlar → görevler → Koy'un kendi öğeleri. Bölüm başına sınır (özel 6, son 5, sık 3, görev 6, en çok 3 özel kategori) ve toplam 14. Tıklanınca görev kısayolu olduğu gibi, belge uygulamanın kendi exe'siyle (olmazsa varsayılan ilişkiyle) çalışır. Okuma 700 ms'yi aşarsa menü listesiz açılır. AppID yoksa yalnız Koy'un öğeleri görünür.
- [x] Snapshot: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix jl --theme both [--scene jumplist]` (VS Code ve Steam benzeri, sınır ve yalnız-Koy sahneleri; sentetik veri).
- [ ] Elle sınama (gerçek uygulamalarla; uygulama çalıştırılmadı): VS Code'da son klasörler + son dosyalar + Yeni Pencere; Edge / Chrome görevleri; Explorer sık kullanılanları; Windows Terminal profilleri; Steam / Spotify; sabitli ama çalışmayan uygulamada menü; AppID'siz klasik uygulamada yalnız Koy'un öğeleri; listedeki girdinin tıklanınca doğru uygulamada açılması (özellikle klasör ve belge girdileri, `.url` oyunlar); menü ekrandan taşmıyor mu (özellikle dock üstte / yanda); sağ tık gecikmesi; menü kapanınca sonraki sağ tıkta liste tazelenmiş mi (uygulama yeni belge açtıktan sonra).
- [ ] Bellek ve CPU ölçümü (AGENTS #1): ölçülmedi (`measure-memory` çalıştırılmadı). Menü açıkken ve kapandıktan sonra private bellek, boşta CPU %0 doğrulanmalı.

**Açık sorular:**
- Spotify öğelerinde `PKEY_Title` boş; başlık başka bir anahtarda olmalı.
- Listeye iğnelenmiş öğeler belgesiz DestList akışında; bu ilk sürümün dışında.
- AUMID'siz klasik uygulamalar için exe yolundan AppID türetme doğrulanmadı.

## İş sırası (proje sahibi kararı, 2026-11-07)
Proje sahibi aşağıdakilerin hepsinin yapılmasına karar verdi. Sıra önerilendir; her iş kendi dalında, sonunda test ve gerekirse bellek ölçümü (AGENTS #1). Ayrıntısı olan işler kendi bölümüne bağlanır.

> **Ajan dağılımı notu:**
> - **Flash ajanına uygun işler:** **1** (Menü/KoyWindow senkronu), **7** (Hızlı başlata klasör ekleme), **8** (RAM: radyo servisi erteleme), **9** (Pil uyarıları OSD), **10** (Bluetooth HUD), **4** (Dock klasörleri — önce *4A Core*, sonra *4B App* olarak iki adımda).
> - **Proje sahibi onayıyla yapılacak git/yayın işleri (kod ajanı dokunmaz):** **2** (`v0.1.2` push/release), **3** (`#9` ve `#11` PR kapatma).
> - **Derin Win32/COM gerektiren işler (Sonnet / Pro ajanına uygun):** **5** (Atlama listesi), **6** (UWP sabitleme), **11** (Mikrofon/kamera noktası).

**Kısa işler (önce):**
1. [x] **Sağ alt menü ve kısayol aç/kapa senkronu (`KoyWindow` ezmesini önle):**
   - **Kök neden:** `SagAltMenuViewModel.ToggleNotch` / `ToggleDock` ve `App.ToggleDockEnabled` zaten `settings.json`'a yazıyor; ancak `KoyWindow` (ayarlar penceresi) daha önce açılmışsa belleğindeki eski `ViewModel.NotchEnabled` / `DockEnabled` değerlerinden haberdar olmuyor ve `KoyWindow` kapatılırken (`Close_Click` / `OnClosing` → `SaveSettingsCommand`) kendi eski değerini diske geri yazıp menünün/kısayolun kaydını **eziyor**.
   - **Yapılacak (`src/App/`):**
     1. `KoyWindow.cs`'e `public static bool IsCreated => _instance is not null;` (veya `SyncFeatureStates(bool? notchEnabled = null, bool? dockEnabled = null)`) ekle; `KoyWindowViewModel`'de `_isLoading = true` koruması altında `NotchEnabled` / `DockEnabled` alanlarını diske yeniden yazmadan güncelleyen `SyncExternalState(bool notchEnabled, bool dockEnabled)` metodu tanımla.
     2. `App.xaml.cs` içinde `UpdateNotchState(bool enabled)` ve `UpdateDockState(bool enabled)` çağrıldığında (hem sağ alt menüden hem `ToggleDockEnabled` kısayolundan gelir), `settings.json`'da ilgili alanın güncel olduğundan emin ol ve `KoyWindow` örneği varsa `SyncExternalState` ile ViewModel'ini güncelle.
     3. `KoyWindow.xaml.cs` içinde `Close_Click` ve `OnClosing` zaten her özellik değişiminde `OnPropertyChanged` ile anında kaydedildiği için, kapanırken `SaveSettingsCommand.Execute(null)` çağırmadan önce ya dış durumu senkronize et ya da yalnız bekleyen değişiklik varsa kaydet.
   - **Doğrulama:** `dotnet build Koy.slnx` ve `dotnet test Koy.slnx` yeşil.
2. [x] **v0.1.2 / v0.2.0 (Yalnız proje sahibi onayıyla — kod ajanı çalıştırmaz):** `main` push edildi ve `v0.2.0` yayınlandı (#18).
3. [x] **#9 ve #11 PR'ları (Yalnız proje sahibi onayıyla — kod ajanı çalıştırmaz):** #9 `main`'de başka yapıyla (Ekranlar sayfası, D2) çözüldü ve kapatıldı. #11 (dock sistem tepsisi) `main`'e entegre edilip birleştirildi; kalan kapsam (taşma simgeleri, balon bildirimleri) "Sonraya kalanlar"da.

**Özellikler:**
4. [x] **Dock klasörleri:** aşağıdaki "Dock klasörleri" bölümü. Flash ajanına verilecekse önce **Adım 4A (Core + Test)**, ardından **Adım 4B (App + UI + Snapshot)** olarak iki parçada çalıştırılır.
5. [x] **Atlama listesi:** "Dock'ta uygulama bazlı sağ tık menüsü" bölümü, araştırma hazır.
6. [ ] **UWP (Store) uygulama sabitleme:** kimlik AUMID, simge `shell:AppsFolder`, pencere eşleme `SHGetPropertyStoreForWindow` (ayrıntı "Sonraya kalanlar"daki maddede). Windows'un sabitleri arasındaki Spotify, WhatsApp gibi uygulamalar dock'ta görünsün.
7. [x] **Hızlı başlata klasör ekleme:**
   - **Mevcut durum:** `Core/QuickLaunch/QuickLaunchService.cs` zaten `Directory.Exists(p)` ve klasör adı gösterimini (`DisplayName`) destekliyor; `IconHelper.GetIconBytes` da klasör simgesi çözebiliyor. Eksik olan yalnız UI'daki seçici.
   - **Yapılacak (`src/App/Features/Home/` + `Resources/`):**
     1. `QuickLaunchViewModel.cs` içine `PickFolderPath` (`Func<string?>`, varsayılanı `DefaultPickFolder`) ve `[RelayCommand] private void AddFolder()` ekle. `DefaultPickFolder` içinde WPF `.NET 10` yerleşik `Microsoft.Win32.OpenFolderDialog` kullan (`Title = Strings.Get("QuickLaunchPickFolderTitle")`, `ResolveDialogOwner` ve `HoldOpen` koruması `Add()` ile birebir aynı).
     2. `HomePanel.xaml` içindeki hızlı başlat `+` düğmesine: sol tık mevcut `AddCommand` (dosya/uygulama seçer) olarak kalsın, sağ tık bağlam menüsü (`ContextMenu`) veya `+` yanında/menüsünde iki seçenek ("Uygulama / dosya ekle…" → `AddCommand`, "Klasör ekle…" → `AddFolderCommand`) bağla; menü açıkken de `HoldOpen` ile çentik kapanmasın.
     3. `Strings.resx` ve `Strings.tr.resx` dosyalarına `QuickLaunchPickFolderTitle`, `QuickLaunchAddFile`, `QuickLaunchAddFolder` anahtarlarını ekle.
     4. `tests/Core.Tests/QuickLaunchTests.cs` içinde klasör yolu ekleme ve görüntüleme testinin bulunduğunu doğrula.
8. [x] **RAM: radyo servisi açılışta yalnız gerektiğinde kurulsun:**
   - **Kök neden:** `NotchViewModel.cs:216` içinde `ShowRadioStatus = _controller.EnabledPanels.Contains(NotchPanel.ControlCenter);` yazılı ve hemen `StartRadioStatus()` çağrılıyor. Oysa kapalı çentikte Wi-Fi/BT simgesi yalnız `IdleDisplay.ShowRadio(Settings.IdleDisplayMode)` doğruysa gösteriliyor (`IdleDisplayMode == 0` yani "Saat, pil ve bağlantılar"). Kullanıcı "Yalnız saat" veya "Hiçbiri" seçtiyse (veya `ShowIdleRadio` false ise) radyo servisinin açılışta kurulmasına hiç gerek yok; Kontrol Merkezi ilk açıldığında (`ControlCenterViewModel.SetActive(true)`) zaten `EnsureRadioService()` ile kuruluyor.
   - **Yapılacak (`src/App/Features/Notch/NotchViewModel.cs`):**
     1. `NotchViewModel` kurucusunda `if (ShowRadioStatus)` yerine `if (ShowRadioStatus && IdleDisplay.ShowRadio(Settings.IdleDisplayMode))` koşuluyla `_controlCenter.StartRadioStatus()` çağır.
     2. Böylece boşta radyo simgeleri kapalıyken `RadioService` (WinRT `Windows.Devices.Radios`) çentik açılışında hiç belleğe yüklenmez, yalnız Kontrol Merkezi sekmesi açılırsa yüklenir.
     3. `tests/Core.Tests` altında `IdleDisplay` testlerini doğrula, `dotnet build` ve `dotnet test` çalıştır.

**E paketi (çentik göstergeleri):**
9. [x] **Pil uyarıları (OSD):**
   - **Hedef:** Şarja takıldı, şarjdan çıkarıldı, düşük pil (%20 ve %10 eşikleri) ve şarj tamamlandı (%100) durumlarında çentikte 1,5 sn OSD göstergesi.
   - **Yapılacak:**
     1. **`Core/Power/BatteryNotificationPolicy.cs` (saf C#, testli):** Önceki ve yeni `PowerChangedEventArgs` durumunu karşılaştırıp `BatteryAlertKind { None, PluggedIn, Unplugged, LowBattery, FullyCharged }` ve yüzde döndüren durum sınıfı yaz. İlk okumada (açılışta) bildirim üretmez; yalnız gerçek geçişte üretir. Düşük pil uyarısı %20 ve %10 altına ilk düşüşte birer kez tetiklenir, şarja takılınca veya %25 üstüne çıkınca sıfırlanır (histerezis). Masaüstünde (`HasBattery == false`) her zaman `None`. Testleri: `tests/Core.Tests/BatteryNotificationPolicyTests.cs`.
     2. **`SharedServices.cs`:** `AcquirePowerService` / `ReleasePowerService` ekle; `NotchFeature` ve `DockFeature` kendi `new PowerService()` örnekleri yerine bunu paylaşsın (WinRT olay aboneliği tek örneğe iner).
     3. **`App/Features/Notch/OSD/`:** `OsdType` enum'una `Battery` ekle; `OsdIndicatorViewModel`'e `ShowBattery(BatteryAlertKind kind, int percent)` ekle. `NotchFeature` (veya `NotchViewModel`) güç değişimini `BatteryNotificationPolicy`'den geçirip `Osd.ShowBattery` çağırsın.
     4. **`Strings.resx` + `Strings.tr.resx`:** `BatteryCharging`, `BatteryOnBattery`, `BatteryLow`, `BatteryFull` metinlerini ekle.
10. [x] **Bluetooth HUD:**
   - **Hedef:** Eşleşmiş bir Bluetooth ses cihazı bağlandığında veya bağlantısı kesildiğinde çentikte kısa OSD göstergesi (cihaz adı + varsa pil yüzdesi).
   - **Yapılacak:**
     1. **`Core/Bluetooth/BluetoothHudPolicy.cs` (saf C#, testli):** İlk cihaz listesi yüklemesinde (tohum) bildirim üretmez; sonrasında bir cihazın `IsConnected` durumu `false → true` veya `true → false` olduğunda `BluetoothHudEvent(string Name, bool Connected, int? BatteryPercent)` üretir. Aynı cihaz için 3 sn içindeki tekrarları (titreşim) bastırır. Testleri: `tests/Core.Tests/BluetoothHudPolicyTests.cs`.
     2. **`App/Features/Notch/OSD/`:** `OsdType.Bluetooth` ve `ShowBluetoothDevice(string name, bool connected, int? battery)` ekle; çentik kapalıyken veya Peek durumundayken 2 sn gösterilir.
11. [ ] **Mikrofon / kamera noktası:** kullanımdayken çentikte nokta (önce olay tabanlı yol araştırılır; yoklama yapılmaz).

**Planda (büyük, sonra):** README güzelleştirme + GitHub Pages tanıtım sayfası (gerçek ekran görüntüleriyle); bildirimleri çentikte gösterme + kod imzası (SignPath; SmartScreen uyarısını da kaldırır); düzenlenebilir çentik arayüzü; Google / Outlook takvim.

**Sonraya bırakıldı:** çentik açık ve müzik çalarken bellek/CPU ölçümü (görselleştirici maliyeti).

## Dock klasörleri (iOS/macOS tarzı) — AÇIK, sıradaki iş
Proje sahibi istedi (2026-11-07). Sabitli uygulamalar dock'ta klasörlere toplanır. Kod yazılmadı. Tahmini iş ~1–1,5 gün.

**Davranış:**
- **Dock'ta görünüm:** klasör tek bir cam kutudur (dock simgesiyle aynı boy, köşe yarıçapı tasarım token'ından). İçinde 2×2 küçük simge olarak en fazla ilk 4 uygulama görünür; daha fazlası panelde.
- **Tıklayınca:** dock'un üstünde cam panel açılır (`Material` ayarına uyar; katı modda opak yüzey). Yay animasyonuyla (`Spring`, `Motion`; hareket azaltma ayarına uyar) klasör kutusundan büyür. İçinde bütün uygulamalar ızgara (4 sütun) olarak durur. Başlıkta klasör adı; tıklayınca düzenlenir (Enter kaydeder, Esc vazgeçer). Uygulamaya tıklayınca başlatılır ve panel kapanır; dışarı tıklayınca ya da Esc ile kapanır. Odak çalma kuralları dock'la aynı (`WS_EX_NOACTIVATE`; ad düzenlerken geçici etkinleştirme, hızlı başlattaki "tutma" mantığı gibi).
- **Sürükleyerek klasör yapma:** sabitli bir uygulama basılı tutulup sürüklenir; başka bir sabitli uygulamanın **ortasında** ~400 ms durursa hedef büyür ve parlar, bırakınca ikisi yeni klasör olur (hedefin yerinde). Simgenin **kenar** bölgesine bırakmak bugünkü gibi sıralamayı değiştirir (`DockReorderHelper`, `Core/Dock/DockReorderMath`). Orta/kenar ayrımı ve bekleme süresi Core'da saf mantık, testli.
- **Ekleme / çıkarma:** uygulamayı klasör kutusunun üstüne bırakınca klasöre girer. Panelden dock'a (panel dışına) sürükleyince klasörden çıkar ve bırakıldığı yere sabitlenir. Panel içinde sürükleyerek sıralama. Klasörde tek uygulama kalırsa klasör dağılır, uygulama klasörün yerine geçer.
- **Sağ tık (klasör):** "Adını değiştir", "Klasörü dağıt" (uygulamalar klasörün yerine sırayla dizilir). Klasör içindeki uygulamanın sağ tık menüsü dock'taki uygulama menüsüyle aynı.
- **Yeni klasör adı (onaylandı):** "Klasör" (TR) / "Folder" (EN), aynı ad varsa "Klasör 2"… (`Strings.resx` + `Strings.tr.resx`).
- **Çalışan uygulama klasördeyse (onaylandı):** uygulama klasörün içinde kalır, klasör kutusunun altında çalışma noktası yanar; panelde o uygulamanın altında da nokta. Klasördeki uygulamanın pencerelerine tıklama / önizleme panelden yapılır.
- **Sabitli olmayan açık uygulamalar** klasöre konmaz (yalnız sabitliler). Windows'un sabit klasörü (`User Pinned\TaskBar\*.lnk`) değiştirilmez; klasörler yalnız Koy'un ayarında.

[x] **Adım 4A — Veri ve kurallar (Core, testli; AGENTS #11):**
- [x] `AppSettings.DockFolders`: `List<DockFolder>`; `DockFolder { Id (kısa guid), Name, Items: List<string> (.lnk adları, DockPinnedOrder'daki gibi) }`. `SettingsJsonContext`'e ekle (`[JsonSerializable(typeof(DockFolder))]`, `[JsonSerializable(typeof(List<DockFolder>))]`); eski dosyada alan yoksa boş liste (göç testli). `Equals`/`GetHashCode` (`AppSettings.cs` 219 / 300 civarı) güncellenir.
- [x] `DockPinnedOrder` öğesi ya `.lnk` adı ya `folder:<Id>`. `Core/Dock/PinnedOrder.cs` klasör öğelerini tanır: klasördeki uygulama dock'ta ayrıca görünmez; kaldırılmış `.lnk` klasörden de düşer; boş kalan klasör silinir, tek kalan dağılır.
- [x] `Core/Dock/DockFolders.cs` (saf): `CreateFromDrop(order, folders, dragged, target)`, `AddToFolder`, `RemoveFromFolder(…, insertIndex)`, `Dissolve`, `Rename`, `ReorderInFolder`, `NextDefaultName`. Her biri yeni sıra + klasör listesi döndürür. `Core/Dock/DockDropZone.cs` (saf): simge dikdörtgeni + imleç konumu → `Before / Merge / After` (orta bölge ~%50 genişlik) ve bekleme süresi dolunca `Merge` geçerli.
- [x] Testler: `tests/Core.Tests/DockFoldersTests.cs`, `DockDropZoneTests.cs` (oluştur, ekle, çıkar, tek kalınca dağıt, aynı ad, kaldırılmış lnk, eski ayar dosyası).

[x] **Adım 4B — App, Görsel ve Sürükleme:**
- [x] `DockItemViewModel` yanında `DockFolderViewModel` (ilk 4 simge, çalışma noktası = içerdekilerden biri çalışıyor mu). Dock öğe listesi karışık tür (uygulama / klasör); `DockCapsule.xaml` ve `DockFullWidth.xaml` için ayrı `DataTemplate` (2×2 ızgara).
- [x] `Features/Dock/Folder/FolderPanel.xaml(.cs)` (cam panel, ızgara, ad düzenleme, sürükleyerek çıkarma). Tepsi penceresindeki açma/kapama kalıbı örnek alınabilir (`TrayButton`: yay animasyonu, dış tıklamada kapanma, yalnız açıkken zamanlayıcı).
- [x] `DockReorderHelper`: sürüklerken `DockDropZone` ile orta/kenar ayrımı; birleştirme önizlemesi (hedef büyür + parlar); bırakınca `DockFolders` çağrılır, `SavePinnedOrder` gibi ayara yazılır (`DockFeature.SavePinnedOrder` genişler: sıra + klasörler birlikte, tek `Save`).
- [x] Sağ tık menüsü (`DockContextMenuManager`): klasör için "Adını değiştir", "Klasörü dağıt".
- [x] Boşta %0 CPU: panel kapalıyken zamanlayıcı/olay yok; simgeler mevcut önbellekten (`IconHelper`). Bellek ölçümü (AGENTS #1, `docs/MEMORY.md`).
- [x] Görsel kanıt: `tools/Koy.UiSnapshots` sahnesi (klasör kutusu 2, 4, 6 uygulamayla; açık panel; birleştirme önizlemesi; koyu ve açık tema).
- [ ] Elle sınama (proje sahibi, Claude ile): sürükle-birleştir, ekle, panelden çıkar, tek kalınca dağılma, ad değiştirme, yeniden başlatınca korunma, çalışma noktası, iki ekranda aynı klasörler, tam genişlik ve kapsül dock.

## Dock: sağ bölüm, kayıp açık uygulamalar, Windows tarzı tam genişlik — AÇIK (2026-11-07)
Proje sahibi istedi. Kod yazılmadı; önce bu plan onaylanır.

**Bugünkü durum (kodda bakıldı):**
- Sağ bölümdeki üç düğme (İndirilenler klasörü, Son kullanılanlar, Sistem tepsisi) Ayarlar > Dock'ta zaten ayrı ayrı kapatılabiliyor (`DockShowFolders`, `DockShowRecents`, `DockShowTray`). Eksik: tam genişlik modunda İndirilenler düğmesi hiç yok ve `ShowFolders` bağlanmamış; kapsülde ayırıcılar düğmeler kapalıyken de kalabiliyor.
- Tam genişlik modu var (`DockFullWidth.xaml`, 44 px, `DockStyle = FullWidth`) ama AppBar ile çalışma alanı yalnız "Yer ayır" yakınlık modunda ayrılıyor (`DockFeature.cs:96`). Diğer modlarda büyütülmüş pencere çubuğun altına giriyor ya da çubuk gizleniyor.
- Proje sahibinin makinesinde: kapsül + "Aşağı yapış", çalışma alanı 1080 değil 1036 px (44 px ayrılmış), yapışmış kapsül ise 46 px ve üstünde 12 px şeffaf pay (`DockMargin 12,12,12,0`). Koda göre bu modda Koy AppBar kaydetmiyor; 44 px'i büyük olasılıkla gizlenen Windows görev çubuğu tutuyor. Ekran görüntüsündeki "boşluk" bu uyumsuzluk olabilir; işe başlarken yerinde ölçülür.

**Haiku iki mod raporu (2026-11-07, kod okuması, çalıştırılmadı):**
- Çalışma göstergesi: kapsülde nokta / çizgi (`DockIndicatorStyle`) / çoklu pencere üç noktası / dikkat turuncusu var (`DockCapsule.xaml:171-236`); tam genişlikte hiçbiri yok, yalnız aktif öğenin adı yazılı hap. Klasör çalışma çubuğu iki modda var.
- Animasyonlar ortak ViewModel'de (gösterme/gizleme yayı, hover büyütme, sıralama kayması, `DockWindow` kare döngüsü); tam genişlikte de çalışıyor. Eksik olan görsel durumlar (nokta, rozet, dikkat). Hareket azaltma dock yaylarında yalnız pil tasarrufuna bağlı; Windows "animasyonları azalt" ve `AnimationSpeed` dock'a uygulanmıyor.
- Basma tepkisi iki modda da yok; öğe stilleri yalnız hover/aktif/hedef/dikkat tetikliyor.
- Sağ bölüm boşlukları: tam genişlikte Başlat `ShowStartButton`'a bağlı değil (hep görünür), İndirilenler yok, ayırıcı yok; kapsülde ikinci ayırıcı hiçbir ayara bağlı değil.
- Pencere süzgeci farkları: `WS_EX_APPWINDOW` istisnası yok, `WS_EX_NOACTIVATE` bakılmıyor, boş başlık eleniyor. Olaylarda `EVENT_OBJECT_UNCLOAKED`, `EVENT_OBJECT_NAMECHANGE` ve sanal masaüstü değişimi yok. `WindowTracker` çentikle ortak: olay eklemek çentiği de etkiler.
- Çalışma alanı: AppBar yalnız "Yer ayır"da; 44 sabiti 6 yerde (`DockPositionResolver.cs:46`, `DockWindow.xaml.cs:31,147,167,169`, `DockFullWidth.xaml:14`, `DockViewModel.cs:669`) — 48'e geçişte tek sabite toplanır. Piksel yuvarlaması üç yerde farklı (`AppBarService` kesme, `WindowPlacementMath` yuvarlama, `DockFeature` tavan) → tek kurala.
- Taşma: kapsülde sınır/küçülme yok, fazlası ekran kenarında kesilir (sağdaki tepsi ve Koy menüsü ilk gider); tam genişlikte orta sütun yan sütunlara taşar.
- Açık uygulama süzgeci (`Platform/Shell/WindowTracker.GetOpenWindows`) Windows görev çubuğunun kuralından ayrılıyor: sahibi olan pencereleri (`GW_OWNER`) `WS_EX_APPWINDOW` olsa bile atıyor, başlığı boş pencereleri atıyor, `WS_EX_NOACTIVATE`/`WS_EX_APPWINDOW` ayrımı yok.

**İşler:**
1. [x] **Sağ bölüm sadeleşmesi:** (kod 2026-11-07, `feat/dock-fullwidth-tray`; elle denenmedi)
   - Üç anahtar tam genişlik modunda da geçerli olsun (İndirilenler düğmesi eklenir, `ShowFolders` bağlanır); kapalı düğmelerin ayırıcısı da gizlenir.
   - **Yeni seçenek "Küçük tepsi"** (proje sahibi onayladı 2026-11-07, varsayılan açık): tepsi düğmesi tam boy simge yerine dar bir ok (`^`, ~18 px genişlik) olarak Koy menüsü düğmesinin **sağına** geçer; tepsi paneli aynı yerden açılır. Son kullanılanlar ve İndirilenler kendi anahtarlarıyla kalır.
2. [x] **Açık uygulamaların bazen görünmemesi:** (kod 2026-11-07, `feat/dock-fullwidth-tray`; yeniden üretme ve elle sınama açık, aşağıya bak)
   - Önce yeniden üretme: görünmeyen uygulama yakalanınca `EnumWindows` dökümüyle (sınıf, stil, sahip, cloak, başlık) hangi süzgece takıldığı bulunur.
   - Süzgeç Windows'un kuralına çekilir (Core'da saf, testli `TaskbarWindowRule`): görünür + cloak değil + (`WS_EX_APPWINDOW` ise her zaman göster; değilse sahipsiz ve `WS_EX_TOOLWINDOW`/`WS_EX_NOACTIVATE` değil). Boş başlık tek başına eleme sebebi olmaz.
   - Olay kapsamı denetlenir: `EVENT_OBJECT_UNCLOAKED`, `EVENT_OBJECT_SHOW`, başlık değişimi, sanal masaüstü değişimi; kaçan olay için yoklama eklenmez.
   - **Taşma:** çok uygulama açıkken kapsül ekran genişliğini geçmez; önce simgeler küçülür (en küçük 24 px), yetmezse kapsül ekran kenarına kadar genişler. Tam genişlikte orta bölüm sağ/sol bölümlere taşmaz (sabit 260 px sütunlar yerine içerik kadar).
3. [x] **Windows tarzı tam genişlik (cam):** (kod 2026-11-07, `feat/dock-fullwidth-tray`; elle denenmedi, uygulama notları aşağıda)
   - Ayarlar > Dock > Stil: "Yüzen kapsül" / "Tam genişlik". Tam genişlik Windows 11 görev çubuğu ölçüsünde: **48 px**, ekran kenarına sıfır boşlukla yapışık, köşe yuvarlaması yok, üst kenarda ince parıltı çizgisi. Başlat solda veya ortada (Windows'taki "Görev çubuğu hizalaması" gibi), uygulamalar ortada, sağda Koy menüsü + küçük tepsi + saat/tarih (isteğe bağlı).
   - Cam: `Material` ayarına uyar (akrilik/mika; katı modda opak). Koy renkleri ve yazı tipleri.
   - **Çalışma alanı:** tam genişlikte "Her zaman göster" seçiliyse AppBar ile tam 48 px ayrılır (piksel/DPI yuvarlaması testli) → büyütülmüş pencere çubuğun hemen üstünde biter, arada boşluk kalmaz. "Otomatik gizle" seçiliyse yer ayrılmaz; fare ekranın en altına değince çubuk çıkar (bugünkü kenardan açma, `EdgeRevealTracker`).
   - Yakınlık modları tam genişlikte sadeleşir: yalnız "Her zaman göster" ve "Otomatik gizle" (Windows'taki gibi); "Aşağı yapış" ve "Kaç" yalnız kapsülde.
   - Çoklu ekran: her ekranda kendi çubuğu (bugünkü dock ayarına uyar).
   - **Çalışma göstergeleri (eksik):** tam genişlikte nokta yok, kapsülde var. Kapsülün gösterge şablonu (nokta / çizgi / çoklu pencere / dikkat / rozet) ortak bir kaynağa alınır, iki mod aynısını kullanır.
   - **Animasyonlar:** yaylar zaten ortak; tam genişlik yeni 48 px düzende de aynı hissi verecek şekilde denenir (hover büyütme 48 px çubukta taşmamalı). Hareket azaltma tek kaynağa bağlanır: pil tasarrufu + Windows "animasyonları azalt" + `AnimationSpeed` dock yaylarına da uygulanır.
4. [x] **Basma hissi (iki modda):** (kod 2026-11-07, `feat/dock-fullwidth-tray`; elle denenmedi) dock'ta uygulamaya basınca simge kısa süre içe çöker (ör. %92 ölçek + hafif koyulaşma), bırakınca yayla geri gelir. Sürükleme başlarsa iptal olur. Hareket azaltmada yalnız renk değişir. Basılı durum `DockEntryViewModel`'de; ölçek hover ölçeğiyle aynı yay hedefini ezmez (basma çarpanı ayrı uygulanır).
**Uygulama notları (2026-11-07, `feat/dock-fullwidth-tray`; spesifikasyondan sapmalar ve bilinçli seçimler):**
- **Kod yapısı:** kapsül ve tam genişlik artık aynı `DockItemsStrip` (uygulama/klasör şeridi, çalışma göstergeleri, rozet, dikkat, basma) denetimini kullanır; ayrı kopya yok. Ortak düğme stili `DockFlatButtonStyle` (Styles.xaml). Saf kurallar Core'da: `DockRightSection`, `TaskbarWindowRule`, `DockFitMath` (simge küçültme + hover tepe ölçeği), `DockProximityMapping`, `DockPress`; piksel dönüşümü tek kural `WindowPlacementMath.ToPixels` (yarım yukarı), `AppBarService` ve pencere yerleşimi aynısını kullanır (`IAppBarService` artık DIP'i `double` alır).
- **Etkin uygulama adı hapı kaldırıldı:** tam genişlikte etkin öğenin yanında başlık yazan hap vardı. Göstergeler kapsülle aynı olsun ve yuvalar eşit genişlikte kalsın (taşma hesabı) diye Windows gibi yalnız simge + gösterge yapıldı. Geri istenirse yuva genişliği değişkenleştirilmeli.
- **Cam / Material:** dock penceresi katmanlı (`AllowsTransparency`) olduğundan DWM akrilik/mika arka planı uygulanamaz. Tam genişlik yüzeyi Material ayarına uyar ama bulanıklık yok: Katı = opak `#121216`, Yarı saydam = %88 opak (ThemeManager'ın pencere yüzeyleriyle aynı değer). Gerçek bulanıklık için ya katmanlı olmayan pencere ya da belgelenmemiş `SetWindowCompositionAttribute` gerekir; ikisi de bu pakette denenmedi. Kapsül yüzeyi değişmedi (hep ~%90 yarı saydam, Material'e bağlı değil). Material, Animasyon hızı ve "animasyonları azalt" değişince dock yeniden kurulur.
- **Yakınlık eşlemesi:** tam genişlikte Aşağı yapış → Her zaman göster (Yer ayır), Çekil → Otomatik gizle (Hep gizli). Eşleme çalışma anında uygulanır (`DockProximityMapping`); Ayarlar'da tam genişliğe geçilince seçili mod da ayar dosyasına eşlenmiş haliyle yazılır (kapsüle dönünce eski Aşağı yapış/Çekil seçimi geri gelmez). Resolver'ın kendi davranışı değişmedi.
- **İkinci ayırıcı** (Koy menüsünden önce) hep görünür; ilk ayırıcı yalnız sağ bölümde görünen bir düğme (İndirilenler, Son kullanılanlar, tam boy tepsi) varken görünür.
- **Taşma:** kapsülde uygun genişlik = monitör genişliği − 24 (pencere kenar boşlukları); tam genişlikte orta sütunun gerçek genişliği (yan sütunlar ortak boyut grubuyla eşit ve içerik kadar, `Grid.IsSharedSizeScope`). Simgeler 24 px'e kadar küçülür; ondan sonra şerit kırpılır (taşan uygulamalar görünmez, kaydırma yok). Sabit paylar (dolgu, ayırıcı) ölçülmüş sabitlerdir; gerçek ölçüyle 1-2 px fark olabilir.
- **Pencere olayları:** `EVENT_OBJECT_UNCLOAKED` ve `EVENT_OBJECT_CLOAKED` (sanal masaüstü geçişi) tek kancada, `EVENT_OBJECT_NAMECHANGE` ayrı kancada; yalnız üst düzey ve görev çubuğu penceresi olanlar, `WindowsChanged` olarak. Başlık değişimi 500 ms'lik tek atımlık birleştiriciyle (`RefreshDebouncer`, arayüz iş parçacığına taşınır); sürekli başlık değiştiren pencere (tarayıcı, terminal) saniyede en çok 2 tazeleme üretir. Yoklama yok. Çentik `WindowsChanged`'e abone olmadığı için etkilenmez.
- **Yapılmadı / açık:** (a) "Önce yeniden üretme" (görünmeyen uygulama yakalanınca `EnumWindows` dökümü) yapılmadı: sorun kodla yeniden üretilemedi, kural Windows'a çekildi; yine görünmeyen uygulama olursa sınıf/stil/sahip/cloak/başlık dökümü alınmalı. (b) Windows saat/tarih alanı eklenmedi (spec'te isteğe bağlıydı). (c) Başlat solda/ortada hizalama seçeneği yok. (d) `DockStyle` değişimi ve çoklu ekran elle denenmedi. (e) Bellek ölçümü yapılmadı. (f) Sağ bölüm düğmelerine (Başlat, İndirilenler, Koy menüsü) basma geri bildirimi eklenmedi; yalnız uygulama ve klasör öğelerine.
- **Çalışma alanı salınımı (2026-11-07, elle sınamadan sonra) — kök neden ve ölçüm:** 1920x1080, kapsül + Yer ayır, iki büyütülmüş pencere arasında 5 sn'de bir geçiş, SPI_GETWORKAREA 20 ms'de bir örneklendi. Her geçişte çalışma alanı alt sınırı 1034 → 1080 (≈100-450 ms) → 1034, büyütülmüş pencere 1042 → 1088 → 1042; aynı anda Gezgin'in Shell_TrayWnd'i görünür (v1) oluyor. Koy günlüğü (geçici ölçüm kaydı) her seferinde `Hide()` çağrısını gösterdi. Zincir: `TrayHostService.Resync` Gezgin'in tepsisi öne geçince (her ön plan değişimi) en sık 5 sn'de bir `TaskbarCreated` yayınlar → yayın Koy'un `TrayIconManager` penceresine de gelir → `ExplorerRestarted` → `DockFeature.ReapplyTaskbarHiding` (`TaskbarService.Hide` = ABM_SETSTATE + AppBar ABM_REMOVE/NEW) → Gezgin çalışma alanını yeniden hesaplar. İkinci besleme: `MonitorService` imzası WorkArea içeriyordu; Koy'un kendi AppBar'ı ve Gezgin'in her oynatması "ekran düzeni değişti" sayılıp `OnMonitorsChanged` → `ApplyTaskbarPolicy` → `Hide()` çağırıyordu. Düzeltmeler: kendi yayın yankısı yok sayılır (`TaskbarCreatedPolicy`), imzadan WorkArea çıkarıldı (`MonitorLayoutSignature`), `Hide()` zaten otomatik gizlemedeyse SETSTATE göndermez, AppBar `UpdatePosition` aynı alan için tekrar etmez, `Hide()` sonrası pay hemen yeniden iddia edilir. Denenmedi: ABN_POSCHANGED ile yeniden iddia (kendi SETPOS'umuzla Gezgin arasında ping-pong riski; ölçülmeden eklenmedi). Düzeltme sonrası canlı yeniden ölçüm yapılamadı: çalışan Koy'u kapatma izni verilmedi.
- **Elle sınama listesi:** bkz. aşağıdaki Doğrulama maddesi; eklenenler: küçük tepsi okunun konumu (Koy menüsünün sağı) ve tepsi paneli, tam genişlikte İndirilenler/Başlat anahtarları, tam genişlikte ayırıcıların gizlenmesi, 125%/150% ölçekte büyütülmüş pencere ile çubuk arasında boşluk olmaması, Otomatik gizle, uygulama basınca çökme ve sürüklemede iptal, Windows "animasyonları azalt" açıkken yalnız renk, çok uygulamada küçülme.

5. [ ] **Doğrulama:** Core testleri (pencere kuralı, AppBar yüksekliği, taşma ölçüsü), UiSnapshots sahnesi (tam genişlik açık/koyu, küçük tepsi), bellek ölçümü, elle sınama (büyütülmüş pencere ile boşluk yok, otomatik gizle, iki ekran, tepsi paneli).

## Tam genişlik dock'ta ikon hizası (2026-11-08)
Proje sahibi istedi. Yalnız tam genişlik çubuk; kapsül değişmez.
- [x] `AppSettings.DockIconAlignment` (`Left` / `Center` / `Right`, varsayılan `Center`); Koy › Dock'ta yalnız tam genişlikte görünen "İkon hizası" satırı. Değişince yalnız ViewModel yeniden kurulur, görev çubuğu ve AppBar oynamaz. Testli.
- [x] Orta hizada yan sütunlar eşit kalır (ikonlar ekranın tam ortasında); sola ve sağa hizada serbest bırakılır, yoksa ikonlarla Başlat ya da sağ düğmeler arasında boşluk kalır.
- [x] Görsel kanıt: `--scene dockbar` (`dockbar-tam-genislik-sola`, `dockbar-tam-genislik-saga`).
- [ ] Elle sınama (proje sahibi): Sol / Orta / Sağ geçişi çalışan dock'ta; yeniden başlatınca korunma; ikon sürükleyerek sıralama, klasör paneli ve pencere önizlemesi üç hizada doğru yerde mi; iki ekran.

## Başlat düğmesi simgesi (2026-11-09)
Proje sahibi istedi. Dock'un Başlat düğmesi artık varsayılan olarak Koy'un kendi simgesini gösterir; Windows logosu seçenek olarak kalır.
- [x] `AppSettings.DockStartIcon` (`Koy` varsayılan / `Windows` / `Custom`) ve `DockStartIconPath`. Eski `settings.json` dosyaları alansız yüklenir ve Koy simgesini alır (Windows logosu görenler için bu bilinçli bir varsayılan değişikliği). Karar mantığı `Core/Dock/DockStartIcon.cs` (`DockStartIconResolver`): yol boşsa, uzantı PNG/JPG/ICO değilse ya da dosya yoksa Koy simgesi. Testli.
- [x] Koy simgesi `assets/branding/koy-symbol-small.svg`'nin vektör karşılığı (`KoyStartIconImage`, `Styles.xaml`): 16-18 px'te her DPI'da keskin. Özel dosya `DockStartIconLoader`'da `DecodePixelWidth=64` ile çözülüp dondurulur (ICO'da hedefe en yakın kare seçilir); bozuk, okunamayan ya da 20 MB üstü dosya Koy simgesine düşer, dock çökmez.
- [x] Değişince yalnız ViewModel yeniden kurulur (`DockSettingsDiff`); görev çubuğu, AppBar ve ekranlara dokunulmaz. Testli.
- [x] Koy › Dock › Bölümler: "Başlat düğmesi simgesi" satırı (Koy / Windows / Özel). Başlat düğmesi kapalıysa satır görünmez. Dosya penceresi yalnız "Özel"e ya da "Görsel seç"e tıklayınca açılır, ayar yüklenirken asla açılmaz; iptalde geçerli dosya yoksa Koy'a dönülür.
- [x] Görsel kanıt: `--scene dockbar` (`dockbar-*-baslat-windows`, `-ozel`, `-bozuk`, `-yok`; varsayılan Koy simgesi bütün öteki dockbar çekimlerinde) ve `--scene all` (`koy-dock`).
- [ ] Elle sınama (proje sahibi): Koy / Windows / Özel geçişi çalışan dock'ta (kapsül ve tam genişlik), yeniden başlatınca korunma, yüksek DPI'da keskinlik, PNG / JPG / ICO seçimi, seçili dosyayı silip dock'u yeniden kurma (Koy'a dönmeli).

## Güncelleme akışı yeniden: sor, güncelle, "neler geldi" — AÇIK, yapılacak (2026-11-07)
Proje sahibi istedi. Kod yazılmadı.

**Bugün:** `AutoUpdater` açılıştan 45 sn sonra sessizce denetler, indirir ve Koy kapanınca uygular (`WaitExitThenApplyUpdates(silent: true)`). Ayarlar › Genel › "Şimdi denetle" de aynı yolu çalıştırır. Kullanıcı neyin geldiğini görmez.

**Yeni akış:**
1. **Açılışta yalnız denetim:** Koy açılınca (mevcut 45 sn gecikme ve `AutoCheckUpdates` ayarı korunur) yalnız yeni sürüm var mı diye bakılır; indirme ve kurma yapılmaz. Ağ yoksa ya da hata olursa sessiz geçilir (`UpdateStatus`'a yazılır).
2. **"Yeni güncelleme var" düğmesi:** güncelleme varsa "Koy'dan çık" düğmesinin hemen üstüne vurgu renginde bir düğme eklenir ("Yeni sürüm: 0.x.y"). Yer: Koy penceresinin sol alt köşesindeki "Koy'dan çık"ın üstü ve sağ alt menüdeki çıkış düğmesinin üstü. Güncelleme yoksa düğme hiç görünmez.
3. **Onay penceresi:** düğmeye basınca Koy tasarımında küçük bir pencere: "Koy 0.x.y sürümüne güncellemek istiyor musunuz?" + kısa özet (sürüm notunun "Öne çıkanlar" bölümü) + **Evet** / **Hayır**. Hayır pencereyi kapatır, düğme yerinde kalır.
4. **Güncelleme:** Evet'e basınca aynı pencerede ilerleme çubuğuyla indirme (Velopack `DownloadUpdatesAsync` ilerleme geri çağrısı), sonra Koy kapanıp güncellemeyi uygular ve yeniden açılır (`ApplyUpdatesAndRestart`). Görev çubuğu güvenlik kuralı geçerli: kapanmadan önce Windows görev çubuğu geri getirilir.
5. **"Güncellendi" ekranı:** yeni sürüm ilk açılışta (önceki sürüm ayarda tutulur, ilk açılışta karşılaştırılır) "Koy 0.x.y'ye güncellendi" ekranı çıkar, ardından **"Neler geldi"** ekranı: sürüm notu afilli bir tasarımla (başlıklar, öne çıkanlar kartları, Koy renkleri ve yay animasyonları; hareket azaltmaya uyar). En sonda **Bitti** düğmesi.
6. **Sürüm notunun kaynağı:** pakete gömülü `CHANGELOG.md`'nin o sürüm bölümü (çevrimdışı da çalışır) ya da GitHub release gövdesi; tercih paket içi.
7. **"Şimdi denetle":** Ayarlar › Genel'deki düğme yalnız denetler; varsa aynı "Yeni güncelleme var" düğmesini ve onay penceresini gösterir, kendiliğinden indirmez.
8. **Doğrulama:** Core testleri (sürüm karşılaştırma, "ilk açılış mı" kararı, sürüm notu bölümü ayıklama), UiSnapshots sahneleri (düğme, onay, ilerleme, güncellendi, neler geldi; açık ve koyu), elle sınama: 0.2.0 kuruluyken yeni sürüme güncelleme.

## Çentikte arama paneli (2026-11-08)
`Win+Shift+Q` çentiği açar, içinde arama çıkar; durum satırı ve sekmeler o sırada gizlidir. `Alt+Shift+F2` panodaki metinle açar. Çentik kapalıysa arama da yoktur. İki kısayol da Koy › Kısayollar'dan değiştirilir.
- [x] **Core (`Core/Search/`, testli):** Türkçe harf eşleme, puanlama, kategori sırası (uygulamalar hep üstte, dosyalar hep altta, arası önem sırasıyla) ve 15 satır sınırı, geçmiş (50 kayıt, 20'si görünür, başı boşluklu arama yazılmaz), öneri ve otomatik tamamlama, satır eylemleri, onay, sonuç gelmeden basılan Enter.
- [x] **Kaynaklar:** uygulamalar, pencereler, hesap (işlevler, hex/bin/oct), oturum komutları, komut satırı (PATH), konumlar ve adresler, bilinen klasörler, son belgeler, dosyalar (Windows dizini, türe göre gruplu), web kısayolları (`gg:`, `!gg`, varsayılan arama), yer imleri (Chrome ailesi), süreç sonlandırma (`kill ad`), pano, tarih ve saat, `?` yardımı. Her biri Koy › Çentik'ten kapatılabilir.
- [x] **Klavye:** oklar, Ctrl+J/K, Ctrl+oklar ve PageUp/Down (kategori), Home/End, Tab (eylemler), Üst+Enter (ilk eylem), sağ ok (öneriyi al), boş kutuda aşağı ok (son aramalar), boş kutuda Enter ve Esc (kapat).
- [x] **Doğrulandı (deneme programı, salt okunur):** 241 uygulama 468–687 ms, dizin sorgusu 16–130 ms, özel karakterli sorgu hatasız, simgeler geliyor.
- [ ] **Elle sınama (proje sahibi):** kısayolla klavye odağı (en büyük risk); Esc sonrası odağın önceki pencereye dönmesi; her kaynaktan bir sonuç çalıştırma; kapat/yeniden başlat onayı; iğne; iki ekran; tam ekranda kısayolun bir şey yapmaması.
- [ ] Bellek ve CPU ölçümü (AGENTS #1): panel açıkken ve kapanınca.
- [ ] **Henüz yok:** birim çevirici, sözlük, özel karakter, renk, yazım denetimi; Firefox yer imleri (SQLite paketi gerekir); uygulamaların atlama listesi eylemleri (#26 sonrası); uygulama mağazası araması; sonuç sürükleme; ayrı süreçte eklentiler (AGENTS #4 izin vermez).

## Kısayollarda serbestlik (#47)
- [x] Ana değiştirici zorunluluğu kalktı: `F9`, `Shift+F9`, tek başına harf (uyarıyla) atanabilir.
- [x] Yalnız değiştiriciler (tek başına `Win`) ve Windows'un kendi kısayolları klavye kancasıyla alınır (`KeyboardHookEngine`, `KeyboardHookService`). Kanca yalnız böyle bir kısayol atanmışken ya da yakalama sürerken kuruludur.
- [x] `Win+L` ve `Ctrl+Alt+Del` ezilemez, reddedilir. Başka uygulamanın tuttuğu Win'siz kısayol hata olarak görünür.
- [ ] Elle sınama (proje sahibi): tek başına `Win` Başlat'ı açmadan aramayı açıyor; `Win+S` ezildiğinde Başlat açılmıyor; yakalama kutusu `Win`'i alıyor; yönetici penceresi öndeyken davranış; bellek ve gecikme.

## Bağlı Bluetooth cihazlarında pil seviyesi (#50)
- [x] **Araştırma (`arastirma/21-bluetooth-pil.md`):** Windows `DEVPKEY_Bluetooth_Battery` alanı boş dönen bağlı LE klavye/fare/kulaklıklar için WinRT üzerinden standart GATT Battery Service (`0x180F` / `0x2A19`) yedek okuma yolu.
- [x] **Core (`Core/Bluetooth/`, testli):** `GattBatteryParser` (0-100 aralığı ve çoklu pil durumunda en düşük pil seçimi) ve `BluetoothBatteryFilter` (tuş basımı/RGB voltaj sıçramalarını süzen histerezis filtresi).
- [x] **Platform (`Platform/Bluetooth/BluetoothService.cs`):** Yalnız `DEVPKEY_Bluetooth_Battery` boş döndüğünde ve ilgili sayfa görünürken GATT okuması; bağlantı kopup yeniden sağlandığında ve 60 sn sonra tazeleme.
- [ ] Elle sınama (proje sahibi): Pil göstermeyen BLE fare veya klavyenin çentikte pilinin görünmesi, pil zıplamalarının filtrelenmesi.

## Çentikte sesli komut ve harness (#48)
- [x] **Araştırma (`arastirma/22-sesli-komut-harness.md`):** WinRT `SpeechRecognizer` dikte mimarisi, güvenli argüman listesiyle harness yürütme ve çentik OSD durum makinesi.
- [x] **Core (`Core/Voice/`, testli):** `VoiceSessionController` durum makinesi (Idle -> Editing -> Running -> Completed/Failed), `VoiceCommandParser` (Claude `-p --`, Codex `exec --`, Antigravity `agy -p` ve özel kalıp ayrıştırma, kabuk enjeksiyon koruması; tire ile başlayan istem seçenek sayılmaz). Hata metni üretmez, `VoiceFailure` kodu verir; metin `Strings.resx` (EN/TR) içinde.
- [x] **Platform (`Platform/Voice/`):** `VoiceTyping` (kısayol tuşları bırakılınca Win+H gönderir) ve `ProcessHarnessRunnerService` (argüman listesiyle güvenli çalıştırma, iptal edilebilir süreç ağacı yönetimi; `HarnessExecutableResolver` PATH'te `.exe` arar, npm'in `.cmd` shim'ini açıkça bildirir).
- [x] **Konuşma (karar 2026-11-09):** `SpeechRecognizer` bırakıldı, yerine Windows sesle yazma (Win+H). Sebep: Windows konuşma tanıma Türkçe desteklemiyor ve ayrı dil paketi istiyordu; sesle yazma klavye dilinde (Türkçe, İngilizce…) yazar. Çentikte komut kutusu açılır, kullanıcı konuşur ya da yazar, Enter çalıştırır. Otomatik onay kaldırıldı: sesle yazmanın "bitti" anı belli değil, yarım komut çalışmasın.
- [x] **Gizlilik (karar 2026-11-09):** Sesle yazma Microsoft'un çevrimiçi konuşma tanımasını kullanır; ses yalnız kutu açıkken gider ve Ayarlar › Çentik'teki harness açıklamasında yazıyor.
- [x] **Arayüz (App):** `Ctrl+Alt+V` varsayılan kısayolu, çentikte 260×40 OSD ses göstergesi, onaylama için geçici pencere odak yönetimi (Enter/Esc veya çentiğe tıklama), Ayarlar › Çentik altında Harness ve onaylama ayarları.
- [ ] Elle sınama (proje sahibi): `Ctrl+Alt+V` ile konuşma, metni onaylayıp çalıştırma, model çıktısının çentikte görünmesi.

## Dock önizlemesine orta tık pencereyi kapatır (#61)
- [x] Önizlemedeki küçük resme (tek pencere ve grup) orta tık o pencereyi kapatır, önizleme kapanır; gruptaki diğer pencereler kalır. `MiddleClickAction` ayarına bakılmaz. `DockWindow.xaml(.cs)`.
- [x] Elle sınama (proje sahibi, 2026-11-11, hepsi geçti): tek pencereli uygulamada kapanma; üç pencereli Not Defteri'nde yalnız ortadaki; kaydedilmemiş belgede "kaydet?" sorusu ve Koy donmaması; orta tık simgede eski davranış; sol tık önizlemede pencereyi öne getirir.

## Şarkı sözleri (#57) sonrası karar
- [x] Sözler varsayılan kapalı (çalan başlık, tarayıcı videoları dahil, LRCLIB'e gider). Ana sayfadaki müzik kartında küçük düğmeyle açılıp kapanır, ayar kalıcı kaydedilir; Ayarlar › Çentik'teki seçenek aynı ayar.

## Sonraya kalanlar
- CsWin32 0.3.106 → güncel sürüme geçiş: 0.3.369'da üretilen türler değişti (Platform'da 70 derleme hatası: HWND/işaretçi imzaları, `unsafe` gereken yerler). Platform ve Watchdog birlikte, aynı sürüme geçirilir; Watchdog'un görev çubuğu geri getirme yolu elle sınanır. O zamana dek Dependabot bu paketi yoksayar (`.github/dependabot.yml`).
- Bildirimleri çentikte gösterme: Windows bildirimleri gelince çentik kısa süre gösterir (Claude Code gibi ajanların "iş bitti" bildirimleri de bu yoldan gelir). `UserNotificationListener` paket kimliği ister; Velopack kurulumu için seyrek (sparse) paket ve imza gerekir. Ara çözüm: Claude Code kancasının çağırdığı yerel komutla doğrudan bildirim.
- Kullanıcının düzenleyebildiği çentik arayüzü: sayfaların ve kartların sırası, hangilerinin görüneceği kullanıcı tarafından seçilir.
- Google / Outlook takvim (OAuth).
- Tepsi: gizli (taşma) simgeleri ayrı gösterme, balon bildirimleri (`NIF_INFO`), animasyonlu simgelerin ölçümü.
- UWP (Store) uygulamalarının dock'ta sabitlenmesi: kimlik AUMID, simge `shell:AppsFolder`, pencere eşleme `SHGetPropertyStoreForWindow`. Windows'un sabitleri arasında Spotify, WhatsApp gibi UWP uygulamalar var; Koy bugün yalnız `.lnk` sabitlerini gösteriyor.
- Kod imzalama.

## Şarkı sözleri (2026-11-09)
Tasarım ve doğrulama: `docs/ARASTIRMA-SARKI-SOZLERI.md`.
- [x] `Core/Lyrics/`: LRC ayrıştırıcı, etkin satır hesabı, sorgu temizliği, LRCLIB yanıt çözümü, yerel kaynak, disk önbelleği, kaynak zinciri, oturum durumu. Testli. `Core/Media/PlaybackClock` konum hesabını tek yerde toplar.
- [x] `Platform/Lyrics/LrclibSource.cs`: önce tam eşleşme, sonra arama; geçici hatalar önbelleğe yazılmaz.
- [x] Ana sayfadaki müzik kartında (küçük ve büyük) `LyricsView`: etkin satır yayla kayar, satıra tıklayınca o yere gider. Ağ ve zamanlayıcı yalnız Ana sayfa görünürken; söz varken ses çubukları için yakalama durur, söz yoksa çubuklar kalır.
- [x] Ayar: `MusicLyricsEnabled` (Çentik sayfası, varsayılan açık); kapalıyken söz yok, istek yok.
- [ ] Elle sınama (proje sahibi): Spotify ve tarayıcıda senkron, duraklat/devam, satıra tıklama, parça değişimi, çevrim dışı, çok monitör.
- [ ] Bellek ölçümü (AGENTS #1): sözler kapalı ve açıkken, `docs/MEMORY.md`.

## Uygulamada İngilizce (2026-11-08)
- [x] Görünen sabit metinler resx'e alındı; hava durumu ve gizlilik metinleri Core'dan anahtar ya da kalıpla geçer, `SystemFormat` kültürü dışarıdan alır.
- [x] İlk açılış dili sistem diline göre (`AppLanguage`); kayıtlı ayar korunur.
- [x] "Neler Geldi" uygulamanın diline göre: `CHANGELOG.md` (İngilizce, release-please), `CHANGELOG.tr.md` (Türkçe, sürüm PR'ında elle). O dilde not yoksa öbür dile düşer. Güncelleme penceresi için iki dil tek paket notunda (`ChangelogParser.TurkishNotesMarker`).
- [x] CI: sürüm PR'ında iki günlükte de yeni sürüm başlığı olmalı (`scripts/check-changelogs.ps1`).
- [ ] Elle sınama (proje sahibi): iki dilde çentik, dock, sihirbaz ve "Neler Geldi"; İngilizce Windows'ta ilk açılış.

## Başlat düğmesi sağ tık menüsü (2026-11-09)
- [x] Dock'taki Başlat düğmesine sağ tık, Windows'un Win+X menüsünün Koy tasarımındaki karşılığını açar (yüzen ve tam genişlik dock). Gerçek Windows menüsü açılmaz.
- [x] **Core (`Core/StartMenu/`, testli):** Windows 11 sırası ve grupları (Yüklü uygulamalar ... Bilgisayar Yönetimi / Terminal, Terminal (Yönetici) / Görev Yöneticisi, Ayarlar, Dosya Gezgini, Ara, Çalıştır / Kapat veya oturumu kapat alt menüsü / Masaüstü), pilsiz makinede Mobilite Merkezi yok, her öğenin eylem türü ve hedefi. `StartMenuActions` hataları yutar; reddedilen UAC istemi sessiz, Windows Terminal yoksa PowerShell.
- [x] **Platform (`Platform/StartMenu/StartMenuHost.cs`):** kabuk üzerinden başlatma (`ms-settings:`, `.msc`, `taskmgr`, `explorer`), Win+D / Win+S / Win+R, pil sorgusu (`GetSystemPowerStatus`, menü açılırken tek çağrı). Oturum komutları aramanın `SystemCommandService`'ini kullanır. Yeni zamanlayıcı ya da servis yok.
- [x] **App:** `DockStartMenu` menüyü `DockContextMenuManager` ile açar (menü açıkken dock gizlenmez); `MenuItem` şablonuna alt menü (Popup ve ok) eklendi. Metinler `StartItem*` anahtarlarıyla iki dilde, Windows'un kendi ifadeleriyle.
- [x] Görsel kanıt: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix baslat --theme both --scene startmenu` (TR ve EN, pilli, pilsiz, alt menü açık).
- [ ] Elle sınama (proje sahibi): iki dock biçiminde sağ tık; alt menünün açılıp tıklanabilmesi ve dışarı tıklayınca kapanması; Terminal (Yönetici) UAC'i reddedince hiçbir şey olmaması; Windows Terminal yokken PowerShell'e düşmesi; Masaüstü, Ara, Çalıştır; pilsiz makinede Mobilite Merkezi'nin görünmemesi.

## Tanıtım sayfası (2026-11-07)
- [x] `site/` (düz HTML/CSS/JS, derleme yok): hero (çentik sayfanın tepesinden sarkar, yayla açılır), çentik sekmeleri (gerçek ekran görüntüleri), dock, hafiflik, ayrıntılar, kurulum (SmartScreen uyarısı ve SHA256 doğrulama dahil). Koyu/açık tema sistemi izler. Son sürüm numarası GitHub API'den gelir, gelmezse rozet görünmez.
- [x] `.github/workflows/pages.yml`: `main`'e `site/**` girince koyhq.github.io/koy adresinde yayınlar (SHA'ya sabit action'lar). Pages kaynağı "GitHub Actions" olarak açıldı.
- [x] Depo ve org "Website" alanı siteye bağlandı; sosyal medya görseli (mavi, yalnız yazı, `scripts/generate-og-banner.py`) ve `og`/Twitter etiketleri yayında; hero başlığı "Masaüstünü hafifçe değiştir."; org avatarı ve social preview yüklendi.
- [x] Hero çentiği her masaüstü genişliğinde (900 px ve üstü) sayfanın en tepesinden sarkar; dar masaüstünde (900–1359 px) üst çubukta yalnız logo ve İndir kalır.
- [ ] README rozetleri ve uygulamadaki Hakkında düğmesi site adresine bağlanır (güncelleme akışı GitHub Releases'ta kalır). Alan adı alınırsa `canonical` ve `og:url` güncellenir.
- [x] Hero'daki çentik fareyle açılır olsun (Dock bölümündeki demo gibi: üst kenara gelince yayla açılır, çıkınca 400 ms sonra kapanır; dokunmatikte dokununca).
- [ ] İngilizce sürüm (şimdilik yalnız Türkçe).
- [ ] Uygulamadaki Hakkında sayfasına teşekkür ve tanıtım kartı (Nectar ve Seelen UI esinlenmesi, site bağlantısı; TR ve EN).

## Çentik kartı altyapısı ve güncelleme kartı (#65)
- [x] **Core (testli):** `Core/Notch/NotchCard.cs` (içerik türünden bağımsız kart modeli: `Kind`, `Title`, `Subtitle`, `PrimaryActionLabel`, `AppName`, `Timestamp`, `Icon`; `NotchLayout.CardWidth/CardHeight` 380×72, tuvalin içinde) ve `Core/Updates/UpdateCardPolicy.cs` (sürüm başına oturumda bir kez, × sonrası bir daha yok, farklı sürüm yeniden, çentik açıkken bekler, 15 sn sonra kapanır).
- [x] **App:** `OsdType.Card`, `OsdIndicatorViewModel.ShowCard` + `PrimaryActionRequested` / `DismissRequested`, `OSD/NotchCardView.xaml` (36 px daire içinde indirme simgesi, iki satır metin, hap "Güncelle" düğmesi ve ×; düğmeler `Border` + `MouseLeftButtonUp`, çentik odak almaz ve karta basmak çentiği açmaz). Çentik kart ölçüsüne mevcut `Spring` ile büyür; yeni animasyon yok.
- [x] **Bağlama:** `NotchFeature`, `UpdateStatus.Changed` ile yalnız birincil ekranın çentiğinde kartı gösterir; çentik açıksa kapanınca gösterir; "Güncelle" Koy penceresini açıp var olan `OpenUpdateOverlay` akışını çağırır (görev çubuğu güvenliği `StartUpdateAsync` içinde, AGENTS #7). Metinler `UpdateCard*` anahtarlarıyla iki dilde. Yeni ayar yok.
- [ ] Elle sınama (proje sahibi): kart yayla açıldı mı, × kapattı mı (aynı sürüm bir daha çıkmıyor), Güncelle Koy penceresini ve onay katmanını açtı mı, 15 sn sonra kapanıyor mu, çentik açıkken bekleyip kapanınca çıkıyor mu, ikinci ekranda çıkmıyor mu, hareket azaltmada yay kısa mı.
- [ ] Bellek ölçümü (AGENTS #1): kart görünmezken ek nesne yok; ölçüm `docs/MEMORY.md`.

## Windows bildirimleri çentikte (#64)
Karar (2026-11-11): paketsiz süreçte `NotificationChanged` aboneliği 0x80070490 ile reddedilir, liste okuması çalışır; bu yüzden olay değil **yoklama**. Deneme sonucu: `arastirma/23-bildirim-dinleyici-denemesi.md` (depo dışı).
- [x] **Core (testli):** `Core/Notifications/` : `NotificationItem` + `INotificationListener` (WinRT tipi yok: simge `byte[]?`, kimlik `uint`), `NotificationDiff` (ilk okuma temel çizgi, açılışta eski bildirim yağmaz), `NotificationPoller` (2 sn aralık, tek görevli döngü, hata sonrası 10 sn), `NotificationQueue` (tek kart, 6 sn, en çok 5 bekleyen, çentik açıkken bekler, Koy ve Rahatsız Etme süzgeci), `NotificationPresentation` ("UYGULAMA · şimdi", kısaltma). `NotchCard.Header` eklendi.
- [x] **Platform:** `Platform/Notifications/WinRtNotificationListener.cs` (liste yoklama; ayrıntı ve simge yalnız yeni kimlikler için okunur, simge null olabilir); `FocusAssistService.IsDoNotDisturb`.
- [x] **App:** `SharedServices.AcquireNotificationListener` (sayaçlı); `NotchFeature` dinleyiciyi yalnız ayar açık + çentik var + izin `Allowed` iken başlatır (durum belirsizse izin istenir), kuyruktan birincil ekranın çentiğine `ShowCard`; `NotchCardView` simge/üst satır; ayarlar `NotchShowNotifications` (varsayılan açık), `NotchNotificationsHideBody` (varsayılan kapalı), `CentikPage` satırları ve "Windows izni gerekiyor" açıklaması; metinler iki dilde.
- [x] Yoklama istisnası `AGENTS.md` kural 5 ve `docs/MAP.md` içinde belgelendi.
- [x] Elle sınama (proje sahibiyle, 2026-11-11, geçti): Claude ve PowerShell bildirimleri kart oldu; simge yedeği (boş daire yerine uygulama simgesi); × ve sıradaki kart; art arda kartlar 6 sn aralıkla; çentik açıkken bekleyip kapanınca çıkma; "İçeriği gizle"; Odaklanma oturumunda susma.
- [x] Son derlemede sınandı (2026-11-11): Lightshot'ın yenilenen bildirimi kart olarak çıkıyor (Lightshot logo ve kabuk simgesi vermiyor, zil yedeği görünüyor); Kontrol döşemesi Odaklanma kapalıyken "Ayarlarda aç", açıkken "Odaklanma açık".
- [ ] Henüz sınanmadı: logo kırpma (Claude bildirimi yeni derlemede gelmedi); izin diyaloğu (izin zaten verilmişti); ayar kapanınca yoklamanın durması.
- [x] Bellek ve CPU ölçümü (2026-11-11, `docs/MEMORY.md`): Release, açılıştan 25 sn sonra 70,3 MB (60 sn sonra 66,0 MB); boşta CPU 60 sn'de tek çekirdeğin %0,96'sı (578 ms). Yoklama maliyeti ölçüm saçılmasının altında.
- Bilinen kısıt: Windows 11 Rahatsız etmeyin düğmesi tek başına algılanmıyor (belgelenmiş API yok); kartlar Odaklanma oturumunda ve sunum modunda susar. Kontrol sekmesindeki Rahatsız Etme döşemesi bu yüzden yalnız "Odaklanma açık" gösterir, aksi halde "Ayarlarda aç" kalır.

## Yüzen ada çentik (#63, P5A)
- [x] **Core (testli):** `Core/Notch/NotchStyle.cs` (`Yapışık` varsayılan / `Yüzen`; `Normalize`, `EarRadius` 12 / 0, `TopOffsetDip` 0 / 9, `CornerRadius` yüzen adada kapalı hâllerde yükseklik/2, açıkta 30, kartta 26; `EdgeTriggerBottomPixels`), `WindowPlacementMath.TopCenter(..., topOffsetDip)` aşırı yüklemesi (ofset hedef monitörün kendi DPI'ıyla piksele çevrilir; %100/150/200 testli), `AppSettings.NotchStyle` (Equals/hash dahil).
- [x] **App:** `NotchShape` kulak yarıçapı 0 iken üst köşeleri de dışbükey yuvarlar; `NotchWindow` `EarRadius` ve Top ofsetini ViewModel'den alır (tuval 640×364 değişmez, yalnız pencere Top'u kayar); kulaksız şekil Collapsed, Peek, Expanded, OSD HUD ve kartta aynı `NotchShape`'ten gelir; kenar açma bölgesi (`CheckEdgePeek`) ve `NotchBounds` 9 DIP boşluğu hesaba katar.
- [x] **Ayar:** Ayarlar › Çentik › "Çentik biçimi: Ekrana yapışık / Yüzen ada"; değişince çentik `ReloadNotch` ile yeniden kurulup yerleşir. Metinler `NotchStyle*` anahtarlarıyla iki dilde.
- [x] Elle sınama (proje sahibi, 2026-11-11, tek monitör, hepsi geçti): yüzen ada kapalı hâlde kulaksız kapsül ve üstten boşluklu; açık hâl kulaksız; ses göstergesi kulaksız; üst kenara gelince açılıyor; Ekrana yapışık'a dönünce hemen yerine oturuyor.
- [ ] Denenmedi: iki ekran ve karışık DPI, tam ekranda gizlenme, hareket azaltma, bildirim/güncelleme kartı yüzen biçimde.
- [x] Bellek ölçümü (AGENTS #1): 59,0 MB private, fark yok (`docs/MEMORY.md`).

## Halka pil ve gün/saat boşta görünümü (#63, P5B)
- [x] **Core (testli):** `IdleDisplay.RingClockDay = 3`, `IsRing`, `ShowRadioWhenCollapsed` (halka modunda Wi-Fi / Bluetooth yalnız Peek'te; radyo izleyicisi yine açılışta kurulur). `Core/Notch/IdleDisplayFormat.cs`: kısa gün + saat ("Sal 19:21" / "Tue 19:21"), Peek tarihi ("17 Kas" / "Nov 17"), dile göre yüzde ("%43" / "43%"), düşük pil kararı (≤%20 ve şarjda değil), halka yay noktası ve yol verisi (%0 boş, %100 iki yarım yay, kültürden bağımsız).
- [x] **App:** kapalı çentikte halka modu: solda 18 px halka (kalınlık 2,5, iz beyaz %28, dolu kısım `#4ADE80`, düşük pilde `#FB7185`), yanında yüzde, şarjda şimşek; sağda gün ve saat, Peek'te "· 17 Kas" ve radyo simgeleri. Pilsiz kasada yalnız gün/saat ortada; zamanlayıcı çalışırken süre halkanın yerine. Halka yalnız yüzde değişince yeniden kurulur (dondurulmuş `Geometry`), saat mevcut 15 sn zamanlayıcıyı kullanır.
- [x] **Ayar:** Ayarlar › Çentik › Boştayken › "Halka ve gün" (`IdleRingClockDay`, açıklaması ipucunda). UiSnapshots sahneleri: `halka`, `halka-peek`, `halka-zamanlayici`, `halka-dusuk`, `halka-sarj`, `halka-pilsiz`, `halka-en`.
- [x] Elle sınama (proje sahibi, 2026-11-11): kapalı çentikte halka, yüzde ve "Çar 18:xx"; Peek'te tarih ve Wi-Fi / Bluetooth simgeleri çıkıp kayboluyor. Şarja takınca pil göstergesi (HUD) çıktı.
- [ ] Henüz sınanmadı: HUD kaybolunca halkanın yanındaki şimşek, İngilizce, iki ekran.
- [ ] Bellek ölçümü (AGENTS #1): ölçülmedi (uygulama çalıştırılmadı). Beklenen fark yok: yeni servis ya da zamanlayıcı eklenmedi.

## Cam malzeme (P4B, onaylı tasarım `CamKoy.dc.html` / `CamCentik.dc.html`)
- [x] **Core (testli):** `MaterialSetting` üç seviye: Katı 1 / Buzlu 0,86 / Cam 0,72 (`Normalize`, `SurfaceOpacity`, `SurfaceAlpha`); eski "Yarı saydam" Buzlu'ya düşer (`Normalize` yükleme sırasında çevirir, ilk kayıtta yeni adla yazılır; şema sürümü değişmedi). `ReadabilityGuard`: duvar kağıdı ortalamasından ikincil metin kontrastı, Cam → Buzlu → Katı (demodaki hesapla aynı), BGRA ortalaması. `AppSettings.MaterialReadabilityGuard` ve `NotchFollowsMaterial` (varsayılan açık).
- [x] **Platform:** `Display/WallpaperSampler`: `HKCU\Control Panel\Desktop\WallPaper` (yoksa `TranscodedWallpaper`, düz renkte `Colors\Background`) WinRT çözücüyle 16 px genişlikte okunur. `App/Resources/MaterialState` yalnız saydam seviye + koruma açıkken ilk istekte bir kez ve `UserPreferenceChanged(Desktop)` ile okur; yoklama yok.
- [x] **App:** `ThemeManager` pencere, kenar menüsü, satır, segment (`KoyBgSegment`, yeni token) ve çentik kart (`KoyNotchCard`, `KoyNotchChip`) tonları; `NotchViewModel.SurfaceBrush` (açık çentik ve kart camsı, kapalı / Peek / HUD opak; `NotchShape` zemin değişince yeniden çizer); `DockViewModel.FullWidthSurface` ve yeni `CapsuleSurface`; Görünüm sayfasında üç seviye + iki anahtar. Metinler iki dilde.
- [x] UiSnapshots: `--material kati|buzlu|cam` ve `--wallpaper light|dark` (gerçek duvar kağıdı okunmaz). Cam koyu ve açık duvar kağıdında, açık ve koyu temada Koy penceresi, açık çentik ve dock kontrol edildi; açık duvar kağıdında koruma Cam'ı Buzlu'ya çıkarıyor.
- [x] Elle sınama (proje sahibi, 2026-11-11), ilk tur: dock geçti; Koy penceresinde bulanıksız Cam "gerçekten rezalet" bulundu (arkadaki pencereler çamur griye karışıyor, köşelerde açık renk sızıyor); açık çentikte %72 arkadaki metni okunur bırakıyordu ("fazla saydam").
- [x] **Düzeltme (proje sahibi kararı "Gerçek bulanıklık"):** Koy penceresi katmansız pencereye geçti (`WindowChrome`, `GlassFrameThickness=-1`, `CompositionTarget.BackgroundColor` saydam) ve DWM'den sistem akriliği, koyu / açık çerçeve ve köşe tercihi alıyor (`BackdropService.ApplyWindowFrame`); başlık ve kenar menüsü içerikle aynı ton. Çentik ve dock için ayrı opaklık: `MaterialSetting.LayeredOpacity` Buzlu 0,94 / Cam 0,88; koruma her yüzeyi kendi opaklığıyla hesaplar (`ReadabilityGuard.Surface.Layered`).
- [x] Elle sınama (proje sahibi, 2026-11-11), ikinci tur: Koy penceresi ("tamam kalsın böyle") ve açık çentik geçti.
- [ ] Henüz sınanmadı: açık tema, duvar kağıdını değiştirince korumanın güncellenmesi, "Açık çentik malzemeyi izlesin" kapalıyken, hareket azalt, iki ekran.
- [x] **Sağ alt menü ve kurulum sihirbazı da DWM akriliğe geçti** (proje sahibi kararı): ortak `App/Resources/WindowFrame` (katmansız pencere + `WindowChrome`; tema, malzeme, köşe ya da duvar kağıdı değişince `ThemeManager.Applied` ile yenilenir); menünün elle çizilen gölgesi ve 16 px kenar boşluğu kalktı, gölge ve köşe DWM'den. Elle sınama (2026-11-11): sağ alt menü geçti. Sihirbaz canlıda sınanmadı (yalnız ilk kurulumda açılır, o anda malzeme Katı).
- [x] Bellek ve CPU ölçümü (AGENTS #1, 2026-11-11, `docs/MEMORY.md`): Cam + halka + albüm rengi birlikte, Release, boşta 71,9–72,1 MB private, durağan boşta CPU tek çekirdeğin %1,25'i (bildirim yoklaması dahil). Katı ile ayrı karşılaştırma yapılmadı.
- Kapsam dışı (demoda vardı): "Pil tasarrufunda saydamlığı kapat"; renkli Ana kartları (müzik, ağ, sistem) opak kalır, müzik kartı P6-App'te.

## Müzik kartı: albüm rengi (#66)
- [x] **P6-Core (`Core/Media/CoverPaletteExtractor`, `Core/Appearance/ColorMath` HSL, testli):** küçük BGRA kapaktan tek geçişte 24 ton kovalı baskın ton; gri/monokrom kapakta (aday piksel < %10) null. Çıktı `CoverPalette(Card, SubText, Spectrum)`; Card beyaz başlıkla en az 7:1 kontrastlı.
- [x] **P6-App, ayar:** `MusicCardStyle` (`Neutral` Düz, `Tinted` Albüm rengi varsayılan, `Artwork` Albüm kapağı); Core'da `MusicCardStyleSetting.Normalize` + testler; Ayarlar › Çentik'te üç seçenek (TR/EN).
- [x] **P6-App, palet:** `MusicViewModel` parça değişince bir kez, arka planda 32 px ikinci çözüm + `CoverPaletteExtractor`; `CardBrush/SubTextBrush/SpectrumBrush` (Spectrum alfa 0,75) dondurulmuş; aynı kapak için `CoverFingerprint` ile yeniden hesaplanmaz; gri/kapaksız parçada Tokens fırçaları.
- [x] **P6-App, kapak arka planı:** `Artwork` modunda 32 px bitmap `UniformToFill` + `Linear`, üstünde Card tonu (0,30) ve `#0A0A0B` 0,55 → 0,85 perde; `BlurEffect` yok; küçük ve büyütülmüş kart aynı katmanı kullanır (`HomePanel.xaml`).
- [ ] **P6-App, elle sınama:** Spotify'da renkli, koyu ve siyah-beyaz kapaklar; YouTube'da kapaksız video; parça değişince geçiş; üç mod ve büyütülmüş kart; açık tema; Ayarlar'dan stil değişince çentik yeniden kurulur.
- [ ] **P6-App, bellek:** şarkı çalarken önce/sonra ölçüm (`measure-memory.ps1`), `docs/MEMORY.md`.
- [x] **P6-App, cam uyumu (demo `CamMuzikKarti.dc.html`):** Albüm rengi kart zemini çentiğin etkin malzemesinin alfasında (`MusicViewModel.SetCardAlpha`, `NotchViewModel` malzeme değişince verir; Katı 255 / Buzlu 219 / Cam 184); Kapak resmi opak kalır (okunurluk perdeden); palet yokken (Düz, gri ya da kapaksız) kart her malzemede nötr: demodaki "Düz nötr" (proje sahibi kararı 2026-11-11, eski yeşil `#0E2A18` bırakıldı); `KoyNotchMusicCard` Katı'da `#141416`, camda beyaz %6, ikincil metin beyaz %62, çubuklar beyaz %60, kapaksız yer tutucu (kart ve kapalı çentik) `#27272A`. UiSnapshots ile Katı ve Cam'da üç stil kontrol edildi.
- [x] **P6-App, elle sınama (proje sahibi, 2026-11-11):** parça değişince renk değişiyor, büyütülmüş kart aynı renkte; siyah-beyaz kapakta kart nötr griye dönüyor (Spotify). Kapaksız video ve Albüm kapağı stili ayrıca denenmedi.
