# Koy eklenti mağazası

Koy'un "Eklentiler" sayfası bu klasördeki `index.json` dosyasını (GitHub raw) okur. Sunucu yoktur: eklentiler
pull request ile eklenir, bakımcı inceler. Tasarım ve dosya biçimleri:
[`docs/EXTENSION-STORE.md`](../docs/EXTENSION-STORE.md).

> **English summary.** `store/index.json` is the catalog the in-app store reads. Packages are data-only `.koyext` zips
> (theme, icon pack, live background, settings profile): no code, no XAML. To submit one: fork, add
> `store/src/<id>/`, run `python scripts/build-store.py`, commit the generated files, open a PR. A maintainer reviews it
> against the checklist below. `summary` and `description` in `store.json` are either a plain string or an object of
> language to text (`{"en": "...", "tr": "..."}`, `en` required); the app shows the UI language and falls back to English.
> The extension name stays untranslated. `python scripts/build-store.py --check` (also run by CI) verifies everything.

## Klasörler

```
store/
  index.json          Üretilir. Elle düzenlenmez.
  packages/           Üretilir. <id>-<sürüm>.koyext (zip)
  previews/           Üretilir. <id>.png
  src/<id>/           Paket kaynakları: bunu siz yazarsınız
```

## Eklenti nasıl gönderilir

1. Depoyu fork'layın, yeni dal açın.
2. `store/src/<id>/` klasörünü oluşturun. `<id>` küçük harf, rakam, tire ve ad alanı ayırıcı nokta içerir (ör. `kullanici.koyu-mavi`); manifest `id`'siyle aynı olmalı.
3. Aşağıdaki dosyaları ekleyin:

   | Dosya | Açıklama |
   |---|---|
   | `manifest.json` | `{"id","name","version":"1.0.0","author","kind","license","capabilities":[],"entryKind":"data"}` |
   | içerik | `kind`'a göre: `theme.json` (theme), `icons/<Uygulama>.png\|ico\|svg` (iconpack), `background.gif\|webp\|mp4\|webm` (background), `profile.json` (profile) |
   | `preview.png` | Mağaza kartı görseli, 320×180 önerilir, en çok 1 MB |
   | `store.json` | Yalnız indekse girer, pakete girmez: `summary` (≤200), `description` (≤2000, düz metin; ikisi de düz metin ya da `{"en": "...", "tr": "..."}` nesnesi, nesnede `en` zorunlu), `homepage` (https), `updated` (`YYYY-MM-DD`), `downloads` (0) |

   Örnekler için `store/src/` altındaki mevcut paketlere bakın.
4. `python scripts/build-store.py` çalıştırın (yalnız Python 3 standart kütüphanesi gerekir). Paketi, önizlemeyi ve `index.json`'u üretir.
5. Kaynakları ve üretilen dosyaları (`store/packages`, `store/previews`, `store/index.json`) birlikte commit'leyin, PR açın.
   PR'daki `Store check` işi `python scripts/build-store.py --check` çalıştırır; üretilmiş dosyalar kaynakla uyuşmazsa ya da bir kural çiğnenmişse kırmızı olur.

Var olan bir eklentiyi güncellemek için `manifest.json` içindeki `version`'u artırın (eski paket dosyası otomatik silinir) ve `store.json` içindeki `updated`'i değiştirin.
Sürüm `major.minor.patch` olmalı; kullanıcılar sürüm farkıyla "Güncelle" düğmesini görür.

## Kurallar

- **Yalnız veri paketi.** Tür dört tanedir: `theme`, `iconpack`, `background`, `profile`. Kod, betik, DLL, XAML, çalıştırılabilir dosya yoktur. `capabilities` boş olmalıdır; yetki isteyen paket reddedilir. `entryKind` her zaman `"data"`.
- **Lisans zorunlu.** `manifest.json` içinde SPDX kimliği (`GPL-3.0-or-later`, `CC-BY-SA-4.0`, `MIT`, `CC0-1.0`…). Kendi yapmadığınız içeriği yalnız lisansı izin veriyorsa ve atıfla ekleyin (`description` içinde).
- **Marka logosu ve telif ihlali yok.** Başka uygulamaların resmî simgeleri/logoları, lisanssız görseller, başkasının eserinin kopyası kabul edilmez. Simge paketlerinde kendi çizdiğiniz şekilleri kullanın.
- **Temalar** `user-theme.json` biçimindedir: yalnız izinli `Koy…` kaynak anahtarları, renkler `#RRGGBB` / `#AARRGGBB`; dosya ≤ 64 KB. Pencere/tuval boyutları değiştirilemez.
- **Profiller** `koy-profile` biçimindedir; makineye özgü alanlar (simge paketi klasörü, canlı arka plan yolu, dock başlat simgesi yolu) yazılmaz. Kısayol çakışması ve geçersiz değer reddedilir.
- **Boyut sınırları:** paket (indirme) ≤ 25 MB, açılmış toplam ≤ 50 MB, en çok 512 dosya, canlı arka plan dosyası ≤ 64 MB (pratikte ≤ 500 KB hedefleyin: çentik 620×352; GIF en çok 120 kare tutulur), `index.json` ≤ 1 MB ve en çok 500 öğe. Açıklama ve özet düz metindir; İngilizce (`en`) zorunludur, Türkçe (`tr`) gibi başka diller isteğe bağlı eklenir, uygulama arayüz diline göre seçer.
- **Güvenli yollar:** mutlak yol, `..`, sürücü harfi (`C:`), ters bölü (`\`), `:` (alternatif veri akışı), sonu nokta/boşluk olan ya da `CON`, `NUL`, `COM1` gibi aygıt adlı dosya yok; bağlantı (symlink) yok.
- Her paket yalnız kendi türünün dosyalarını içerir (ör. tema paketinde yalnız `manifest.json`, `theme.json`, `preview.png`).
- Aynı kimlikle ikinci paket eklenemez; kimlik klasör adıyla aynıdır. Kimlikte başkasının adı ya da markası kullanılmaz.
- Paket üretimi deterministiktir (sıralı adlar, sabit tarih). `store/packages`, `store/previews` ve `index.json` elle düzenlenmez.

## Bakımcı inceleme listesi

PR'ı birleştirmeden önce:

- [ ] `Store check` yeşil; `index.json` yalnız beklenen öğeyi ekledi/güncelledi (başka öğeler değişmedi, `downloads` sayaçları el ile şişirilmedi).
- [ ] `store/src/<id>/` içeriği pakete tam uyuyor; pakette gizli ya da gereksiz dosya yok. `unzip -l` ile bakın.
- [ ] Kod/XAML/betik/çalıştırılabilir yok; `capabilities` boş; `entryKind: data`.
- [ ] Lisans doğru ve içerikle uyumlu; atıflar `description` içinde. Kopyalanmış görsel, marka logosu ya da telifli eser yok (simge ve arka planları tersine görsel aramayla kontrol edin).
- [ ] Ad, özet ve açıklama yanıltıcı değil; başka bir eklentiyi, kişiyi ya da markayı taklit etmiyor; uygunsuz içerik yok. Yazarlık dürüst.
- [ ] Tema: uygulamada denendi, metin okunur (özellikle `KoyTextPrimary` / `KoyBgWindow` karşıtlığı), bozuk/saçma renk yok. Uygulama tarafındaki ayrıştırıcı bilinmeyen anahtarı yok sayar, ama gereksiz anahtar eklenmemeli.
- [ ] Simge paketi: dosya adları gerçek uygulama anahtarlarıyla eşleşiyor, her simge okunur boyutta ve şeffaf arka planlı/temiz.
- [ ] Canlı arka plan: kısa, döngüsel, boyutu makul, ani yanıp sönme (nöbet riski) yok, yazıyı okunmaz kılmıyor.
- [ ] Profil: yalnız amaçlanan bölümleri içeriyor, kullanıcıya zarar veren ayar (kısayol çalma, çıkışı gizleme vb.) yok.
- [ ] Güncellemede sürüm artmış, `updated` güncellenmiş, eski paket dosyası silinmiş.
- [ ] Önizleme gerçek içeriği gösteriyor.

Kaldırma/şikâyet: sorunlu bir eklenti için issue açılır; bakımcı öğeyi `store/src/<id>/` ile birlikte siler (kullanıcıdaki kopya, kullanıcı kaldırana kadar çalışmaya devam eder).

## Betikler

```powershell
python scripts/build-store.py            # paketleri, önizlemeleri ve index.json'u üretir
python scripts/build-store.py --check    # yazmadan doğrular (CI), fark ya da hata varsa çıkış kodu 1
python scripts/make-theme.py             # store/palettes/*.json'dan temaları üretir, bütün tema önizlemelerini çizer (Pillow gerekir)
python scripts/make-theme.py <id>        # yalnız bu paletin theme.json'u ve preview.png'si
python scripts/make-theme.py --preview <id>   # elle yazılmış temanın yalnız önizlemesi
python scripts/make-store-samples.py     # tema dışı örnek paketlerin görsellerini yeniden çizer (Pillow gerekir)
```

### Paletten tema üretmek

Bir temanın ~120 renk anahtarını elle seçmek gerekmez. `store/palettes/<id>.json` içine 20 temel renk yazın
(yüzeyler, yazı, vurgu, durum ve çentik sekme renkleri; biçim `scripts/make-theme.py` başında), sonra
`python scripts/make-theme.py <id>` çalıştırın. Betik Koy penceresi, menü, çentik ve dock anahtarlarının hepsini
türetir, okunurluğu (WCAG karşıtlığı: ana metin en az 4.5) denetler ve mağaza kartı önizlemesini (çentik, pencere,
menü, dock) çizer. `manifest.json` ve `store.json`'u siz yazarsınız; palet dosyası pakete girmez. Türetilen bir
değeri ya da yazı tipi / köşe gibi anahtarları paletteki `tokens` nesnesiyle ezebilirsiniz.

Gerçek uygulamada denemek için: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix t --scene centik --user-theme store/src/<id>/theme.json`.
