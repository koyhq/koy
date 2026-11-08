# DESIGN — Koy arayüz tasarımı (M6)

Tasarım proje sahibi tarafından onaylandı (2026-11-04). Kodlarken bu belge esas alınır.

**Tasarımları görmek için:** [`docs/design/index.html`](design/index.html) dosyasını herhangi bir tarayıcıda aç. Tek dosyadır, internet gerekmez; bütün ekranlar sayfa sayfa görünür, düğmeler tıklanabilir. Ekranların kaynak kodu (renkler, ölçüler) `docs/design/source/*.dc.html` dosyalarında; kaynak değişince `python docs/design/build.py` ile `index.html` yeniden üretilir.

**Not (`support.js`):** `source/*.dc.html` dosyaları, tasarım tuvalinin çalışma zamanı olan `support.js` dosyasına başvurur. Bu dosya depoda yoktur; bu yüzden `source/` altındaki dosyalar tarayıcıda tek başına açılınca çalışmaz. Tasarımı görmek için `index.html` kullanılır: kaynakları kendi içine gömer ve `support.js` gerektirmez.

Galerideki beş sayfa:

| Sayfa | İçerik |
|---|---|
| Kararlar ve notlar | Kararlar, ilkeler, M6 yapılacakları, açık sorular |
| Koy uygulaması (ayarlar) | Sekiz ayar sayfası, ilk açılış, sağ alt menü |
| Dock görünümü | Yüzen dock, tam genişlik biçimi, etkileşimler |
| Pencereli görünüm | Aşağı yapış, çekil, yer ayır, iki ekran |
| Çentik görünümü | Durumlar, göstergeler, açık paneller, kısa bildirimler |

## İlkeler
1. Koy, Windows görev çubuğunun kopyası değildir. Saat, pil ve ağ çentikte durur, dock'ta değil.
2. Dock hiçbir pencerenin içeriğini örtmez. Pencere yaklaşınca yer ayırır, aşağı yapışır ya da çekilir; istenirse hiçbir şey yapmadan havada yüzer (Hep havada).
3. Boştayken işlemci kullanımı sıfırdır. Zamanlayıcılar ve animasyonlar yalnız görünen panelde çalışır.
4. Göz yoran animasyon kullanılmaz. Yay animasyonları kısa ve sakindir.
5. Her davranış ayardan değiştirilebilir. Varsayılanlar akıllı seçilir.
6. Her ekranın kendi çentiği ve dock'u vardır. Tam ekran yalnız kendi ekranındaki Koy'u gizler.

## Görsel dil
- **Zemin (koyu tema):** çentiğin dolgusu `#0A0A0B`. Pencere ve dock zemini `#18181B`; dock'ta bu renk %88–96 opaklıkla kullanılır. Kenarlık `rgba(255,255,255,0.10)`. Açık tema aynı rolleri açık renklerle karşılar (aşağıda "Tema ve token sistemi").
- **Vurgu:** `#3B82F6`. Ayarlardan başka renk seçilebilir.
- **Yazı:** metin `#F4F4F5`, ikincil metin `#A1A1AA`. Yazı tipi Segoe UI Variable.
- **Köşeler:** yüzen dock 18 px, Koy penceresi 12 px, çentiğin alt köşeleri 18 px.

## Tema ve token sistemi
Koy penceresi, sihirbaz ve sağ alt menü renk, ölçü ve hareketi koda gömmez; üç kaynaktan alır (`src/App/Resources`):
- **Renkler:** `Theme.Dark.xaml` ve `Theme.Light.xaml` aynı anahtarları (`KoyBgWindow`, `KoyBgRow`, `KoyTextPrimary`, `KoyDangerText`, `KoyScrim` vb.) farklı değerlerle tanımlar. Anahtarlar `DynamicResource` ile okunur, bu yüzden tema değişince açık pencereler yeniden açılmadan güncellenir. Sabit hex kullanılmaz; çentik ve dock'un kendi koyu yüzeyleri (`KoyMenu*`) tema dışında kalır.
- **Ölçüler:** `Tokens.xaml`: yazı boyları (`KoyFontPageTitle` 20, `KoyFontRowTitle` 13.5, `KoyFontBody` 13, `KoyFontCaption` 12), boşluk (`KoySpace1..6`: 4, 8, 12, 16, 24, 32), köşe (`KoyRadiusWindow` 12, `KoyRadiusRow` 8, `KoyRadiusControl` 6, `KoyRadiusSmall` 4), kontrol yüksekliği 28, ikon 16 ve çizgi kalınlığı 2.
- **İkonlar:** `Icons.xaml` içinde Lucide tabanlı çizgi geometrileri, `KoyIcon` denetimiyle çizilir. Emoji ve bitmap ikon yoktur.
- **Stiller:** `Styles.xaml` (başlıklar, satır, düğmeler: ikincil, `PrimaryButtonStyle`, `DangerButtonStyle`; anahtar, onay kutusu, segment, açılır liste, kaydırma çubuğu).
- **Tema seçimi:** Sistem / Koyu / Açık. "Sistem"de Windows uygulama teması (`AppsUseLightTheme`) izlenir. Vurgu rengi varsayılan `#3B82F6`; kullanıcı rengi ya da Windows vurgusu `ThemeManager` tarafından üstüne yazılır. Malzeme Katı ya da Yarı saydam (DWM acrylic istenir; saydam pencerede etkisi sistemde sınırlı kalabilir).
- **Doğrulama:** `tools/Koy.UiSnapshots` sayfaları iki temada PNG'ye çizer; arayüz değişikliğinde öncesi/sonrası buradan bakılır.

## Hareket ilkeleri
`Motion.cs` Koy penceresinin bütün geçişlerini tek yerden yönetir. Şablonlar süre yazmaz; tetikleyiciler `Motion.FadeTo`, `Motion.ScaleTo`, `Motion.KnobOn` ekli özelliklerini değiştirir.
- **Süreler (taban, Normal hızda):** hover katmanı 140 ms; basılınca küçülme (0,97) 110 ms; anahtar yayı 260 ms (`BackEase`, genlik 0,45, 18 DIP); sayfa girişi 170 ms solma + 200 ms ile 8 DIP yukarı kayma; sayfadaki satırlar 30 ms + 22 ms × sıra gecikmesiyle (en çok 12 satır) 200 ms solma ve 6 DIP kayma; kaldırma onay kartı sayfa girişiyle aynı.
- **Eğri:** `CubicEase` EaseOut; yalnız anahtar düğmesi yaylanır. Göz yoran, sürekli ya da dikkat dağıtan animasyon yoktur.
- **Tek çarpan:** hız ayarı (Sakin 1,5×, Normal 1×, Hızlı 0,65×, Kapalı 0) süreleri ölçekler. Çarpan 0 ise değerler anında atanır.
- **Azaltılmış hareket:** Windows "animasyonları azalt" (`SystemParameters.ClientAreaAnimation` kapalı) ve "uyum" ayarı açıksa, ya da pil tasarrufu hareketi azaltıyorsa çarpan 0 olur.
- **Boşta sıfır iş:** animasyon yalnız tetiklenince çalışır, biten animasyon saat bırakmaz (`FillBehavior.Stop`).
- Çentik ve dock yay fiziğini (`Core/Animation/Spring`) kullanır; bu belgedeki süreler yalnız Koy penceresi içindir.

## Dock
- **Yüzen (varsayılan):**
  - 60 px yüksekliğinde, ekranın altından 10 px yukarıda durur.
  - Düzen soldan sağa: Başlat, uygulamalar, ayırıcı, klasörler, son kullanılanlar yığını, (tepsi düğmesi), ayırıcı, Koy simgesi.
  - Çalışan uygulamanın altında nokta, aktif uygulamanın altında 16 px'lik çizgi görünür.
  - **Sistem tepsisi:** klasörlerin sağında, Koy simgesinin solunda yukarı ok düğmesi. Tıklanınca düğmenin üstünde koyu bir pencere açılır (başlık "Sistem tepsisi", 36 px kareler, en çok 6 simge genişliğinde sarar, boşsa "Tepsi simgesi yok"). Sol tık uygulamaya iletilir ve pencere çift tık süresi kadar açık kalır. Sağ tık uygulamanın menüsünü açar ve pencere AÇIK kalır (Windows'un taşma penceresi gibi); menüden seçim yapılınca ya da pencere ve menünün dışına tıklanınca kısa bir yay animasyonuyla kapanır. Odak kaybı kapatma sebebi değildir (menü öne gelince odak zaten başka sürece geçer); karar `TrayPopupDismissPolicy`'de, yoklama yalnız pencere açıkken (50 ms) yapılır. Düğme yalnız Windows görev çubuğu gizliyken ve ayar açıkken görünür (Dock sayfası "Sistem tepsisini dock'ta göster"). Renkler dock'un sabit koyu token'larıdır (`KoyMenu*`, `KoyNotch*`), ok `IconChevronUp`.
- **Tam genişlik:**
  - 44 px yüksekliğinde. Saat yoktur; sistem tepsisi düğmesi yüzen dock'taki gibi koşulludur.
  - Solda çalan müzik, ortada uygulamalar, sağda (varsa tepsi düğmesi ve) Koy simgesi durur.
  - Aktif uygulama, adıyla birlikte hap biçiminde gösterilir.
- **Pencere yaklaşınca** (Koy › Dock):
  - **Yer ayır (varsayılan):** dock alt kenara oturur ve koyu bir şerit olur. Çalışma alanından 46 px ayrılır. Büyütülmüş pencere tam dock'un üstünde biter, arada boşluk kalmaz.
  - **Aşağı yapış:** normal boy bir pencere dock'a yaklaşınca dock alt kenara iner, 60 px'ten 46 px'e incelir, yalnız üst köşeleri yuvarlak kalır. Pencere uzaklaşınca dock yeniden yüzer.
  - **Çekil** (Ayarlar'da "Pencereden çekil"): yalnız bir pencere dock'a yaklaşınca ya da büyütülünce dock gizlenir ve altta 120×4 px'lik bir tutamak kalır. Fare alt kenara inince dock 150 ms sonra geri gelir; pencere uzaklaşınca da kendiliğinden döner.
  - **Hep gizli:** pencereler ne yaparsa yapsın dock, fare alt kenara gelene kadar hep gizlidir.
  - **Hep havada** (yalnız yüzen kapsülde): pencere yaklaşsa ya da büyütülse bile dock asla alta yapışmaz, gizlenmez, yer ayırmaz; her zaman alttan 10 px boşlukla 60 px yükseklikte yüzer. Yalnız gerçek tam ekranda (ve "Tam ekranda gizle" açıkken) gizlenir. Tam genişlikte seçilemez; tam genişliğe geçilirse Yer ayır'a eşlenir.
  - **Gerçek tam ekran** (oyun, F11 tarayıcı, tam ekran video) her modda gizlidir ve kenardan da ASLA açılmaz. Tam ekran üç işaretin birleşimidir: Windows'un tam ekran bildirimi (`SHQueryUserNotificationState`: meşgul / özel kip D3D / sunum), başlık çubuğunun olmayışı, monitörü tamamen kaplama. Büyütülmüş (IsZoomed) pencere tam ekran sayılmaz; yalnız monitöre tam oturan başlıksız büyütülmüş pencere (F11) sayılır. Karar monitör başınadır.
  - **Alt kenardan açılma:** büyütülmüş pencere dock'u gizlediyse (Yer ayır modunda dock zaten görünür kalır) fare o monitörün alt kenarında ~300 ms kesintisiz durunca dock çıkar; fare tuşu basılıyken (pencere sürükleme, boyutlandırma) çıkmaz. Dock'a yürürken kapanmasın diye açıkken bölge yukarı genişler. Fare izleme yalnız dock gizliyken ve 100 ms aralıkla çalışır. Dock sayfasındaki "Alt kenardan aç" ile kapatılabilir (varsayılan açık); kapalıyken büyütülmüş pencerede dock gizli kalır.
- **Etkileşimler:**
  - Pencere önizlemesi 400 ms bekledikten sonra açılır. Aynı uygulamanın pencereleri gruplanmışsa önizlemeler yan yana gösterilir.
  - Sağ tık menüsü: son kullanılan dosyalar, Yeni pencere, Dock'tan kaldır, Bütün pencereleri kapat, Görev Yöneticisi'nde sonlandır.
  - Okunmamış sayısı kırmızı rozetle gösterilir. Dikkat isteyen pencerenin göstergesi turuncudur.
  - Klasöre tıklanınca son dosyalar ızgara halinde açılır.
  - Sabitli uygulamalar sürüklenerek sıralanır: öbür simgeler yayla kayarak yer açar, bırakınca sıra Koy'un ayarlarına yazılır (`DockPinnedOrder`). İlk açılışta Windows'un sırası (Taskband `Favorites`) tohum olarak okunur; okunamazsa klasör sırası kullanılır. Yeni sabitlenen sona eklenir, kaldırılan listeden düşer. Çalışan sabitlenmemiş uygulamalar sürüklenmez.
  - Son kullanılanlar: ayırıcının sağındaki yığın simgesine tıklanınca Windows'un Son Kullanılanlar klasöründen en çok 10 dosya listelenir (klasör yalnız tıklanınca okunur, izleme yok); dosyaya tıklanınca ilişkili uygulamayla açılır. Dock sayfasından kapatılır.
  - Sürükleme: sürüklenen simge için yer açılır. Bir dosya uygulamanın üstüne bırakılınca o uygulamayla açılır.
  - Büyütme efekti isteğe bağlıdır, varsayılan olarak kapalıdır.

## Çentik
- **Boşta:** 236×34 px. Solda saat, sağda pil görünür. Bir zamanlayıcı (Pomodoro, sayaç, kronometre) çalışırken pil ve Wi-Fi / Bluetooth simgelerinin yerine kalan süre ("18:24", Odak rengiyle) yazılır; saniyede bir güncellenir, animasyonu yoktur.
- **Müzik çalarken:** solda albüm kapağı, sağda hareketsiz bir dalga simgesi.
- **Göstergeler:** ses ya da parlaklık değişince çentik 260×40 px olur ve gösterge 1,5 sn görünür. Ses göstergesi beyaz, parlaklık göstergesi sarı (`#FACC15`).
- **Açık çentik (H tasarımı, onaylı kaynak: `docs/design/source/CentikH.dc.html`):** 620×352 px (tuval 640×364), her sekmede aynı boyut. Üstte durum satırı, ortada 236 px yüksekliğinde sabit sayfa, altta ikonlu yazılı sekme çubuğu.
  - **Durum satırı:** solda "14:36 · Perşembe, 6 Kasım"; sağda hava (sıcaklık ve küçük yer adı, tıklayınca yenilenir; konum seçilmemişse "Konum seç" yazısı Koy penceresinin Çentik sayfasını açar), pil yüzdesi (yalnız pil varsa), Bildirim Merkezi düğmesi (Win+N) ve "çentiği açık tut" düğmesi (açıkken fare çıkınca kapanmaz; çentik kapanınca kalkar). Hava yalnız buradadır, Ana'da hava kartı yoktur.
  - **Sekmeler** `NotchTabs` listesinden üretilir, 7 küçük düğme (simge 16 px + 10,5 pt yazı); seçili sekmenin simgesi sayfa rengini alır. Sıra: Ana `#F4F4F5`, Odak `#FB7185`, Takvim `#38BDF8`, Raf `#FBBF24`, Pano `#A78BFA`, Sistem `#34D399`, Kontrol `#3B82F6`. Takvim rengi tasarımda yoktu, diğerlerinden ayırt edilsin diye seçildi (`KoyNotchAccentCalendar`). Sekmelere tekerlekle de gidilir. Ayarlarda Ana her zaman açık, öbürleri tek tek kapatılır; sıra sabittir. Pano ayrıca "Pano geçmişi" ayarı kapalıyken görünmez.
  - **Ana:** solda müzik çalıyorsa müzik kartı (kapak, ad, sanatçı, ilerleme, oynat düğmeleri), çalmıyorsa günün dilimine göre selamlama + büyük saat + gün tarih; zamanlayıcı çalışıyorsa kartın altında "Odak · 18 dk kaldı" satırı (tıklayınca Odak açılır). Sağ sütun tasarımdaki gibi ama kulaklık kartı olmadan (Bluetooth pil verisi servisi yok, sonraki iş): üstte yan yana CPU / RAM kartı (yüzde, çubuk, en çok kullanan uygulama, kullanılan / toplam bellek) ve ağ kartı (ağ adı, ↓ / ↑ hız), altta hızlı başlat şeridi (uygulama simgesi, tıklayınca başlar, "+" ile dosya / uygulama seçilip eklenir, sağ tıkla kaldırılır; en çok 8). Kulaklık kartı gelince CPU / RAM ve ağ satırı bir satır küçülür, kart en üste girer.
  - **Odak:** solda Odak · Mola · Sayaç · Kronometre alt sekmeleri, ilerleme halkası, büyük süre, "1 / 4 tur" (Pomodoro turu), sıfırla, başlat / duraklat ve (Odak / Mola'da) sonraki aşamaya geç. Sağ kart sekmeye göre: Odak / Mola için görev listesi ("Bugün" kartı: tamamlanan üstü çizili, sıradaki vurgulu ve "şimdi", saatli görevler, satıra tıklayınca tamamla, × ile sil; altta başlık + isteğe bağlı saat (14:30) ve +; "Gün · Hafta · Ay" seçicisi bugünü, haftayı (Pzt-Paz) ya da ayı listeler, hafta ve ayda satırda gün de yazar; yeni görev hep bugüne eklenir), Sayaç için süre (‹ › ve hazır süre düğmeleri), Kronometre için tur düğmesi ve turlar. Aynı anda tek zamanlayıcı çalışır; birini başlatmak çalışanı duraklatır; bitince Peek ve titreme.
  - **Takvim:** solda ay ızgarası (olan günlerde nokta, bugün vurgulu, tıklayınca gün seçilir) ya da hafta satırları, üstte ‹ bugün › ve Ay · Hafta · Gün seçici; sağda seçili günün etkinlikleri, altta başlık + saat + ile yerel etkinlik ekleme (× ile silme). Gün görünümünde sol kart kalkar, gündem tam genişlik olur. Etkinlikler `events.json` dosyasında, bütün monitörlerde ortaktır; Google / Outlook yok.
  - **Raf:** "Raf · 4 öğe", Temizle, öğe kutucukları (uzantı rozeti, ad, kaldır; çift tıklayınca açılır, sürüklenince dışarı verilir) ve "Buraya bırak" kutusu. Raf, çentiğe dosya bırakılınca kendiliğinden açılır.
  - **Pano:** arama kutusu, Tümü · Sabit · Bağlantı · Renk · Kod süzgeçleri, Temizle (sabitler kalır) ve 3 sütunlu kart listesi ("Tür · zaman" ya da "· sabit", metnin ilk satırları, renkte örnek). Karta tıklayınca öğe panoya geri yazılır (kısa süre "Kopyalandı"), karttaki iğne sabitler / çözer, çöp kutusu siler. Altta "Yalnız metin, bağlantı, renk ve kod; son 50 öğe; sabitlenenler kalır." Servis yalnız Koy penceresi › Çentik › "Pano geçmişi" açıkken kurulur; kapalıyken pano dinlenmez ve sekme görünmez.
  - **Sistem:** iki halka (işlemci yüzdesi ve en çok kullanan uygulama; bellek yüzdesi ve "9,2 / 16 GB"), ağ kartı (ağ adı, ↓ / ↑ hız) ve "Koy'un kendisi" belleği (Görev Yöneticisi "Bellek" ile aynı ölçü). Veri servisi yalnız çentik açıkken ve Sistem ya da Ana görünürken çalışır; sekme değişince ya da çentik kapanınca bırakılır.
  - **Kontrol:** Wi-Fi, Bluetooth, Rahatsız Etme döşemeleri (açık döşeme mavi; döşemeler sürüklenerek yeniden sıralanır, sıra ayarlarda saklanır) ve ses / parlaklık kaydırıcıları. Wi-Fi ve Bluetooth döşemeleri bölünmüştür: sol bölüm aç/kapa, sağda dar bölümde ok (hover'da parlar, basınca vurgulanır). Ok, çentik içeriğini (sekme çubuğu kalır) liste sayfasıyla değiştirir: başlıkta geri oku, ad ve aç/kapa anahtarı; altta küçük "Diğer Wi-Fi / Bluetooth ayarları" bağlantısı (`ms-settings:`). Wi-Fi satırı: sinyal çubukları, ad, kilit; bağlı ağ üstte "Bağlı, güvenli" ve "Bağlantıyı kes"; kayıtsız güvenli ağda satır içi şifre kutusu; konum izni yoksa açık mesaj ve konum ayarı bağlantısı. Bluetooth satırı: tür simgesi, ad, "Bağlı" / "Bağlı değil", pil; ses cihazlarında Bağlan / Bağlantıyı kes düğmesi, diğerlerinde satır Windows ayarlarını açar. Sağdaki "Bağlı cihazlar" kartı (ad + pil) Bluetooth radyosu varsa görünür. Ana'nın sağ sütununda bağlı ses cihazı varsa üstte kulaklık kartı çıkar (yoksa yer kaplamaz).
  - Eski Müzik ve Durum sekmeleri kalktı (müzik ve durum Ana ile durum satırına girdi). Eski Takvim sekmesi H içinde yeniden geldi; eski paneldeki Pomodoro, zamanlayıcı ve kronometre araçları artık yalnız Odak sayfasındadır.
- **Kapalı ve Peek:** kapalı çentikte yalnız saat ve pil durur; üzerine gelince (Peek) tarih ve ağ simgesi (ve Bluetooth) de görünür, kapalı çentik kalabalıklaşmaz.
- **Kısa bildirimler (Peek):** "Pomodoro bitti", "Şarj tamamlandı", "Kulaklık bağlandı" gibi.

## Arama paneli
- `Alt+F2` ile açık çentiğin içinde açılır, tuval büyümez (AGENTS #13). Durum satırı, sayfa ve sekmeler o sırada gizlidir.
- Üstte arama çubuğu (yardım, yazı alanı, son aramalar, ayarlar, açık tut), altında kategori başlıklı liste. En çok 15 satır; sığmayanlara kaydırılır.
- Seçili satırda eylem düğmeleri görünür. Kapat, yeniden başlat ve oturumu kapat ikinci Enter ile çalışır.

## Koy penceresi
- 880 px genişliğinde, orta boy bir pencere.
- Pencereyi kapatmak Koy'u kapatmaz. Çıkış yalnız sağ alttaki Koy menüsünden ya da pencerenin sol altındaki "Koy'dan çık" düğmesinden yapılır.
- Sol menüde sekiz sayfa vardır:

| Sayfa | Ayarlar |
|---|---|
| Genel | Windows açılınca başlat; Windows görev çubuğunu gizle; arayüz dili; güncellemeleri otomatik denetle; kanal (Kararlı / Önizleme); Şimdi denetle |
| Çentik | Çentiği göster; ne zaman görünsün (Her zaman / Akıllı / Fareyle); tam ekranda gizle; boştayken ne gösterilsin (Saat ve pil / Yalnız saat / Hiçbiri); müzik çalarken kapak ve dalga; ses göstergesi; parlaklık göstergesi; Windows'un kendi göstergesini gizle (deneysel); paneller (Odak, Raf, Kontrol tek tek kapatılır; Ana her zaman açık, sıra sabit); açılma (Üzerine gelince / Tıklayınca); boyut |
| Dock | Dock'u göster; biçim (Yüzen / Tam genişlik); pencere yaklaşınca (Yer ayır / Aşağı yapış / Pencereden çekil / Hep gizli / Hep havada; tam genişlikte yalnız Her zaman göster / Otomatik gizle); tam ekranda gizle; alt kenardan aç; geri çıkma gecikmesi; simge boyutu; büyütme efekti; çalışan göstergesi (Nokta / Çizgi); pencereleri grupla; bildirim rozetleri; Başlat düğmesi; klasörler; son kullanılanlar yığını; sistem tepsisini dock'ta göster (varsayılan açık; yalnız Windows görev çubuğu gizliyken etkili); pencere önizlemesi ve gecikmesi |
| Ekranlar | Bağlı monitörler gerçek yerleşimiyle bir haritada kartlar olarak görünür; karta tıklayınca o ekranın ayarları açılır ("Tanımla" ekranın üstünde numarasını kısa süre gösterir). Ekran başına: çentik ve dock açık/kapalı, dock'ta yalnız o ekrandaki pencereler. Yeni bağlanan ekranda ne gösterilsin (Çentik ve dock / Yalnız çentik / Hiçbiri); varsayılan Çentik ve dock: ayarlar ekran başına baştan yapılmaz, genel ayarlar her ekrana uygulanır, kapatmak isteyen o ekranı seçip kapatır. Ayarlar kararlı bir monitör kimliğine bağlıdır; ekran çıkarılıp takılınca ayarı geri gelir. Gerçek çok monitörlü ve karışık DPI'lı donanımda henüz elle denenmedi. |
| Görünüm | Tema (Sistem / Koyu / Açık); vurgu rengi; Windows vurgu rengini kullan; malzeme (Katı / Yarı saydam); köşe yuvarlaklığı; animasyon (Sakin / Normal / Hızlı / Kapalı); Windows'un "animasyonları azalt" ayarına uy |
| Kısayollar | Çentik, Raf, Dock, Koy menüsü ve oynat/duraklat için genel kısayollar; satıra tıklayıp tuşlara basarak değiştirilir. Varsayılanlar `Ctrl+Alt+N`, `R`, `D`, `K`, `Space`. Windows'un ayırdığı (Alt+Tab, Win+harf vb.) ya da başka uygulamanın tuttuğu bir kombinasyon kaydedilmez ve sebebi satırda gösterilir. Çentikte tekerlek (Panel değiştir / Ses); dock'ta orta tık (Yeni pencere / Kapat) |
| Performans | Bellek göstergesi (100–150 MB hedefine karşı, 200 MB'a kadar kabul); pil tasarrufunda animasyonları azalt; pil tasarrufunda müzik dalgasını durdur; hava durumunu yenileme sıklığı; ses görselleştirici (deneysel) |
| Hakkında | Sürüm ve lisans (GPLv3); güncellemeleri denetle; tanılama bilgisini kopyala; **Windows görev çubuğunu geri getir** (acil durum düğmesi); ayarları sıfırla; kaynak kodu; **Koy'u kaldır** (yalnız kurulu sürümde etkin) |

- **Koy'u kaldır:** Hakkında'daki düğme pencerenin üstünde bir onay kartı açar: neyin silineceği, "Ayarlarımı sakla" kutusu (varsayılan işaretli; kapatılırsa ayar ve raf da silineceği kırmızıyla yazılır), görev çubuğunun hemen geri geleceği bilgisi, İptal / Kaldır. Kaldır; görev çubuğunu geri getirir, gerekirse verileri siler, Velopack kaldırıcısını başlatır ve Koy'u kapatır. Kurulu olmayan (geliştirme ya da taşınabilir) sürümde düğme kapalıdır ve "Yalnız kurulu sürümde kullanılabilir" yazar. Windows'un "Yüklü uygulamalar" sayfasından kaldırmada onay ekranı yoktur (Velopack bunu iptal edilemez ve süre sınırlı bir kancayla çağırır); orada ayarlar silinmez.
- **Sağ alt menü:** Koy simgesi dock açıkken dock'un sağ ucunda, dock kapalıyken Windows bildirim alanında durur. Menüde şunlar var:
  - Çentik ve Dock için aç/kapa düğmeleri
  - Koy penceresini aç
  - 1 saat duraklat
  - Koy'dan çık
- **İlk açılış:** üç adımlık bir kurulum. İkinci adımda kullanıcı seçer: Çentik ve dock / Yalnız çentik / Yalnız dock.

## Açık sorular
- Dock'taki ayırıcı ve klasörler kesin değil. Ayardan kapatılabilir olacak.
- Varsayılan "pencere yaklaşınca" davranışı Yer ayır (Windows görev çubuğu gibi dock hep görünür); Burhan'ın elle testinde istendi, ayardan değişir. Kayıtlı ayar göçle değiştirilmez.
- Kısayol varsayılanları (`Ctrl+Alt+harf`) Windows'un `Win+harf` kısayollarıyla çakışmaz; yine de başka bir uygulama aynı kombinasyonu tutuyorsa kullanıcı değiştirir.
