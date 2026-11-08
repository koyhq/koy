# Bellek ölçüm kaydı

Her aşamanın sonunda `scripts/measure-memory.ps1` çalıştır ve sonucu bu tabloya ekle.
Hedef (karar 2026-11-06): boşta **100–150 MB private**; çok ekran ya da açık panellerle **200 MB'a kadar kabul**.
Eski hedef olan tek monitörde < 60 MB, WPF'in ilk pencere maliyeti (~47 MB, aşağıdaki döküm) yüzünden ulaşılamadığı için bırakıldı;
aşağıdaki eski satırlardaki "Bütçe (< 60 MB) korundu" notları o günün hedefine göredir.

Makine: Windows 11 Home 26200, RTX 4050 Laptop + Intel UHD.

| Tarih | Aşama | Private | Working set | Not |
| --- | --- | --- | --- | --- |
| 2026-11-03 | M0, GPU ile çizim | 169 MB | 194 MB | NVIDIA sürücüsü ~135 MB ekliyor |
| 2026-11-03 | M0, yazılımla çizim | 34 MB | 93 MB | Varsayılan yapıldı (`App.xaml.cs`) |
| 2026-11-04 | M0, Features yapısı + 500×260 tuval | 35 MB | 94 MB | Büyük tuval belleği değiştirmedi |
| 2026-11-04 | M1, NotchShape + Müzik paneli | 47.7 MB | 127.0 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M1 Tam (Müzik + Görünürlük Modları) | 45.1 MB | 125.5 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M2, Kontrol Merkezi + Durum Panelleri | 40.9 MB | 258.9 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M3, Takvim, Pomodoro, Raf, Ayarlar | 39.2 MB | 256.6 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M4, Dock (Sabitli + Açık Uygulamalar, AppBar) | 45.8 MB | 261.8 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M5, Çoklu Monitör, Velopack Kurulum, Sürüm (v0.1.0) | 39.3 MB | 256.8 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | M4-M5 Düzeltme (ShellHook, Yay Büyütme, Monitör) | 38.8 MB | 256.4 MB | Bütçe (< 60 MB) korundu |
| 2026-11-04 | Yeniden ölçüm: oturum başı kodu (`b615431`), çentik açık | 97.2 MB | 567.5 MB | **Bütçe aşılmış.** Önceki düşük değerler büyük olasılıkla çentik hiç açılmazken alındı (eksik alanlı `settings.json` `NotchEnabled`'ı false yapıyordu) |
| 2026-11-04 | Kontrol Merkezi servisleri + hava durumu tembel (`b07c0db`) | 79.0 MB | 132.0 MB | 196 → 147 modül, pencere dışı kısım 20 → 13 MB. ~7 MB tasarruf sağlandı |
| 2026-11-04 | Tam genişlik dock + durum alanı, dock kapalı | 90.4 MB | 160.6 MB | Bütçe aşılmış; dock dışı bir kaynak, ayrıca incelenecek |
| 2026-11-04 | Tam genişlik dock + durum alanı, dock açık | 101.5 MB | 650.4 MB | Dock + durum alanı birkaç MB ekliyor; working set'teki artış kabuk simge fabrikasının yüklediği DLL'ler |
| 2026-11-04 | M6 Sürüm (Paket 0-4 Tam E2E, Boşta) | 82.7 MB | 135.9 MB | Bütçe kararı revizyon sonrasına bırakıldı (hedef < 60 MB geçerli; 85 MB eşiği resmi karar değil); Seelen UI model yüzen dock, çentik 236×34, lazy servisler |
| 2026-11-06 | Revizyon sonrası (main `ccb3a96`, Release) | 63,9 MB | 177,7 MB | Tek monitör; çentik + dock + tepsi açık; açılıştan sonra 15 sn boşta. Yeni bütçenin (100–150 MB hedef) altında. Koy penceresi bu ölçümde açılmadı. |
| 2026-11-07 | Kurulu v0.1.1 (Velopack, `%LOCALAPPDATA%\Koy\current`), çalışan oturum | 99–100 MB | 226 MB | İki monitör (RTX 4050 + Intel UHD), çentik + dock + tepsi açık. Görev Yöneticisi 'Bellek' (özel çalışma kümesi) 73,6 MB. Gözcü 2,7 MB private (8 MB ws). İlk örnekte (20–30 sn) iki iş parçacığı (UI + WPF çizim) ~%10'ar çekirdek harcadı; sonra 60 sn boşta yeniden ölçüldü: 5 sn'de 0–156 ms (çoğu ~20 ms) = bir çekirdeğin ~%0,3'ü, yani tüm işlemcide ~%0. İlk yüksek değer büyük olasılıkla ölçüm sırasındaki fare/ekran etkinliğiydi (neden kesinleştirilmedi; `dotnet-stack` anlık görüntüsünde UI iş parçacığı mesaj bekliyordu). Hedef (100–150 MB) içinde. |
| 2026-11-08 | Karşılaştırma: main `e40e67f`, Release, açılıştan 25 sn sonra, 3 tur | 78,6 / 81,7 / 109,9 MB | 188–233 MB | Tek monitör; çentik + tam genişlik dock + tepsi açık. Aşağıdaki satırla aynı koşul. |
| 2026-11-08 | Aynı main + arama paneli (#40), ikon hizası (#41), İngilizce (#46), Release, açılıştan 25 sn sonra, 3 tur | 76,8 / 80,4 / 122,6 MB | 186–250 MB | Orta değerde fark yok (81,7 → 80,4 MB). İki tarafta da birer yüksek değer var, sebebi araştırılmadı. |
| 2026-11-08 | Kurulu `0.3.0-test.3`, arama paneli: açılmadan önce → açıkken (15 sn) → kapandıktan 60 sn sonra | 85,8 → 90,2 → 88,9 MB | 219 → 226 → 224 MB | Tek monitör. Panel boş, sorgu yazılmadı; sonuç simgeleri dahil değil. Panel WM_HOTKEY iletisiyle açıldı. İki monitör ölçülmedi. |

> **Not (2026-11-05):** 82,7 MB'lık ölçümden sonra kısayollar, çoklu ekran, tema/ikon/hareket ve kaldırma ekranı eklendi; bu revizyon 2026-11-06 satırında ölçüldü (63,9 MB). Dock/tepsi paketi (gözcü süreç, son kullanılanlar, sabitli sıra, kenardan açma) sonrası yeniden ölçülmedi.
>
> **Gözcü süreç (`Koy.Watchdog.exe`):** yalnız dock görev çubuğunu gizlediği sürece ayrı bir süreç olarak çalışır; bu satırlar Koy sürecinin ölçümüdür, gözcü ayrı sayılır. Native AOT derlemesi bu geliştirme makinesinde C++ derleme araçları olmadığı için üretilemedi, bu yüzden AOT gözcünün private belleği ölçülmedi (IL ile derlenmiş, JIT'li sürüm 5 MB civarı; AOT'un belirgin biçimde altında olması beklenir). CI'da ve `scripts/pack.ps1` ile üretilince ölçülmeli.

## Öğrenilenler
- **GPU ile çizim pahalı.** WPF donanım hızlandırması açıkken ekran kartı sürücüsü sürece ~135 MB private bellek ekliyor. Notch ve dock küçük yüzeyler; yazılımla çizim (`RenderMode.SoftwareOnly`) yeterli. Animasyon akıcılığı M1'de ölçülecek; takılırsa önce çizilen alanı küçült, GPU'ya dönmeden önce ölç.
- Working set paylaşılan DLL'leri de sayar; karşılaştırmada **private** değeri esas alınır.

## 2026-11-04 bellek dökümü (çentik açık, dock kapalı)
Makine o gün **iki monitörlü** (1920×1080 harici + dizüstü ekranı), yani "tek monitör" koşulu sağlanmadı.
Yöntem: özellikleri ortam değişkeniyle tek tek kapatıp Release ölçümü, `dotnet-gcdump`, `dotnet-counters`,
`VirtualQueryEx` ile blok taraması, modül listesi farkı. Gerçek medya oturumu sonucu oynattığı için
karşılaştırmalar sahte medya verisiyle yapıldı.

| Bileşen | Yaklaşık private | Not |
| --- | --- | --- |
| .NET + WPF, pencere yok | 11–13 MB | Yönetilen yığın canlıda ~2 MB, GC commit 4–10 MB |
| İlk WPF penceresi (boş pencere bile) | **+47 MB** | `SoftwareOnly` olsa da WPF `d3d9.dll` yükleyip her ekran kartının yeteneklerini sorguluyor (`CDisplaySet::Init`, koşulsuz). NVIDIA (`nvgpucomp64`, `nvd3dumx`) ve Intel (`igc64`, `d3d9on12` → D3D12) sürücüleri yükleniyor. Boş pencere = 60 MB |
| Çentik penceresi bloğu | **+13–20 MB** | Veri bağlamı olmadan bile NotchWindow, boş 500×260 saydam pencereden 18 MB fazla. Sebep: `Topmost`, `AllowDrop`, boyut değil (denendi). İçerik piksel verisine benziyor (8,188 KB ≈ 1920×1080×4). Bazı çalıştırmalarda yok; açılışta ağ isteği (eski hava durumu) varken hep yok. **Kök sebep bulunmadı** |
| Kontrol Merkezi servisleri (WMI parlaklık, CoreAudio, radyo) + açılış hava durumu (HTTPS/TLS) | ~7 MB, 50 modül | **Düzeltildi:** panel ilk açılınca kuruluyor |
| Albüm kapağı | +8–10 MB | `WindowsCodecs.dll` + çizim. PNG/JPEG farkı yok, ölçekleme modu etkisiz |
| GC ayarları (`GCgen0size`, `GCgen0MaxBudget`, `GCHeapHardLimit`, `ConserveMemory`, `clrgc.dll`) | 0 | Etkisiz; şişkinlik yönetilen yığında değil |

### Bütçeye inilemiyor: sebep ve seçenekler
1. **WPF'nin D3D sorgusu (~47 MB) uygulama kodundan kapatılamıyor.** Süreç bazında ayar yok;
   `HKCU\Software\Microsoft\Avalon.Graphics\DisableHWAcceleration` bütün WPF uygulamalarını etkiler ve
   kaynağa göre sorguyu yine de yapıyor. Seçenekler: (a) bütçeyi "boş WPF penceresi + 15 MB" gibi göreli
   tanımlamak; (b) tek monitörde (dGPU'ya ekran bağlı değilken) ölçmek, NVIDIA sürücüsü yüklenmeyebilir;
   (c) uygulama klasörüne D3D'yi başarısız kılan bir `d3d9.dll` saplaması koymak (kırılgan, önerilmez);
   (d) çizimi WPF dışına almak (Win32 + Direct2D/GDI katmanlı pencere): büyük iş.
2. **Çentik penceresi bloğu (13–20 MB)** sıradaki iş: NotchWindow XAML'ini yarıya bölerek
   (CollapsedView / ExpandedView / ContextMenu / panel DataTemplate'leri) hangi öğenin bu yüzeyi açtığını bulmak.
