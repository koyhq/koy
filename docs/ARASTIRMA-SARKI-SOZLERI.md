# Araştırma: Eşzamanlı şarkı sözleri

**Tarih:** 2026-11-09. **Durum:** uygulandı (`feat/sarki-sozleri`).

Soru: Çalan parçanın sözleri, müzikle eşzamanlı olarak çentikte nasıl gösterilir?

## Kısa sonuç
- Koy'a uyuyor. Yeni NuGet paketi ve ek süreç gerekmiyor; parça ve konum bilgisi `IMediaService`'ten geliyor.
- Kaynak sırası: yerel `.lrc` dosyası, disk önbelleği, LRCLIB.
- Yalnız zaman damgalı sözler gösterilir. Düz metin söz, müzikle akmadığı için kapsam dışı.
- Kullanıcı "Sözler" düğmesine basmadıkça dosya ya da ağ işi yapılmaz.

## Kaynaklar
| Sıra | Kaynak | Konum | Not |
|---|---|---|---|
| 1 | Yerel dosyalar | `Müzik\Lyrics\` | Ağa çıkmaz. Önbelleğe alınmaz. |
| 2 | Disk önbelleği | `%LOCALAPPDATA%\Koy\lyrics\` | Parça başına bir dosya. |
| 3 | LRCLIB | `https://lrclib.net/api` | Herkese açık, belgelenmiş, anahtar istemez. |

Resmî belgesi olmayan müzik servisi uç noktaları bilerek kullanılmadı: haber verilmeden değişebilirler, tarayıcı gibi görünmeyi gerektirirler ve parça adını ek bir üçüncü tarafa gönderirler.

## LRCLIB
| Amaç | İstek |
|---|---|
| Tam eşleşme | `GET /get?track_name&artist_name[&duration]` |
| Arama | `GET /search?track_name&artist_name` |

Bu makinede 2026-11-09'da denendi:
- Var olan parça `200` döner; ilgili alanlar `trackName`, `artistName`, `duration`, `instrumental`, `plainLyrics`, `syncedLyrics`.
- Olmayan parça `404` ve `{"name":"TrackNotFound"}` döner. Hata değil, "yok" anlamındadır.
- Arama en çok 20 kayıt döner.
- İstekte uygulama adı ve bağlantısı olan `User-Agent` gönderilir (`AppHttp`).

Sıra: önce tam eşleşme; `404` ya da eşzamanlı sözü olmayan kayıt gelirse arama. Aramadan gelen kayıt, başlık tutuyorsa ve süre farkı en çok 3 saniyeyse kabul edilir, en yakın süre seçilir. Başka bir sürümün sözleri yanlış yerde akacağı için toleransı aşan kayıt alınmaz.

## Akış
```text
IMediaService (parça, zaman çizelgesi)
   ▼
LyricsViewModel: 350 ms bekle, sorgu kur (LyricsQuery)
   ▼
LyricsSession: eski isteği iptal et, arka planda yükle
   ▼
LyricsProvider: LocalLyricsSource → LyricsCache → LrclibSource
   ▼
LrcParser → satırlar → LyricsTimeline ile etkin satır → LyricsView
```

Katmanlar: söz mantığı `src/Core/Lyrics/`, ağ çağrısı `src/Platform/Lyrics/LrclibSource.cs`, görünüm `src/App/Features/Music/LyricsView*` ve `LyricsViewModel.cs`.

## Sorgu temizliği
Tarayıcıdaki videolarda başlık çoğu zaman `Sanatçı - Parça (Official Video)`, sanatçı alanı kanal adıdır. `LyricsQuery.Create`:
- Parantez içindeki gürültüyü siler (`official`, `lyrics`, `audio`, `video`, `hd`, `4k`, `remaster`, `feat`, `resmi`, `klip`, `sözleri`). `(Live at …)` gibi sürümü belirleyen ekler kalır.
- `- Topic` ve `VEVO` soneklerini siler.
- Sanatçı boşsa ya da başlığın sol tarafı sanatçı alanıyla kelime bazında örtüşüyorsa (`Queen` ve `Queen Official`) `A - B` biçimini ayırır.
- Sanatçısız parça için çevrim içi istek gönderilmez: yanlış şarkının sözünü getirme riski yüksektir.

## LRC biçimi
`LrcParser` şunları yapar:
- `[mm:ss.xx]`, `[mm:ss.xxx]`, `[mm:ss:xx]` ve `[mm:ss]` damgalarını okur.
- Bir satırdaki birden çok damgayı ayrı satırlara açar.
- Kelime bazlı `<mm:ss.xx>` etiketlerini metinden siler.
- `[offset:±ms]` başlığını uygular (pozitif değer sözü erkene alır).
- Damgasız satırları ve `[ar:]`, `[ti:]` gibi meta etiketlerini atlar.
- Art arda boş satırları teke indirir; boş satır arayüzde `♪` olur.
- En çok 5000 satır okur.

## Önbellek
- Dosya adı parçanın SHA-256 özetidir: parça adları diskte okunur halde durmaz, dosya adına uymayan karakter sorunu olmaz.
- Boş dosya "söz yok" kaydıdır, 7 gün geçerlidir. Sözler sonradan eklenebildiği için kalıcı değildir.
- Bulunan sözlerin süresi dolmaz; en çok 1000 dosya tutulur, aşılınca en eskiler silinir.
- Geçici hatalar (ağ, zaman aşımı, 429, 5xx) önbelleğe yazılmaz.

## Müzikle eşzamanlama
- Konum: son bildirilen konum + o andan beri geçen süre (`PlaybackClock`). Oynatıcılar konumu yalnız durum değişiminde bildirir.
- Etkin satır ikili aramayla bulunur; vurgu sesten 150 ms önce gelir.
- Saniyede bir yoklama yok: zamanlayıcı bir sonraki satırın zamanına kurulur, satırlar arasında CPU harcanmaz.
- Satıra tıklamak oynatıcıyı o satıra götürür.

## Dikkat edilen tuzaklar
| Tuzak | Çözüm |
|---|---|
| Parça hızlı değişince eski yanıt yeni sözlerin üzerine yazar | Her yüklemenin kendi `CancellationTokenSource`'u var; sonuç yalnız hâlâ güncelse uygulanır |
| İptal geri çağrısı eşzamanlı çalışıp iptal edilen isteği güncel sanar | `LyricsSession.CancelLoad`, iptalden önce alanı boşaltır |
| Aynı parça yeniden yüklenince "yükleniyor" ekranında kalmak | Aynı parça yalnız `Loading`, `Ready` ya da `NotFound` iken atlanır; `Failed` ve `Idle` yeniden dener |
| Parça bilgisi parça parça gelir | 350 ms bekleme; süre sorgu kimliğine (`Key`) dahil değil |
| Yeni parçaya önceki parçanın süresiyle sorgu gitmesi | Parça değişiminden 5 sn'den eski zaman çizelgesinin süresi kullanılmaz |
| Büyük klasör taramasının arayüzü dondurması | Bütün kaynak zinciri arka planda; tarama derinlik ve dosya sayısıyla sınırlı, bağlantı klasörleri atlanır |
| Görünmeyen özelliğin çalışması (AGENTS #5) | Yükleme ve zamanlayıcı yalnız sözler ekrandayken; sözler açıkken ses çubukları için yakalama durur |

## Gizlilik
Sözler açıldığında parça adı, sanatçı ve süre LRCLIB'e gönderilir. Yerelde ya da önbellekte bulunan parça için istek gitmez. Ayarlar › Çentik › Şarkı sözleri kapatılınca düğme kalkar ve hiçbir istek gönderilmez; açıklama metni bunu kullanıcıya söyler.

## Bu sürümde yok
- Birden çok aday arasından elle söz seçme.
- Parça başına zaman kaydırma ayarı.
- Düz metin sözler ve kelime kelime vurgu.
- Künye satırlarını (söz yazarı, besteci) eleme.
- Yerel klasör seçme arayüzü (`Müzik\Lyrics` sabit).
- 429 yanıtındaki `Retry-After` başlığına göre bekleme.

## Doğrulama
| Ne | Nasıl | Sonuç |
|---|---|---|
| Mantık (ayrıştırma, sorgu, seçim, önbellek, oturum, saat) | `tests/Core.Tests`, xUnit | Yeşil |
| LRCLIB yanıt biçimi | Gerçek istek | `200`, `404`, 20 sonuç |
| Kaynak zinciri | `LyricsProvider` + `LrclibSource` gerçek hizmetle, geçici klasörde | Bulma, gürültülü başlık, önbellek, olmayan parça çalıştı |
| Görünüm | `tools/Koy.UiSnapshots`, `ana-muzik-sozler` sahnesi | Koyu ve açık tema |
| Gerçek oynatıcıyla senkron | **Yapılmadı** | Elle sınama bekliyor |
| Bellek ölçümü | **Yapılmadı** | Elle sınamayla birlikte |
