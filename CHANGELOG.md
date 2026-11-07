# Değişiklik Günlüğü (Changelog)

Bu projedeki tüm önemli değişiklikler bu dosyada belgelenecektir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr/1.1.0/) standardına dayanır ve bu proje [Semantik Sürümleme (SemVer)](https://semver.org/lang/tr/) kurallarına uyar.

---

## [Unreleased]

Öne çıkanlar:
- **Dock klasörleri:** sabitli bir uygulamayı başka birinin üstüne sürükleyip bırakınca klasör olur; klasör dock'ta 2×2 önizlemeyle durur, tıklayınca cam panelde açılır.
- **Windows tarzı tam genişlik dock:** 48 px, ekrana boşluksuz yapışık, "Her zaman göster" ya da "Otomatik gizle".
- **Küçük tepsi, basma hissi, vurgu rengi:** tepsi Koy düğmesinin yanında dar bir ok; simgeye basınca hafifçe çöker; seçtiğin vurgu rengi dock'a ve çentiğe de uygulanır.
- **Çentikte bildirimler:** şarja takma/çıkarma, düşük pil ve dolu pil; Bluetooth kulaklık bağlanınca adı ve pili.
- **Daha kararlı dock:** büyütülmüş pencereler artık kendi kendine yeniden boyutlanmıyor, ayar değiştirince dock zıplamıyor, açık uygulamalar kaybolmuyor, Dosya Gezgini ikinci simge açmıyor.
- **Depo taşındı:** Koy artık [github.com/koyhq/koy](https://github.com/koyhq/koy) adresinde. Eski adres yönlendirir; otomatik güncelleme kendiliğinden yeni adrese geçer.

### Eklenenler (Added)
- **Dock klasörleri:** sabitli uygulamayı başka bir sabitli uygulamanın ortasına sürükleyip bekleyince ikisi klasör olur; kenara bırakmak sıralamayı değiştirir. Klasörü tıklayınca cam panel açılır; ad tıklanarak değiştirilir; panelden dışarı sürüklenen uygulama klasörden çıkar; tek uygulama kalınca klasör dağılır. Sağ tık: "Adını değiştir", "Klasörü dağıt".
- **Pil bildirimleri:** şarja takılınca, çıkarılınca, pil %20 ve %10'un altına inince ve dolunca çentikte kısa gösterge.
- **Bluetooth bildirimi:** eşleşmiş bir cihaz bağlanınca ya da bağlantısı kesilince çentikte cihaz adı ve varsa pil yüzdesi.
- **Hızlı başlata klasör eklenebiliyor** (`+` düğmesinin menüsünden).
- **Vurgu rengi dock ve çentikte:** Ayarlar › Görünüm'de seçilen vurgu, dock'taki etkin uygulama çizgisinde, Koy düğmesinde, çentikteki Kontrol Merkezi ve Bluetooth göstergesinde de kullanılır.
- **Dock: küçük tepsi.** Ayarlar › Dock › "Küçük tepsi" (varsayılan açık): tepsi düğmesi tam boy simge yerine dar bir ok olur ve Koy menüsü düğmesinin sağına geçer; tepsi paneli aynı yerden açılır.
- **Dock sağ bölümü iki modda aynı:** tam genişlik dock'ta da İndirilenler düğmesi var; Başlat, İndirilenler, Son kullanılanlar ve Tepsi anahtarları her iki modda çalışır. Kapalı düğmelerin ayırıcısı gizlenir.

- **Dock: Windows tarzı tam genişlik.** Çubuk Windows 11 görev çubuğu ölçüsünde 48 px, ekran kenarına sıfır boşlukla yapışık, köşesiz, üstte ince parıltı çizgisi. Yüzey Ayarlar › Görünüm › Malzeme'ye uyar (Katı: opak, Yarı saydam: %88). Çalışma göstergeleri (nokta/çizgi, çoklu pencere, dikkat, rozet) kapsülle aynı şablondan gelir; etkin uygulamanın adı artık çubukta yazmıyor (Windows gibi yalnız simge ve gösterge).
- **Tam genişlikte yakınlık modları sadeleşti:** "Her zaman göster" (çalışma alanından tam 48 px ayırır, büyütülmüş pencere çubuğun hemen üstünde biter) ve "Otomatik gizle" (yer ayırmaz, fare alt kenara gelince çıkar). "Aşağı yapış" ve "Çekil" yalnız kapsülde; tam genişliğe geçince Aşağı yapış → Her zaman göster, Çekil → Otomatik gizle olur.
- **Dock'ta basma hissi (iki modda):** uygulamaya ya da klasöre basınca simge %92'ye çöker ve hafifçe koyulaşır, bırakınca yayla geri gelir. Sürükleme başlarsa ya da fare öğeden ayrılırsa iptal olur. Hareket azaltmada yalnız renk değişir. Hover büyütmesinden bağımsız çarpandır.
- **Dock hareketi tek kaynaktan:** dock yayları (kayma, solma, simge büyümesi) Windows "animasyonları azalt", Animasyon hızı ayarı ve pil tasarrufuna uyar.

### Düzeltmeler (Fixed)
- **Büyütülmüş pencereler 5 sn'de bir yeniden boyutlanıyordu ("git gel"):** Koy'un tepsi penceresi simgeleri yeniden eklettirmek için "TaskbarCreated" yayınlıyor, bu yayın Koy'un kendi bildirim penceresine de ulaşıp "Gezgin yeniden başladı" sanılıyor ve dock görev çubuğunu yeniden gizleyip AppBar kaydını silip yeniden yapıyordu (çalışma alanı her seferinde bir an tam ekrana dönüyordu). Kendi yayınımızın yankısı artık yok sayılır. Ekran düzeni imzası çalışma alanını içermiyor (Koy'un kendi AppBar'ı düzeni "değişti" sandırıyordu). Görev çubuğunu gizleme zaten gizliyken tekrar ayar göndermez; aynı alan için AppBar konumu tekrarlanmaz.
- **Ayar değişimi dock'u yeniden başlatmıyor:** dock yerinde güncellenir; Windows görev çubuğu geri gelmez, AppBar kaydı ve çalışma alanı oynamaz (ayrıntı: `DockSettingsDiff`).
- **Kapsül "Aşağı yapış" büyütülmüş pencerede gizlenmiyor:** dock alta yapışık (46 px) görünür kalır, pencerenin alt şeridine biner, yer ayırmaz. Yalnız gerçek tam ekran gizler. Çekil ve Hep gizli eskisi gibi.
- **Dock'ta açık uygulamalar bazen görünmüyordu:** açık pencere süzgeci Windows görev çubuğunun kuralına çekildi (`WS_EX_APPWINDOW` pencereler sahipli olsa da gösterilir; `WS_EX_NOACTIVATE` ve araç pencereleri gizli kalır; başlığı boş pencere tek başına elenmez). Pencere gizlilikten çıkınca (`UNCLOAKED`), gizlenince ve başlığı değişince dock tazelenir (yoklama yok, olaylar birleştirilir).
- **Çok uygulama açıkken dock ekrandan taşmıyor:** kapsülde simgeler en küçük 24 px'e kadar küçülür; tam genişlikte orta bölüm yan bölümlere binmez.
- **Dosya Gezgini dock'ta ikinci simge açıyordu:** açık pencereler sabitli uygulamalarla Windows'un yaptığı gibi uygulama kimliğine (AUMID) göre eşleşir.
- **"Yer ayır"da büyütülmüş pencere ile dock arasında boşluk kalıyordu:** açılışta ayrılan alan görev çubuğunun payı kadar fazlaydı (94 px); artık birkaç saniye içinde tam dock yüksekliğine iner.
- **Ayar sayfalarında açıklama yazısı sağdaki düğmelerin altına giriyordu:** satırlar yazı ve denetim için ayrı sütunla dizildi.
- **Alt+Tab'da kapsül zıplıyordu:** çok kısa süren pencere geçişlerinde dock yüksekliği değişmez.
- **Boşta işlemci kullanımı:** pencere başlığı sık değişen uygulamalar (tarayıcı sekmesi, müzik) dock'u sürekli yeniden kurduruyordu.
- Sağ alt menüden ya da kısayoldan açılıp kapatılan çentik ve dock, Koy penceresi kapanınca eski haline dönüyordu.
- Kontrol Merkezi kapalıyken radyo (Wi-Fi/Bluetooth) servisi açılışta gereksiz yere kuruluyordu.
- Dock kapatılıp yeniden açılınca sistem tepsisi düğmesi (gizli simgeler) kayboluyordu. Kapanan dock tepsi penceresini yeniden kuruyor, yeni dock aynı pencereyi kuramıyordu.
- Bağlı olmayan Bluetooth cihazları için her seferinde hata kaydı yazılıyordu; günlük dosyası dolup gerçek hata kayıtları siliniyordu.

## [0.1.1](https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1) (2026-11-06)

0.1.0'dan sonraki ilk büyük sürüm. Öne çıkanlar:
- Yeni çentik tasarımı: 7 sekme (Ana, Odak, Takvim, Raf, Pano, Sistem, Kontrol).
- Windows'taki gibi Wi-Fi ve Bluetooth listeleri.
- Çalan müziğe göre hareket eden çubuklar ve büyütülebilen müzik kartı.
- Görev çubuğunu her koşulda geri getiren gözcü süreç ve tek örnek koruması.
- Çoklu ekran, kısayollar, Koyu/Açık tema, kaldırma ekranı.
- 0.1.0'da yalnız görüntü olan ayarların gerçekten çalışması.

> **v0.1.0'dan geçenler için:** v0.1.0'da otomatik güncelleme denetimi yoktu. Bu sürümü Ayarlar › Genel › "Şimdi denetle" ile ya da yeni kurulum dosyasıyla kurun; sonraki sürümler otomatik gelir. v0.1.0 ile başka bir Koy kopyasını aynı anda çalıştırmayın: eski sürüm ikinci ekranda çökebiliyor ve görev çubuğu geri gelmiyordu (bu sürümde düzeltildi).

### Düzeltmeler (Fixed)
- **Aynı anda iki Koy çalışmıyor:** ikinci açılış yeni bir kopya başlatmak yerine çalışan Koy'un penceresini öne getirir. İki kopya birlikte çalışınca eski sürüm ikinci ekranda çöküyor ve görev çubuğu geri gelmiyordu.
- **İki ekranda dock'lar birbirini etkilemiyor:** bir ekranda büyütülen pencere, kenardan taşan birkaç pikseliyle yan ekrandaki dock'u "yapışık" moda geçiriyordu. Ön plandaki pencere artık yalnız kendi ekranının dock'unu etkiliyor.
- **Başlat ve Arama açılınca dock görünüyor:** Windows tuşuyla açılan Başlat menüsü, Arama ve hızlı ayarlar dock'u gizlemiyor; dock o ekranda görev çubuğu gibi görünür kalıyor. Koy menüsünü açmak da dock'u gizlemiyor.
- **Parlaklık donması:** ikinci ekran kapatılınca parlaklık kaydırıcısı artık olmayan ekrana yazmaya çalışıyor ve çentik donuyordu. Parlaklık yolu ekran düzeni değişince yeniden seçiliyor (dizüstü ekranında Windows, harici ekranda DDC/CI), yazma arka planda yapılıyor; hiçbir yol yoksa kaydırıcı devre dışı ve nedeni yazıyor.
- **Sistem sayfası:** sekmeye geri dönünce değerler 1–2 saniye "–" görünüyordu; artık son bilinen değerler hemen görünüyor.
- **Hızlı başlat "+" düğmesi:** düğmenin ortasına tıklanınca çentik kapanıyordu; dosya seçici açıkken çentik açık kalıyor ve seçici çentiğin önünde açılıyor.
- **Tepsi simgeleri net:** simgeler kendi boyutunda (16 px) çiziliyor, saydamlık doğru okunuyor. Başlat düğmesinde gerçek Windows logosu.
- **Koy penceresinde sayfa geçişi** kesik kesik değil, tek bir yumuşak solma.
- **Bellek göstergesi** Görev Yöneticisi ile aynı ölçüyü (özel çalışma kümesi) gösteriyor.
- **Otomatik güncelleme durumu:** yeni sürüm bulunup indirildiğinde "güncel" yerine "yeni sürüm hazır; Koy kapanınca kurulacak" yazıyor.
- **Görev çubuğu Koy öldürülünce geri geliyor:** Görev Yöneticisi'nden sonlandırılan Koy'un gizlediği görev çubuğu otomatik gizlemede kalıyordu. Dock görev çubuğunu gizlediği sürece küçük bir gözcü süreç (`Koy.Watchdog.exe`, Native AOT, pencere yok) Koy'u izliyor; Koy ölürse görev çubuğunu önceki haline döndürüyor, düzgün kapanışta dokunmuyor.
- **Dock alt kenardan açılıyor:** büyütülmüş pencere dock'u gizlediğinde fare alt kenarda ~300 ms (tuş basılı değilken) durunca dock çıkıyor; Dock sayfasındaki "Alt kenardan aç" ile kapatılır. Oyunda, F11 tarayıcıda ve tam ekran videoda dock hiçbir koşulda çıkmıyor. Tam ekran artık Windows'un tam ekran bildirimi, başlık çubuğunun olmayışı ve monitörü kaplama işaretlerinin birleşimiyle ve monitör başına belirleniyor (yan ekrandaki oyun bu ekrandaki dock'u gizlemiyor).
- **Tepsi açılır penceresi sağ tıkta kapanmıyor:** uygulamanın bağlam menüsü açıkken pencere açık kalıyor; menüden seçim yapılınca ya da pencere ve menünün dışına tıklanınca kısa bir yay animasyonuyla kapanıyor.
- **Dock'taki tepsi düğmesi kaybolmuyor:** tepsi penceresi beklenmedik kapanırsa ya da kurulumu bir kez başarısız olursa düğme ekran değişimine kadar geri gelmiyordu; artık pencere kapanınca dock haberdar oluyor, düğme yeniden kuruluyor, başarısız kurulum zamanla yeniden deneniyor ve sebep `crash.log`'a yazılıyor.
- **Tepsi uygulamasının sağ tık menüsü dock'un altında kalmıyor:** dock ön plan değişiminde kendini yeniden en üste alıyor ve açılan menünün üstüne çıkıyordu. Menü açıkken dock kendini öne almıyor, tepsi penceresi açıkken görülen menüler en üste alınıyor ve menü çıpası dock'un üzerine düşerse dock'un üst kenarının üstüne çekiliyor.
- **Dock'ta tek Koy simgesi:** dock açıkken bildirim alanındaki Koy simgesi kaldırılıyor, dock kapanınca ya da duraklayınca geri geliyor.
- **0.1.0'da çalışmayan, artık çalışanlar:**
  - Kısayollar sayfası yalnızca görüntüydü; artık `RegisterHotKey` ile gerçekten kayıtlı ve değiştirilebilir.
  - Ses ve parlaklık göstergesi (OSD HUD) çentiğe hiç bağlı değildi; artık sistem ses ve parlaklık değişimlerinde açılıyor.
  - Çoklu ekran kullanıcı tarafından açılamıyordu (yalnız birincil ekran); artık Ekranlar sayfasından ekran başına seçiliyor.
  - "1 saat duraklat" yalnızca bir bayrağı değiştiriyordu; artık çentik ve dock'u durduruyor, görev çubuğunu geri getiriyor ve süre bitince devam ediyor.
  - "Pencere yaklaşınca" ayarı her açılışta "Çekil"e çevriliyordu; artık seçilen değer kalıyor.
  - "Güncellemeleri otomatik denetle", Pomodoro süreleri, hava durumu yenileme sıklığı, dock orta tık eylemi ve çalışan göstergesi (nokta/çizgi), pil tasarrufunda animasyon azaltma, çentikte tam ekranda gizleme, boşta gösterim, açılma davranışı ve tekerlek eylemi ayarları davranışa bağlandı.
  - "Windows görev çubuğunu gizle" anahtarı kapalıyken dock görev çubuğuna dokunmuyor.
- **Çökmeler:** sağ alt menü ilk tıklamada kapanıyordu (`FailFast`); dock ve çentik kenar zamanlayıcılarındaki başka iş parçacığı erişimi çökmesi giderildi; `crash.log` artık gerçekten yazılıyor ve gözlenmeyen görev hataları süreci düşürmüyor.
- **Görev çubuğu güvenliği:** dock görev çubuğunu iki kez gizleyince "otomatik gizle" kalıntısı kalıyordu; Explorer yeniden başlayınca tepsi simgesi kayboluyor ve görev çubuğu yeniden görünüyordu; "Yer ayır" modunda AppBar kaydı yenilenmiyordu; ilk açılış sihirbazı X ile kapatılınca dock açılıp görev çubuğu gizleniyordu; güncelleme ve kaldırma kancalarında görev çubuğu hata toleranslı biçimde geri getiriliyor.
- UWP pencerelerinde "Sonlandır" `ApplicationFrameHost`'u değil gerçek uygulamayı sonlandırıyor.
- Tam ekran algısı odak başka ekrandayken de her monitörün kendi en üst penceresine bakıyor.
- Ayarlar sıfırlanınca kurulum sihirbazı tekrar açılmıyor; çalışan özellikler yeni varsayılanla yeniden kuruluyor.
- Sabitli uygulama kısayolları çözülürken sızan COM nesnesi serbest bırakılıyor.
- Topluluk katkıları (teşekkürler **xdaxer**):
  - Kontrol Merkezi'ndeki Wi-Fi ve Bluetooth anahtarları artık radyoyu gerçekten açıp kapatıyor; radyo yoksa, kapalıysa ya da Windows izin vermiyorsa döşeme bunu gösteriyor ve Windows ayarlarına yönlendiriyor (#12).
  - Pili olmayan bilgisayarlarda çentikteki pil göstergesi gizleniyor (#8).
  - Dock pencere önizlemesi üst düzey pencereye kaydediliyor; önizleme güncellemesi başarısız olursa sonraki düzende yeniden deneniyor (#10).
  - Açılışta kapalı çentiğin zemini çiziliyor (#13).
  - Açık çentikte sekme çubuğu ve kapalı çentikte saatin yanında Wi-Fi ve Bluetooth durum simgeleri (#14). Sekmeler bu sürümde yeni çentik tasarımıyla yeniden düzenlendi.

### Eklenenler (Added)
- **Yeni çentik (tasarım H):** üstte saat, tarih, hava durumu, pil, bildirim ve iğne düğmesiyle durum satırı; altta ikonlu 7 sekme. Hangi sekmelerin görüneceği Ayarlar › Çentik'ten seçilir.
  - **Ana:** müzik kartı ya da selamlama ve büyük saat. Sağda bağlı kulaklığın pili, CPU/RAM, ağ hızı ve hızlı başlat şeridi (en çok 8 uygulama).
  - **Odak:** Pomodoro, serbest zamanlayıcı ve kronometre; günlük ve haftalık görev listesi. Çalışan zamanlayıcı kapalı çentikte de görünür.
  - **Takvim:** ay, hafta ve gün görünümü, yerel etkinlikler.
  - **Raf:** dosya ve metin sürükle-bırak.
  - **Pano:** kopyalanan metinlerin geçmişi (bağlantı, kod, metin süzgeci; sabitleme). Geçmiş yalnız bellekte tutulur, diske yalnız sabitlenen öğeler yazılır. Parola yöneticilerinin "geçmişe alma" işaretli içerikleri alınmaz. Ayarlardan kapatılabilir.
  - **Sistem:** CPU, bellek, en çok kaynak kullanan süreç, ağ hızı ve Koy'un kendi belleği.
  - **Kontrol:** Wi-Fi, Bluetooth, Rahatsız Etme, ses, parlaklık ve bağlı cihazlar.
  - Veriler yalnız ilgili sekme görünürken toplanır; çentik kapalıyken işlemci kullanımı sıfırdır.
- **Wi-Fi ve Bluetooth listeleri:** Windows hızlı ayarlarındaki gibi. Döşemenin solu aç/kapa, sağdaki ok listeyi çentiğin içinde açar.
  - **Wi-Fi:** sinyal gücü, kilit ve "Kayıtlı" işaretleriyle ağlar; bağlan ve bağlantıyı kes; şifre kutusu satırın içinde açılır. Ağ adları için Windows konum izni istiyorsa bunu söyler ve ayara götürür.
  - **Bluetooth:** eşleşmiş cihazlar türüne göre simge ve pil yüzdesiyle. Kulaklık ve hoparlörde bağlan/kes; diğer cihazlarda Windows'un Bluetooth ayarları açılır.
  - Listenin altında küçük bir "Diğer … ayarları" bağlantısı var. Bluetooth simgesi yeniden çizildi.
- **Müzik çubukları:** Ana'daki müzik kartında çalan sesten hesaplanan 24 çubuk. Yalnız kart görünür ve müzik çalarken çalışır; "kapak ve dalga" ayarına bağlıdır, pil tasarrufunda kapanır. Ses kaydedilmez, hiçbir yere gönderilmez.
- **Müzik kartını büyütme:** karta tıklayınca Ana'nın tamamını kaplar: büyük kapak, tam şarkı adı, geniş çubuklar. Tekrar tıklayınca eski boyutuna döner.
- **Cihaz konumunda şehir adı:** konum kullanılıyorsa çentikte "Konumum" yerine şehir adı yazar (OpenStreetMap). Koordinat yaklaşık 1 km'ye yuvarlanarak gönderilir; hava durumu isteği de artık yuvarlanmış koordinatla gider. Hakkında sayfasında OpenStreetMap atfı.
- **Sabitli uygulama sırası:** sıra Koy'un ayarlarında tutuluyor (`DockPinnedOrder`); ilk açılışta Windows'un sırası (Taskband `Favorites`) tohum olarak okunuyor, okunamazsa klasör sırası kullanılıyor. Simgeler dock'ta sürüklenerek sıralanıyor (yay animasyonuyla kayma), yeni sabitlenen sona ekleniyor, kaldırılan listeden düşüyor.
- **Dock'ta son kullanılanlar:** ayırıcının sağında yığın simgesi; Windows'un Son Kullanılanlar klasöründen en çok 10 dosya, yalnız tıklanınca okunur (izleme yok). Dock sayfasından kapatılır.
- **Genel kısayollar:** çentik, raf, dock, Koy menüsü ve oynat/duraklat için `Ctrl+Alt+N/R/D/K/Space`; tuş yakalama kutusuyla değiştirilir, Windows'un ayırdığı ya da başka uygulamanın tuttuğu kombinasyonlar satırda bildirilir.
- **Ekranlar sayfası:** monitör haritası, ekran başına çentik ve dock, yeni bağlanan ekran için seçenek, "Tanımla" düğmesiyle ekran numarası. Ayarlar kararlı monitör kimliğine bağlıdır; ekran çıkarılıp takılınca geri gelir.
- **Koyu / Açık tema** (ve "Sistem"), yarı saydam malzeme seçeneği, köşe yuvarlaklığı, animasyon hızı; Windows "animasyonları azalt" ve pil tasarrufuna uyum. Emojiler yerine tutarlı çizgi ikonlar, erişilebilirlik adları ve klavye odağı.
- **Koy'u kaldır:** Hakkında sayfasında, onay kartıyla ("Ayarlarımı sakla" varsayılan işaretli). Windows'un "Yüklü uygulamalar" sayfasından kaldırınca da görev çubuğu geri gelir, Windows ile başlatma kaydı silinir ve ayarlarınız korunur. Kurulu olmayan sürümde düğme kapalıdır.
- Hakkında sayfasında gerçek sürüm ve son güncelleme denetimi durumu.
- **Dock sistem tepsisi** (topluluk katkısı, teşekkürler **xdaxer**, #11): Windows görev çubuğu gizliyken Discord, OneDrive gibi arka plan uygulamalarının simgelerine dock'taki düğmeden açılan pencereyle ulaşılır; sol ve sağ tık uygulamaya iletilir. Koy yalnız görev çubuğu gerçekten gizliyken tepsiyi devralır ve her kaydı Windows'a da iletir; Koy kapanınca, duraklayınca ya da çökünce tepsinin eksiksiz geri gelmesi hedeflenir. Dock sayfasından "Sistem tepsisini dock'ta göster" ile kapatılabilir (varsayılan açık). Elle denenmedi.
- Geliştirici aracı: `tools/Koy.UiSnapshots` (arayüzü ekrana çıkarmadan PNG'ye çizer).
- **Takvim:** ay görünümüne ek olarak hafta ve gün görünümü (yerel etkinlikler, `%AppData%\Koy\events.json`; Google/Outlook yok). Gün görünümünde etkinlik eklenip silinir, ay ızgarasındaki ya da hafta satırındaki güne tıklayınca o günün görünümü açılır. Pomodoro'nun yanında serbest geri sayım zamanlayıcısı (süre seç, başlat, duraklat, sıfırla; bitince çentik kısa süre açılıp titrer) ve kronometre (başlat, duraklat, tur, sıfırla). Zamanlayıcı bitiş anını bir kez hesaplar, kronometre saatten okur; ikisi de arka planda saniyelik tik atmaz, ekran yalnız takvim sekmesi görünürken tazelenir.
- **Hava durumu ve konum:** kurulum sihirbazına "Hava durumu için konumunu kullanalım mı?" adımı (Evet / Şimdi değil). Evet denirse konum açılışta, günde en fazla bir kez ve tek seferlik düşük doğrulukla okunur; koordinat ayarlara yazılır, hava 30 dakika önbellekle Open-Meteo'dan gelir. Ayarlar › Çentik'te "Hava durumu için konumu kullan" anahtarı ve yedek olarak şehir arama (Open-Meteo geocoding). Konum yoksa ve şehir seçilmemişse çentikte hava durumu gösterilmez. İzin reddi, zaman aşımı ve ağ hatası uygulamayı etkilemez. Hakkında sayfasında Open-Meteo atfı (CC BY 4.0).
- **Kontrol merkezi:** Wi-Fi, Bluetooth ve Rahatsız Etme döşemeleri sürüklenerek yeniden sıralanır (yay animasyonlu); sıra ayara yazılır ve açılışta uygulanır.
- **Ağ durumu servisi** çentik durum verisine bağlandı (Wi-Fi adı, Ethernet, bağlı değil; olay tabanlı, yoklama yok). Ağ adı konum izni gerektirmeyen Windows bağlantı profilinden alınır.
- Çentikte yazı yazılacak alanlara (takvimde etkinlik adı) tıklanınca pencere geçici olarak etkinleşir ki klavye çalışsın; odak gidince yine odak çalmaz.

### Değişenler (Changed)
- **Dock varsayılanı "Yer ayır":** yeni kurulumlarda dock Windows görev çubuğu gibi hep görünür, pencereler onun üstünde biter. Mevcut ayar korunur. "Pencere dock'a yaklaşınca" seçicisinin altında seçili seçeneğin ne yaptığı yazıyor.
- **Müzik kapakları net:** kapak kendi çözünürlüğünde (en çok 320 px) çözülüyor ve köşeleri yuvarlak. Video kapakları (Chrome, Windows'a yalnız ~150×83 px veriyor) kırpılıp büyütülmüyor; resmin tamamı görünüyor, arkasını bulanık hâli dolduruyor.
- **Bellek bütçesi:** boşta 100–150 MB private hedef, çok ekran ya da açık panellerle 200 MB'a kadar kabul (eski hedef 60 MB idi). Performans sayfasındaki gösterge yeni hedefe göre. Bu sürümün geliştirme ölçümü: açılışta ~66 MB private.
- Yeni takılan ekranda çentik ve dock varsayılan açık; ayarlar ekran başına baştan yapılmıyor, genel ayarlar her ekrana uygulanıyor (Ekranlar sayfasından ekran seçilip kapatılır).
- Dock kenar izleme zamanlayıcısı 50 ms yerine 100 ms ve yalnız dock gizliyken çalışıyor.
- Dock ve çentik tek bir pencere izleyici ve tek bir medya servisini paylaşıyor; pencere olaylarında dock yenilemesi birleştiriliyor (daha az iş).
- Koy penceresi, sihirbaz ve sağ alt menü ortak tema ve ölçü sözlüklerinden besleniyor; sabit renk ve emoji kalmadı.
- Derleme tekrarlanabilir: yerel yollar PDB'ye gömülmüyor.
- Sürüm ve CI: release-please ile otomatik sürüm PR'ı, GitHub Actions adımları commit SHA'sına sabit, `vpk` sürümü Velopack paket sürümüne bağlı.

### Kaldırılanlar (Removed)
- Dock'taki küçültülmüş pencereler düğmesi ve ayarı (bir işe yaramıyordu).
- Çentik ve dock sağ tık menülerindeki "Çıkış". Çıkış yalnız Koy penceresindeki "Koy'dan çık" düğmesi ve sağ alt menüden yapılır.
- Hiçbir davranışa bağlı olmayan ayarlar: eski ses görselleştirici anahtarı (çubuklar bu sürümde "kapak ve dalga" ayarına bağlı olarak geldi), "müzik dalgasını durdur", "Windows'un kendi göstergesini gizle", çentik boyutu ve dock'ta pencere gruplama.
- Eski Ayarlar penceresi ve kullanılmayan kod, marka konseptleri ve sunum dosyaları.

### Güvenlik (Security)
- Depo kuralları: `main` dalına zorla push ve dal silme, yayınlanmış `v*` etiketlerinin taşınması ve silinmesi engellendi.
- README'de kod imzası olmadığı, SmartScreen uyarısında ne yapılacağı ve `SHA256SUMS.txt` ile otomatik güncellemenin neyi doğruladığı açıkça yazıldı.
- Yayımlanan ikili dosyalara geliştirici makinesinin yerel yolları artık gömülmüyor.
- GitHub Actions adımları commit SHA'sına sabitlendi (tedarik zinciri sertleştirmesi).

### Bilinen eksikler
- Kurulum dosyası ve güncellemeler henüz kod imzalı değil (SmartScreen uyarısı).
- Dock tepsisi (#11), Wi-Fi/Bluetooth bağlanma, kulaklık bağla/kes, parlaklık ve gözcü süreç her donanım çeşidinde denenmedi.
- Karışık DPI'lı çok monitör düzenlerinde doğrulama.
- Windows'un UWP sabitlemeleri (Spotify, WhatsApp gibi) dock'ta henüz gösterilmiyor.
- Hızlı başlata klasör eklenemiyor.
- Dock'ta uygulamaya özel sağ tık menüsü (atlama listesi) araştırıldı, yapılmadı.

Ayrıntı: [`docs/ROADMAP.md`](docs/ROADMAP.md).

## [0.1.0] - 2026-11-04

> **Not (sonradan eklendi):** Aşağıdaki listede bu sürümde bile eksik ya da bağlı olmayan şeyler vardı: ekran seçimi arayüzü olmadığı için çoklu ekran kullanıcıya açık değildi, OSD HUD çentiğe bağlı değildi, kısayollar ve ayarların çoğu davranışa bağlı değildi, "Aşağı yapış" seçilemiyordu, "1 saat duraklat" duraklatmıyordu, takvimde kronometre ve serbest zamanlayıcı hiç yoktu (sonradan eklendi). Düzeltmeler için [0.1.1](#011-2026-11-06) bölümüne bakın.

**Koy**'un ilk halka açık sürümü! Windows 11 için sıfır tarayıcı (WebView2/Electron içermeyen), tek süreçte çalışan, yay fiziği animasyonlu **Notch (Çentik)** ve Seelen UI modelinde **Dock**.

### Eklenenler (Added)
- **Yay Animasyonlu Çentik (Notch):**
  - Ekranın üst ortasında 236×34 px boşta durum (saat ve pil göstergesi; müzik çalarken albüm kapağı ve %0 CPU tüketen 4 çubuklu dalga ikonu).
  - İçbükey (*concave*) üst kulaklara sahip özel `NotchShape` vektör geometrisi ve `Spring` yay fiziğiyle 640 px genişliğe açılan sekme yapısı.
  - **Müzik Paneli:** `GlobalSystemMediaTransportControlsSessionManager` üzerinden Spotify, tarayıcılar ve medya oynatıcılar için kapak resmi, şarkı/sanatçı bilgisi, zaman çizelgesi (seek) ve çoklu oturum geçişi.
  - **Takvim & Pomodoro Paneli:** Pazartesi başlangıçlı ay görünümü, yerel zamanlayıcı, kronometre ve çalışma/kısa mola/uzun mola durum makineli Pomodoro sayacı.
  - **Raf (Shelf) Paneli:** Dosya veya metinleri çentiğe sürükleyip bırakma (dosyayı çentiğin üzerine sürükleyince Raf sekmesi otomatik açılır) ve dışarı sürükleme.
  - **Kontrol Merkezi & Durum Panelleri:** CoreAudio ses seviyesi, WMI ve DDC/CI donanım parlaklığı, Wi-Fi/Bluetooth radyo anahtarları, Rahatsız Etme durumu, pil yüzdesi ve Open-Meteo hava durumu (tembel/lazy başlatmalı).
  - **OSD HUD Göstergeleri:** Ses veya ekran parlaklığı değiştiğinde çentikte 1.5 saniye beliren 260×40 px yerleşik gösterge (ses için beyaz `#F4F4F5`, parlaklık için sarı `#FACC15`).
- **Seelen Modeli Dock:**
  - İki ayrı yerleşim biçimi: **Yüzen Kapsül** (60 px yükseklik, 18 px köşe yuvarlaklığı) ve **Tam Genişlik Cam (Full-Width Glass)** (44 px yükseklik, solda canlı medya hapı, ortada uygulamalar, sağda Koy menüsü).
  - **Pencere Yaklaşma Davranışları (`DockProximityMode`):** *Çekil / Akıllı Gizlenme (Dodge - Intellihide)*, *Yer Ayır (ReserveSpace - AppBar)* ve *Hep Gizli (AlwaysHidden)*.
  - **Canlı DWM Pencere Önizlemesi:** Dock simgelerinin üzerinde 400 ms durulduğunda `DwmRegisterThumbnail` ile gerçek zamanlı pencere önizlemesi.
  - **Z-Order Kararlılığı & Etkileşim:** Alt+Tab geçişlerinde sabitlenmemiş açık pencerelerin dock sırasını koruma, sol tıkla odaklama/küçültme, orta tıkla yeni pencere açma veya kapatma, özel koyu cam sağ tık menüsü.
  - **Görev Çubuğu Güvenliği:** Dock açıkken birincil (`Shell_TrayWnd`) ve ikincil (`Shell_SecondaryTrayWnd`) görev çubuklarını gizleme; kapanışta, çökmede, kaldırmada veya acil durum düğmesiyle eksiksiz geri yükleme (`TaskbarService.Restore()`).
- **Çoklu Ekran (Multi-Monitor) Mimarisi:**
  - `DisplayLayoutResolver` ve `MonitorService` ile her monitörde kendi DPI ölçeğinde bağımsız Çentik ve Dock pencereleri.
  - **Ekran Bazlı Tam Ekran İzolasyonu:** Yan ekranda tam ekran oyun veya video açıldığında yalnız o ekrandaki Koy gizlenir; ana ekrandaki Çentik ve Dock çalışmaya devam eder (`Win+Shift+S` ekran alıntısı aracı filtreli).
  - `WM_DISPLAYCHANGE` ile ekran takılıp çıkarıldığında pencereleri dinamik oluşturma ve yeniden konumlandırma.
- **Koy Penceresi, Sağ Alt Sistem Menüsü ve İlk Kurulum Sihirbazı:**
  - 880 px genişliğinde, 8 sayfalı (**Genel, Çentik, Dock, Ekranlar, Görünüm, Kısayollar, Performans, Hakkında**) modern ayarlar penceresi (`KoyWindow`).
  - Sağ altta 320 px hızlı popover menü (`SagAltMenuWindow`) ve CsWin32 `Shell_NotifyIcon` tabanlı sistem tepsisi simgesi (`TrayIconService`).
  - İlk açılışta 3 adımlı kurulum sihirbazı (`SetupWizardWindow`).
  - Tam Türkçe (`tr-TR`) ve İngilizce (`en-US`) anlık dil desteği (`Loc.Instance`).
  - **Marka Kimliği:** Konsept C "Koy Rozeti" (Windows 11 Fluent Squircle + Oyulmuş Çentik + K Monogramı) vektör logosu ve çok katmanlı `.ico` varlık kiti.
  - **Dağıtım & CI/CD:** Velopack kurulum paketi (`Koy-win-Setup.exe`), taşınabilir paket (`Koy-win-Portable.zip`), `SHA256SUMS.txt` doğrulaması ve GitHub Actions otomatik derleme/yayın iş akışları.

[0.1.1]: https://github.com/koyhq/koy/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/koyhq/koy/releases/tag/v0.1.0
