# Araştırma: Kısayolla açılan arama paneli

**Tarih:** 2026-11-08. **Durum:** araştırma; panelin yeri kararlaştırıldı, diğer kararlar açık, kod yazılmadı.

Soru: Kısayolla açılıp yazdıkça uygulama, pencere, hesap ve komut sonuçları gösteren bir panel Koy'a nasıl eklenir?

Her bilginin kaynağı yanında yazar:
**[Kod]** kaynak kodu okundu · **[Ölçüm]** bu makinede ölçüldü · **[Belge]** resmî belge · **[Doğrulanmadı]** yalnız ikincil kaynak ya da çıkarım.

## Kısa sonuç
- Fikir Koy'a uyuyor. Açık kaynak masaüstü arama araçlarının yaklaşımları incelendi; **kod alınmaz**, Koy kendi kodunu yazar.
- Windows tarafında engel görünmüyor, ama **en büyük risk klavye odağı** ve henüz sınanmadı (aşağıda "Klavye odağı").
- Uygulama listesi bu makinede ~0,9 sn sürdü: panel bunu **beklememeli**.
- Dosya arama, Windows Arama dizini açıksa mümkün; yeni NuGet paketi gerektirmeyen yol var.

## Bu tür paneller nasıl çalışıyor

**Arama altyapısı [Kod]**
- Her kaynağın iki işi vardır: sonuç üretmek ve seçileni çalıştırmak.
- Bir sonucun iki puanı vardır: kategori önemi (grupları sıralar) ve sonuç puanı (grup içini sıralar).
- Kaynaklar birbirini bekletmez; biri yavaşsa diğerlerinin sonuçları gelir.
- Liste en sık **250 ms**'de bir yenilenir. Yeni aramada boş liste hemen gösterilmez, titreme olmaz.
- Kaynak gereksiz çalışmasın diye en az harf sayısı, desen ve tetik sözcüğü tanımlanabilir.
- Hazırlık ve temizlik adımları: panel açılınca kaynak hazırlanır, kapanınca bırakılır.
- Kategori başına sınır vardır: her kategori kalan yerin en çok 1/(n+1)'ini alır, en az bir sonuç gösterilir. Tek kaynak bütün listeyi doldurmaz.
- Tek kaynak modu: yalnız bir kaynakta arama.

**Uygulama arama puanı [Kod]**
- Yazılan sözcüklerin hepsi eşleşmeli.
- Yazım hatasına izin verir (en çok 1 karakter). Tam eşleşme, tam uzunluk ve "şununla başlıyor" ayrı ek puan alır.
- Anahtar sözcüklerde kural daha sıkıdır. Aynı komuta giden iki sonuç tekilleştirilir.

**Pencere ve klavye [Kod]**
- Ekranın üstüne yapışık, tepeden kayarak iner. Genişlik sabit, yükseklik sonuçlara göre uzar, ekrana sınırlıdır.
- İsteğe bağlı "serbest" mod: ekranın 1/3'ünden aşağıda açılır.
- Odak gidince kapanır; iğnelenirse açık kalır. Etkin ekranda açılır.
- Önceki aramayı hatırlar (ayarlı). Boşken aşağı ok geçmişi açar.
- Geçmiş: 50 kayıt saklanır, 20'si gösterilir. **Başında boşluk olan arama geçmişe yazılmaz.**
- Sonuçların ek eylemleri olabilir (ör. hesapta "Kopyala").

**Tehlikeli kaynaklar [Kod]**
- Kabuk komutu kaynağı yazılanı çalıştırır, yetkiye bağlıdır. Süreç sonlandırma tetik sözcüğü ister.
- Koy'un ilk sürümünde ikisi de önerilmez.

## Alınacaklar, alınmayacaklar

| Yaklaşım | Koy için öneri | Sebep |
|---|---|---|
| Kaynak + puan modeli | Uygula | Core'da saf ve testli yazılır (AGENTS #11) |
| 250 ms toplu yenileme, eski aramayı iptal | Uygula | Yazılımla çizimde her yenileme pahalı (AGENTS #3, #13) |
| Hazırlık ve temizlik adımları | Uygula | AGENTS #5 ile aynı fikir |
| Kaynak başına iş parçacığı | Değiştir | Hesap ve pencere listesi anlık; yalnız yavaş kaynaklar için iptal edilebilir görev yeter |
| Ayrı süreçte çalışan eklentiler | Uygulama | AGENTS #4 tek süreç |
| Harici hesap kütüphanesi | Uygulama | AGENTS #8; küçük bir ayrıştırıcı yeter |
| Kabuk komutu, süreç öldürme | Uygula | Karar 7; süreç sonlandırma tetik sözcüğü ister |

## Windows tarafı

### Uygulama listesi
- `shell:AppsFolder` hem klasik hem Store uygulamalarını tek listede verir. Kimlik olarak AUMID verir, `shell:appsFolder\<AUMID>` ile başlatılır [Doğrulanmadı: ikincil kaynak, başlatma denenmedi].
- Koy bugün yalnız görev çubuğu `.lnk` sabitlerini okuyor. Bu liste yeni iş; ROADMAP'teki "UWP sabitleme" maddesiyle aynı temeli paylaşır.

**[Ölçüm]** Windows PowerShell 5.1 + COM, bu makine, tek deneme. C#'ta süre farklı olabilir.

| Ölçüm | Sonuç |
|---|---|
| Liste, ilk tur | 241 uygulama, 880 ms |
| Ad ve yol okuma | 57 ms |
| İkinci tur, toplam | 476 ms |
| Paketli / diğer | 42 / 199 |
| Başlat menüsü `.lnk` | 204 |

**Sonuç:** Liste arka planda hazırlanmalı, panel önceki kopyayı hemen göstermeli. Bellek maliyeti **ölçülmedi**. Simgeler yalnız görünen satırlar için çözülmeli (AGENTS #9).

### Dosya arama
- Windows Arama dizini `Provider=Search.CollatorDSO` ile sorgulanır, salt okunurdur; `ISearchQueryHelper` kullanıcı sorgusunu SQL'e çevirir [Belge].
- **[Ölçüm]** `WSearch` çalışıyor. COM üzerinden ADO ile, **NuGet'siz** sorgu çalıştı: ilk sorgu 931 ms, sonrakiler 133 ms ve 7 ms (ilk 20 sonuç, dosya adı öneki).
- C#'ta `System.Data.OleDb` yeni NuGet demek, proje sahibi onayı gerekir (AGENTS #8). COM ile geç bağlama bunu gerektirmez, ama denenmedi.
- Kullanıcının yazdığı metin SQL'e girer: tırnak kaçırılmazsa sorgu bozulur. Dizin kapalı makinede kaynak sessizce devre dışı kalmalı.

### Klavye odağı (en büyük risk)
- Çentik bilerek odak almıyor (`WS_EX_NOACTIVATE`). Yazı alanına tıklanınca geçici olarak etkinleşiyor, `NotchWindow.xaml.cs:153` [Kod]. Bu, **fare tıklamasıyla** çalışıyor; panel ise **kısayolla** açılacak.
- Windows, bir sürecin öne geçmesine yalnız belirli koşullarda izin verir: ön plandaki süreç olmak, son girdiyi almış olmak gibi. Koşullar sağlansa bile reddedebilir [Belge]. Belge, genel kısayolun "son girdi" sayılıp sayılmadığını söylemiyor.
- PowerToys Run'da da bazı ayarlarda odak alamama yaşandığı bildirilmiş [Doğrulanmadı: sürüm notu özeti].
- Koy'da benzer yol var: `Ctrl+Alt+K` sağ alt menüyü `Show` + `Activate` ile açıyor, menü odağı kaybedince kapanıyor [Kod]. Sonuç bilinmiyor.

**En ucuz ilk sınama, kod gerektirmez:** Başka bir uygulamada yazarken `Ctrl+Alt+K`'ye bas, sonra menünün dışına tıkla. Menü kapanıyorsa kısayolla odak alınabiliyor demektir; açık kalıyorsa ayrı çözüm gerekir.

Ek gereksinimler:
- Esc ile çıkınca odak, panel açılmadan önceki pencereye dönmeli.
- Yönetici olarak çalışan bir pencere öndeyken davranış sınanmalı [Doğrulanmadı].

### Kısayol
- `Alt+Space` Koy'un kurallarına göre sisteme ayrılmış, `Ctrl+Alt+Space` oynat/duraklat [Kod].
- `Win+Alt+Space` PowerToys Command Palette'in varsayılanı, çakışır [Belge].
- `Alt+F2` Koy'un ayrılmış listesinde yok; başka uygulamalarla çakışması sınanmadı.

### Türkçe
- `I/ı/İ/i` ayrı harfler; standart büyük-küçük çevirileri arama için yetmez. Core'da kendi harf eşleme tablosu ve testi gerekir.
- Türkçe Windows'ta ad "Hesap Makinesi" olur; `calc` yazan bulamaz. Exe adı ikinci arama anahtarı olabilir.
- Hesapta ondalık virgül desteklenmeli. Ayrıştırıcıda `eval` benzeri yol olmamalı, uzunluk ve derinlik sınırlı olmalı.

## Koy'da hazır olanlar [Kod]

| Gereken | Durum |
|---|---|
| Genel kısayol, çakışma denetimi | Hazır: `ShortcutAction`, `ShortcutConflicts` |
| Dosya/uygulama başlatma | Hazır: `IShellActionService.Launch` |
| Açık pencere listesi | Hazır: `IWindowTracker.GetOpenWindows` |
| Pano geçmişinde arama | Hazır: `ClipboardHistory.Query(filter, search)`. Yalnız ayar açıkken |
| Uygulama listesi, hesap, komutlar, puanlama | Yok |

## Panel nerede açılır
**Karar (2026-11-08, proje sahibi):** panel çentiğin içinde açılır. Karşılaştırma aşağıda kayıt için duruyor.

| | Çentiğin içinde | Ayrı pencere |
|---|---|---|
| Görünen sonuç | Sekmeler kalırsa ~4–5, sekmeler gizlenirse ~7 satır (40 birimlik satır varsayımı) | Sonuç sayısına göre uzar |
| Sınır | Tuval 640×364'ten büyütülmez (AGENTS #13) | Çentikle yan yana durması tasarlanmalı |
| Bellek | Yeni pencere yok; ölçülmedi | Yeni pencere, yalnız açılınca; ölçülmedi |
| Çoklu ekran | Hangi çentik: fare mi etkin pencere mi, seçilmeli | Aynı seçim gerekir |
| Kimlik | Koy'a özgü | Sıradan bir arama penceresine benzer |

Ölçüler: açık çentik 352 birim; gövde 236, durum satırı 26, sekme çubuğu 44 (`NotchLayout`).

## Kararlar (2026-11-08, proje sahibi)
1. Arama açıkken sekme çubuğu ve durum satırı gizlenir.
2. İlk sürümün kaynakları: uygulamalar, açık pencereler, hesap makinesi, pano, bilgisayar komutları.
3. Dosya arama ilk sürümde var.
4. Arama geçmişi tutulur; ayarlardan kapatılabilir, başında boşluk olan arama yazılmaz.
5. Tam ekran oyundayken kısayol hiçbir şey yapmaz.
6. Kapat ve yeniden başlat onay ister.
7. İşlev olarak incelenen araçlarla aynı davranır; fark yalnız görünümdedir. Açık kalan her kararda onların davranışı esas alınır.

## Sıradaki adımlar (karardan sonra)
1. `Ctrl+Alt+K` sınaması (yukarıda).
2. Küçük odak denemesi: kısayol, çentikte yazı alanı, Esc ile önceki pencereye dönüş.
3. Bellek ölçümü: liste açıkken ve kapanınca (AGENTS #1).

## Bu araştırmada yapılmayanlar
İncelenen araçlar çalıştırılmadı. Koy'da hiçbir şey denenmedi. Odak denemesi yapılmadı: masaüstüne tuş göndermek gerekirdi. Bellek ölçülmedi. Ölçümler tek makinede ve PowerShell üzerinden; çıktıya dosya ya da uygulama adı yazılmadı.

## Kaynaklar
- [SetForegroundWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)
- [PowerToys Command Palette](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview)
- [ISearchQueryHelper ile dizin sorgulama](https://learn.microsoft.com/en-gb/windows/win32/search/-search-3x-wds-qryidx-searchqueryhelper), [SQL ve AQS ile dizin sorgulama](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ff684395(v=vs.85))
- [Uygulama AUMID'si bulma](https://learn.microsoft.com/en-us/windows/configuration/store/find-aumid)
