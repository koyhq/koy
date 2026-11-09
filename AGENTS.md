# AGENTS.md — Koy

Bu dosya bu depoda çalışan bütün ajanlar (Antigravity/Gemini, Claude, Codex) için tek talimat kaynağıdır.
İletişim Türkçe. Kararları proje sahibi verir.

## Proje
Windows 11 için hafif bir **notch** (ekranın üst ortasında, yay animasyonlu koy/ada) ve görev çubuğunun yerine geçen bir **dock**.
İlham: [kua8/Nectar](https://github.com/kua8/Nectar) (Tauri/WebView2, bellek ağır). **Nectar'dan kod, görsel ya da metin kopyalanmaz**; Nectar GPLv3 olsa da biz kendi kodumuzu yazarız.

- Hedefler, mimari, kararlar: `docs/PLAN.md`
- Görev odaklı harita ("şunu değiştirmek için nereye bakarım"): `docs/MAP.md`
- Sıradaki iş ve dosya dosya yapılacaklar: `docs/ROADMAP.md`
- Bellek ölçümleri: `docs/MEMORY.md`
- Arayüz tasarımı (dock, çentik, Koy penceresi, her sayfanın ayarları): `docs/DESIGN.md`. Arayüz işinden önce oku. Ekranların görseli: `docs/design/index.html` (tarayıcıda aç).

## Yığın
C# + WPF, .NET 10 (`global.json`), yalnız Windows 11. MVVM: CommunityToolkit.Mvvm. Win32: Microsoft.Windows.CsWin32. Test: xUnit. Lisans GPLv3.

## Komutlar
```powershell
dotnet build Koy.slnx                       # derle
dotnet test Koy.slnx                        # testler
dotnet run --project src/App                # çalıştır (çıkış: Koy penceresinde "Koy'dan çık" ya da sağ alt menü)
powershell -ExecutionPolicy Bypass -File scripts\measure-memory.ps1   # bellek ölç
dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix <ad> [--theme dark|light|both] [--scene all|uninstall|centik]
python scripts/generate-icons.py            # marka simgelerini (ICO/PNG) assets/branding'den üretir
```
`tools/Koy.UiSnapshots`: Koy penceresinin sayfalarını, sihirbazı ve sağ alt menüyü ekrana çıkarmadan PNG'ye çizer
(Win32 kancası, AppBar ya da görev çubuğu servisi başlatılmaz; ayarlar geçici dosyada). Arayüz değişikliğinin görsel
kanıtı için kullanılır. Uygulamayı çalıştırmadan bakmak gerektiğinde ve kaldırma gibi geri alınamaz akışlarda tek güvenli yol budur.
Sürüm ve değişiklik günlüğü release-please ile otomatiktir; elle etiket atılmaz, akış için `CONTRIBUTING.md` (Sürüm bölümü).
SDK bulunamazsa yeni terminal aç (PATH yenilenmemiş olabilir).

## Yapı
```
src/Core/       Saf C# (net10.0). WPF ve Windows API YOK.
                Servis sözleşmeleri (IMediaService…) ve veri modelleri, durum makineleri, orkestrasyon,
                yay fiziği, ayarlar. Klasörler özelliğe göre: Media/, Audio/, Notch/, Visibility/, Settings/…
                Appearance/ tema, vurgu rengi, malzeme, köşe ve hareket (MotionPolicy) kararları
                Shortcuts/  kısayol modeli (Hotkey), varsayılanlar, çakışma/ayrılmış kombinasyon denetimi, klavye kancası kararları (KeyboardHookEngine)
                Display/    monitör modeli, ekran başına ayar, yerleşim çözümleyici (DisplayLayoutResolver)
                Uninstall/  kaldırma kararları ve temizlik listesi (UninstallPlan, UninstallCleaner)
                Tray/       dock sistem tepsisi: SHELLTRAYDATA çözümleyici, simge deposu (üst sınırlı), tıklama geri
                            çağrısı, TrayHostPolicy (ne zaman barındırılır, Koy-süreç filtresi), ITrayService
                Clipboard/  pano geçmişi: sınıflandırıcı (ClipboardClassifier), bellek mantığı (ClipboardHistory), sabit öğe dosyası
                SystemStats/ CPU/RAM/ağ örnekleme hesapları, süreç seçici, Türkçe biçimlendirme (SystemFormat)
                QuickLaunch/ hızlı başlat listesi (en çok 8, kalıcı: AppSettings.QuickLaunchItems)
                Tasks/      odak görevleri (TaskService, tasks.json); Settings/JsonListFile ayar klasöründeki küçük JSON listeleri okur/yazar
                Search/     arama paneli: kaynaklar (ISearchSource), puanlama, sıralama, geçmiş, panel durumu (SearchPanelController), hesap
                StartMenu/  dock Başlat düğmesinin sağ tık (Win+X) menüsü: grup ve sıra modeli (StartMenuModel), eylemler (StartMenuActions), IStartMenuHost
src/Platform/   Core'daki sözleşmelerin Windows 11 uygulamaları (WinRT / CsWin32). Aynı klasör adları:
                Shell/TrayHostService.cs : Core.Tray.ITrayService. Koy'un gizli Shell_TrayWnd penceresi (kendi iş
                parçacığında); her kaydı Windows'a da iletir. SharedServices.AcquireTrayHost ile sayaçlı paylaşılır;
                yalnız görev çubuğu gizliyken kurulur (DockFeature.ApplyTrayPolicy). Görev çubuğunu gizleyen kod
                ShellTrayLocator.FindExplorerTray kullanır; FindWindow("Shell_TrayWnd") yalnız TrayHostService.EnsureFirst'te
                (z-sırası denetimi) bilinçli kalır.
                Wireless/WifiService.cs (WiFiAdapter + WlanProfiles) : Core.Wireless.IWifiService, Bluetooth/BluetoothService.cs
                (DeviceWatcher) + BluetoothAudioKs.cs (KS bağlan/kes) : Core.Bluetooth.IBluetoothService. SharedServices.AcquireWifiService /
                AcquireBluetoothService sayaçlı; Wi-Fi yalnız liste sayfası açıkken (10 sn tarama), Bluetooth yalnız Ana/Kontrol görünürken.
                Core/Wireless ve Core/Bluetooth: liste mantığı (WifiList, BluetoothList) saf ve testli.
                Clipboard/ClipboardHistoryService.cs : pano dinleyicisi (kendi iş parçacığında gizli mesaj penceresi), SystemStats/SystemStatsService.cs :
                Search/ : uygulama listesi (shell:AppsFolder, kısa ömürlü STA), Windows dizini (ADO COM, NuGet'siz), oturum komutları, yer imleri.
                1 sn zamanlayıcı (yalnız Start ile), Diagnostics/ProcessMemory.cs : özel çalışma kümesi. SharedServices.AcquireClipboardHistory/AcquireSystemStats sayaçlı.
                Media/MediaService.cs : Core.Media.IMediaService. Win32 çağrısı için adını NativeMethods.txt'ye
                ekle, CsWin32 üretir; elle DllImport yazma.
src/App/        WPF. Yalnız görseller ve ince ViewModel'ler, özellik klasörleriyle:
                Features/IFeature.cs           özellik giriş noktası (Start / Stop)
                Features/Notch/                NotchFeature, NotchWindow, NotchShape, NotchViewModel
                Features/Home/, Focus/, Calendar/, Shelf/, ClipboardPanel/, SystemInfo/, ControlCenter/   çentik sayfaları (Ana, Odak, Takvim, Raf, Pano,
                Features/Search/               çentikteki arama paneli; ilk açılışta kurulur (SearchFactory), kapalıyken kaynaklar çalışmaz
                                               Sistem, Kontrol); Music/ ve Status/ Ana'nın ve durum satırının veri kaynağı (kendi paneli yok).
                                               Servisler yalnız sayfası görünürken alınır: SystemStatsViewModel.SetActive (Ana + Sistem),
                                               Pano / Takvim / görevler / hızlı başlat kendi SetActive'leri; pano dinleyicisi NotchFeature'da yalnız ayar açıkken alınır
                Features/Dock/                 DockFeature, DockWindow… (kapalıyken hiç yüklenmez)
                Features/Settings/             KoyWindow (ayarlar), sayfaları, AppUninstaller (kaldırma)
                Features/Shortcuts/            ShortcutController: genel kısayolların kaydı ve dağıtımı
                Features/Menu/, Wizard/        sağ alt menü ve tepsi simgesi, ilk açılış sihirbazı
                Resources/                     Theme.Dark/Light.xaml (renkler), Tokens.xaml (ölçü, yazı), Icons.xaml,
                                               Styles.xaml, Motion.cs (hareket), ThemeManager, Strings.resx
src/Watchdog/   Gözcü süreç (Koy.Watchdog.exe, WinExe, Native AOT). Platform'daki TaskbarService.cs ve ShellTrayLocator.cs
                dosyalarını bağlayarak derler (görev çubuğu mantığı tek yerde); karar mantığı Core/Shell/WatchdogPolicy.cs.
                Koy tarafı: Platform/Shell/TaskbarService.Watchdog.cs (başlat/durdur). Exe'yi yalnız `dotnet publish`
                üretir (C++ derleme araçları gerekir); scripts/pack.ps1 ve release.yml Koy'un klasörüne kopyalar.
                Geliştirme çalıştırmasında exe yoksa gözcü sessizce atlanır.
tests/Core.Tests/  xUnit, yalnız Core. Ayrı App.Tests yok: mantık Core'da, ViewModel ince kalır.
tools/Koy.UiSnapshots/  Arayüzü PNG'ye çizen yardımcı (bkz. Komutlar). Çözüme dahildir, sürümle yayımlanmaz.
docs/           PLAN, MAP, DESIGN, ROADMAP, MEMORY, NAMING.
site/           Tanıtım sayfası (koyhq.github.io/koy): düz HTML/CSS/JS, derleme yok, `python -m http.server --directory site` ile denenir.
                `main`'e site/** değişince .github/workflows/pages.yml yayınlar. Ekran görüntüleri docs/screenshots'tan WebP'ye çevrilip site/img'e konur. Metinler iki dilde `site/js/i18n.js` içinde; varsayılan İngilizce.
scripts/        pack.ps1 (Velopack paketi), measure-memory.ps1, generate-icons.py, get-velopack-version.ps1.
```
Bağımlılık yönü: `App → Platform → Core` ve `App → Core`. Core hiçbir şeye bağımlı değildir. Platform WPF'e bağımlı olmaz.
Özellikler ayrı projelere (assembly) bölünmez; klasör yeterli.

## Değişmez kurallar
1. **Bellek bütçesi: boşta 100–150 MB private hedef; çok ekran ya da açık panellerle 200 MB'a kadar kabul.** Her aşama sonunda `scripts/measure-memory.ps1` çalıştır, sonucu `docs/MEMORY.md` tablosuna yaz (ölçüm koşulunu da yaz: monitör sayısı, açık özellikler, bekleme süresi). 200 MB'ı aşan ya da önceki ölçüme göre açıklanamayan sıçrama yapan değişiklik birleştirilmez; önce sebebi bul.
2. **Tarayıcı yok.** WebView2, CefSharp, Electron, Blazor Hybrid kullanılmaz.
3. **Yazılımla çizim varsayılan.** GPU ile çizim ~135 MB ekliyor (bkz. `docs/MEMORY.md`). `App.xaml.cs` içindeki `RenderMode.SoftwareOnly` satırını ölçmeden kaldırma.
4. **Tek süreç; tek istisna görev çubuğunu geri getiren gözcü.** Bütün pencereler (her monitör için notch + dock, ayarlar) aynı süreçte. Koy penceresi (`KoyWindow`) ilk açılışta oluşturulur ve tek örnek olarak kalır; kapatınca yok edilmez, gizlenir (`OnClosing` iptal edilir, ayarlar kaydedilir). Açılmamışsa hiç oluşturulmaz. Koy'u gerçekten kapatan yalnız "Koy'dan çık" düğmesi ve sağ alt menüdür; çentik ve dock bağlam menülerinde Çıkış yoktur.
   Gözcü (`src/Watchdog/`, `Koy.Watchdog.exe`, Native AOT, pencere yok) yalnız dock Windows görev çubuğunu gizlediği sürece
   çalışır, Koy'un PID'ini bekler; Koy öldürülürse görev çubuğunu geri getirir, temiz çıkışta (dock durdu, `Restore()`)
   hiçbir şey yapmaz. Aynı anda tek gözcü. Başka yardımcı süreç eklenmez.
5. **Servisler yalnız gerektiğinde çalışır.** Notch gizliyken görselleştirici, fare takibi, küçük resim üretimi durur. Zamanlayıcılar (`DispatcherTimer`) görünür değilken durdurulur.
6. **Animasyon yalnız hareket varken kare çizer.** `CompositionTarget.Rendering` animasyon başlarken bağlanır, `Spring` durunca çözülür (örnek: `NotchWindow.xaml.cs`). WPF `Storyboard` yerine `Core/Animation/Spring` kullanılır.
7. **Görev çubuğu güvenliği.** Uygulama nasıl kapanırsa kapansın (normal çıkış, istisna, süreç öldürülmesi) Windows görev çubuğu geri gelmeli. Açılışta `TaskbarService.Restore()` çağrısı kalır. Dock'la ilgili her değişiklikte elle test: Görev Yöneticisi'nden süreci öldür → görev çubuğu gözcü sayesinde birkaç saniye içinde kendiliğinden geri gelmeli (gözcü yoksa uygulamayı yeniden aç → geri gelmeli). Koy'un tepsi penceresi (`TrayHostService`) yalnız görev çubuğu gizliyken kurulur, her kaydı Windows'a da iletir ve dock kapanırken / duraklatırken / çökme işleyicilerinde görev çubuğundan önce kapatılır; görev çubuğunu gizleyen/geri getiren kod `ShellTrayLocator.FindExplorerTray` kullanır (Koy'un kendi `Shell_TrayWnd` penceresini atlar). `FindWindow("Shell_TrayWnd")` yalnız `TrayHostService.EnsureFirst` içinde bilinçli kalır: Koy'un penceresinin z-sırasında ilk olup olmadığına bakar.
8. **Ağır kütüphane yok.** Serilog, Autofac, ReactiveUI, MahApps, büyük UI kütleri eklenmez. Yeni NuGet paketi eklemeden önce proje sahibine sor ve bellek etkisini ölç.
9. **Görseller:** Albüm kapağı gibi resimler gösterilecek boyutta çözülür (`BitmapImage.DecodePixelWidth`), sonra `Freeze()` edilir.
10. **Ayarlar** `%AppData%\Koy\settings.json`. Yeni alan → `AppSettings` içine varsayılan değerle eklenir; kaynak üreticili `SettingsJsonContext` kullanılır, yansımalı `JsonSerializer` çağrısı yazılmaz.
11. **Sözleşmeler Core'da.** Her platform servisinin arayüzü ve veri modeli `src/Core/<Alan>/` içinde, uygulaması `src/Platform/<Alan>/` içinde. Core arayüzlerinde **WinRT / Win32 tipi görünmez** (`IRandomAccessStreamReference`, `HWND` yok): kapak `byte[]`, pencere kimliği `nint`. Durum ve karar mantığı (ne zaman gizlen, hangi panel, Pomodoro) Core'da yazılır ve test edilir; ViewModel yalnız bağlar.
12. **Özellik yükleme.** Her özelliğin tek giriş noktası `IFeature` (`Start` / `Stop`). `App.xaml.cs` ayarlara bakıp yalnız açık özellikleri başlatır. Kapalı özelliğin penceresi oluşturulmaz, servisi ve hook'u kurulmaz. **Dock kapalıyken (`DockEnabled = false`) Windows görev çubuğuna hiç dokunulmaz** (açılıştaki güvenlik `Restore()` çağrısı hariç).
13. **Sabit tuval.** Notch penceresi açık çentiğin boyutunda (H tasarımı: tuval 640×364 DIP, açık çentik 620×352; sabitler `Core/Notch/NotchLayout`) sabit durur; animasyon yalnız içindeki şeklin boyutunu değiştirir. `Window.Width/Height` animasyon sırasında değiştirilmez (saydam pencere titrer). Yazılımla çizimde maliyet pencere boyutuyla orantılıdır (her karede bütün pencere gönderilir), bu yüzden pencereyi gereğinden büyük yapma. H tuvali eski 500×260'ın yaklaşık 2,3 katı alan: yazılımla çizimde karede gönderilen piksel ve tuval belleği o oranda artar, bu yüzden tuval bundan büyütülmez. Panel açılışında fps ve CPU ölçülür; takılırsa çözüm, pencereyi yalnız durum değişiminde (açılmadan önce / kapandıktan sonra) bir kez boyutlandırmaktır. Zamanlayıcılar (Pomodoro, sayaç, kronometre) görünümden bağımsız tek örnekte (`Core/Timers/FocusTimers`, NotchFeature kurar) yaşar; çentik gizli ya da sekme değişik olsa da süre akar, yalnız ekran metninin tazelenmesi görünürlüğe bağlıdır.

## Kod biçimi
- Dosya kapsamlı namespace, `Nullable` açık, `var` serbest.
- Private alanlar `_camelCase`. ViewModel'lerde `[ObservableProperty]` ve `[RelayCommand]`.
- Yorumlar Türkçe ve kısa; *neden*i anlatır, *ne*yi değil.
- Kullanıcıya görünen metin XAML'e ya da koda sabit yazılmaz: `src/App/Resources/Strings.resx` (İngilizce) ve `Strings.tr.resx` (Türkçe), ikisine birden eklenir (M3'te kurulur; o zamana kadar Türkçe sabit metin kabul).
- Platform servisleri Core'daki arayüzlerin arkasında olur (kural #11) ki mantık sahte servisle test edilebilsin.

## Bitti tanımı (her görev için)
- `dotnet build` uyarısız ve hatasız, `dotnet test` yeşil.
- Davranış uygulama çalıştırılarak denendi (ekran görüntüsü ya da kısa açıklama).
- Aşama bittiyse bellek ölçüldü ve `docs/MEMORY.md` güncellendi.
- `docs/ROADMAP.md` içindeki ilgili kutu işaretlendi.

## Git
- Ana dal `main`. Her iş kendi dalında: `feat/m1-music-panel`, `fix/...`.
- Commit mesajı **İngilizce**, konvansiyonel önekli, kısa, emir kipinde: `feat(notch): add music panel`.
  Sebep: `CHANGELOG.md` (İngilizce) release-please ile commit'lerden üretilir; Türkçe notlar `CHANGELOG.tr.md`
  dosyasına sürüm PR'ında elle eklenir. Uygulamanın "Neler Geldi" ekranı seçili dile göre birini okur.
  Kod yorumları ve iç belgeler Türkçe kalır.
- Push ve PR yalnız proje sahibi isteyince.
