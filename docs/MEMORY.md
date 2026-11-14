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
| 2026-11-11 | Karşılaştırma: main `56c5b6d`, Release, açılıştan 25 sn sonra | 85,4 MB | 194,1 MB | Tek monitör; çentik + dock + tepsi açık. Aşağıdaki satırlarla aynı koşul, tek tur. |
| 2026-11-11 | Dock önizlemesine orta tık (#61), Release, aynı koşul | 67,7 MB | 167,5 MB | Tek tur. Değişiklik yalnız iki olay işleyicisi; main'den düşük çıkması ölçüm saçılması (2026-11-08 satırlarında aynı koşulda 76–122 MB). |
| 2026-11-11 | Çentik kartı altyapısı + güncelleme kartı (#65), Release, aynı koşul, kart görünmezken | 73,7 MB | 175,1 MB | Tek tur. main ile aynı aralıkta; kart görünmezken ek yük yok. |
| 2026-11-08 | Kurulu `0.3.0-test.3`, arama paneli: açılmadan önce → açıkken (15 sn) → kapandıktan 60 sn sonra | 85,8 → 90,2 → 88,9 MB | 219 → 226 → 224 MB | Tek monitör. Panel boş, sorgu yazılmadı; sonuç simgeleri dahil değil. Panel WM_HOTKEY iletisiyle açıldı. İki monitör ölçülmedi. |
| 2026-11-11 | Windows bildirimleri çentikte (#64), yoklama 2 sn, Release, açılıştan 25 sn sonra, kullanıcı dokunmadan | 70,3 MB (60 sn sonra 66,0 MB) | 181 MB | Tek monitör; çentik + dock + tepsi açık; bildirim ayarı açık. Boşta CPU 60 sn: 578 ms = tek çekirdeğin %0,96'sı (12 çekirdekte %0,08). Aynı yöntemle main 60 sn'de %4,32 ölçüldü (kullanıcı etkinliği ayrılamadı); yoklama maliyeti ölçüm saçılmasının altında. |
| 2026-11-11 | Cam malzeme + halka boşta görünümü + albüm rengi (P4B, P5B, P6; dal `feat/music-card-glass`), Release, malzeme **Cam**, boşta "Halka ve gün", müzik duraklatılmış, Koy penceresi kapalı, kullanıcı dokunmadan | 71,9 MB (30 ve 90 sn), 72,1 MB (189 sn) | 182,8 MB | Tek monitör; çentik + dock + tepsi açık; bildirimler açık (2 sn yoklama). Boşta CPU: 120–189 sn arasında tek çekirdeğin %1,25'i (750 ms / 60 sn); açılıştan sonraki ilk dakikada %4,1 (duvar kağıdı okuma, ilk bildirim çekimi, medya oturumu). Müzik çalarken ve Koy penceresi açıkken (aynı oturum, ölçüm değil gözlem) 95–122 MB, CPU %5–12. Katı ile karşılaştırma yapılmadı. |
| 2026-11-11 | Yüzen ada çentik biçimi (#63), Release, açılıştan 25 sn sonra | 59,0 MB | 149,9 MB | Tek monitör; çentik + dock + tepsi açık; çentik biçimi o an Yapışık (varsayılan). Tek tur; yüzey alanı değişmedi, düşük değer ölçüm saçılması. |
| 2026-11-09 | `test/0.3.0-test.6` (dört açık PR + şarkı sözleri), **Debug**, açılıştan 25 sn sonra, sözler kapalı | 69,4 MB (özel çalışma kümesi 44,9 MB) | 170,6 MB | Tek monitör; çentik + dock açık, ön planda tam ekran oyun. Debug olduğu için Release satırlarıyla birebir karşılaştırılamaz. Sözler açıkken ölçülmedi. |
| 2026-11-09 | Dal `feat/bluetooth-battery-voice-harness` (#48 sesli komut, #50 BT pil) ile main `f1eaa3b` karşılaştırması, Release, açılıştan 25 sn sonra, 7'şer tur | Dal: 91,8–98,5 (orta 93,9) MB; main: 85,4–95,0 (orta 86,0) MB | Dal 197–209 MB; main 192–203 MB | Tek monitör; çentik + dock + tepsi açık; görev çubuğu her turda gözcüyle (Debug `Koy.Watchdog.exe --pid`) geri geldi. main iki kümeli: ~85,5 MB (4 tur) ya da ~93,5 MB (3 tur); dal 7 turun hepsinde ~94 MB kümesinde. Yüksek kümede iki taraf aynı (fark ≤ 1 MB), yani ~8 MB fark yukarıdaki "Çentik penceresi bloğu" ile aynı boyda; yeni sesli komut/BT kodu boşta yüklenmiyor (ses denetleyicisi ilk kısayolda kurulur, GATT okuması sayfa kapalıyken kapalı). Dalın neden hep yüksek kümede kaldığı bulunmadı. Hedef (100–150 MB) altında. |
| 2026-11-09 | Aynı yöntem, Spotify ve Chrome açıkken (medya oturumu var): eski main `f1eaa3b` / main + #57 şarkı sözleri `ca08b64` / main + #57 + bu dal | 123,3–128,7 / 121,4–126,2 (orta 124,6) / 118,3–130,6 (orta 128,2) MB | 219–232 MB | Tek monitör; çentik + dock + tepsi açık, açılıştan 25 sn sonra, 3/5/5 tur, görev çubuğu her turda gözcüyle geri geldi. Bir önceki satırdan ~30 MB yüksek, ama eski main de aynı ölçüde yükseldi: fark ortamdan (çalan medya oturumu, albüm kapağı ve müzik kartı), PR'lardan değil. Üç sürüm aynı aralıkta; #57 ve bu dal boşta ölçülebilir bellek eklemiyor. Hedef (100–150 MB) içinde. |
| 2026-11-12 | Dal `feat/issue-51-extension-store` (#51 eklenti mağazası), Release, `scripts/measure-memory.ps1 -Seconds 15`, tek tur | 74,3 MB | 184,2 MB | Tek monitör; varsayılan özellikler, Koy penceresi kapalı. Mağaza kodu yalnız Eklentiler sayfası açılınca çalışır (indeks ve önizleme indirmesi o zaman); boşta yüklenmiyor. Hedef (100–150 MB) altında. Sayfa açıkken ölçülmedi. |
| 2026-11-14 | Çentik D (dal `feat/notch-d`), Release, açılıştan 12 sn sonra ve birkaç dakika sonra; çentik bir kez açılıp kapandı | 106,2 MB (sonra 103,8 MB) | 246,7 MB (sonra 256,3 MB) | Tek monitör; çentik (yüzen) + dock (kapsül) açık, kullanıcının gerçek ayarları. Bütçe içinde (100–150 MB) ama 11-11 satırlarından (59–74 MB) yüksek; aynı koşulda `main` ölçülmedi, bu yüzden farkın ne kadarının bu dala ait olduğu bilinmiyor. Sıradaki: `main` ile aynı koşulda karşılaştırma (#157). |

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
