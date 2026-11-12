# Eklenti mağazası (Issue #51)

Masaüstü ortamlarındaki "yeni içerik getir" pencerelerinin bilinen modelini izler: merkezi bir indeks, kategori,
arama, kurulum ve güncelleme tek yerde; kurulum ile uygulama ayrı. Koy'un değişmez kurallarıyla (AGENTS.md) çelişen her şey dışarıda kalır.

## Kapsam ve kararlar

| Konu | Karar |
|---|---|
| Ne indirilir | **Yalnız veri paketi:** tema (`theme`), simge paketi (`iconpack`), canlı arka plan (`background`), ayar profili (`profile`). Kod çalıştıran tür yok (AGENTS #4, tek süreç; XAML de kabul edilmez). |
| Kaynak | Sunucu yok. Depodaki `store/index.json` (GitHub raw). Geliştirici PR ile ekler, bakımcı inceler. |
| Kurulum ≠ uygulama | Kurulum paketi `%AppData%\Koy\extensions\<id>\` altına açar. "Uygula" düğmesi ayrı bir eylemdir. |
| Kayıt | `%AppData%\Koy\extensions.json`: kimlik, sürüm, tür, kurulum zamanı, açık/kapalı, kurulan dosya listesi. Kaldırma yalnız kayıttaki klasörü siler. |
| Güncelleme | Sürüm farkı "Güncelle" düğmesi gösterir; kendiliğinden güncellemez. Mağaza yalnız sayfa açıkken indeksi çeker (AGENTS #5); arka planda yoklama yok. |
| Arayüz | Arama, sıralama (En yeni, Alfabetik, En çok indirilen), süzgeç (Tümü, Kurulu, Güncellemeler), kategori, kart listesi, ayrıntı. Puan yok (sunucu yok). |
| Dosyadan kur | Var: yerel `.koyext` (zip) seçilir; aynı doğrulamadan geçer. |
| İzinler | Manifestte bildirilir, kurulumdan önce gösterilir. Veri paketleri hiçbir yetki istemez; yetki isteyen paket şimdilik reddedilir. |
| Lisans | `license` (SPDX) zorunlu. |

## İndeks biçimi (`store/index.json`)

```json
{
  "format": "koy-store-index",
  "formatVersion": 1,
  "generated": "2026-11-12T00:00:00Z",
  "items": [
    {
      "id": "koyhq.nord-theme",
      "name": "Nord",
      "kind": "theme",
      "version": "1.0.0",
      "author": "Koy",
      "license": "GPL-3.0-or-later",
      "summary": "Kısa açıklama (en çok 200 karakter)",
      "description": "Uzun açıklama (en çok 2000 karakter, düz metin)",
      "homepage": "https://github.com/koyhq/koy",
      "updated": "2026-11-12",
      "downloads": 0,
      "package": { "url": "https://raw.githubusercontent.com/koyhq/koy/main/store/packages/koyhq.nord-theme-1.0.0.koyext", "sha256": "<64 hex>", "size": 1234 },
      "preview": { "url": "https://raw.githubusercontent.com/.../preview.png", "sha256": "<64 hex>" }
    }
  ]
}
```

Kurallar: `id` manifest kimliği kuralları (ExtensionManifestValidator); `kind` kapalı küme; URL'ler yalnız `https`; indeks en çok 1 MB ve 500 öğe;
bilinmeyen alan yok sayılır, zorunlu alanı bozuk öğe atlanır (bütün indeks reddedilmez); `formatVersion` büyükse indeks reddedilir.

## Paket biçimi (`.koyext` = zip)

```
manifest.json          { "id", "name", "version", "author", "kind", "license", "capabilities": [] , "entryKind": "data" }
theme.json             (kind=theme)       user-theme.json biçimi, UserStyleParser ile doğrulanır
icons/<Uygulama>.png   (kind=iconpack)    .png/.ico/.svg, IconPackFiles kuralları
background.gif|.webp|.mp4|.webm (kind=background)  LiveBackgroundFileValidator uzantıları
profile.json           (kind=profile)     SettingsProfileCodec biçimi
preview.png            (isteğe bağlı)
```

Açma kuralları: indirme boyutu ≤ 25 MB, açılmış toplam ≤ 50 MB, en çok 512 dosya; mutlak yol, `..`, sürücü harfi, ters bölü, alternatif veri akışı yok (zip slip);
türe göre izinli uzantılar dışındaki dosya reddedilir; SHA256 indeksteki ile eşleşmezse paket silinir. Paketteki manifest kimliği/sürümü/türü indeksteki ile aynı olmalı.
Açma önce geçici klasöre, doğrulama bitince tek taşıma ile `extensions\<id>\` klasörüne yapılır (yarım kurulum kalmaz).

## Uygulama ("Uygula")

- `theme`: mevcut `user-theme.json` `user-theme.backup.json` olarak saklanır, paketin `theme.json`'u kopyalanır, `UserStyleOverridesEnabled = true`.
- `iconpack`: `IconPack = Folder`, `IconPackFolder = extensions\<id>\icons`.
- `background`: `LiveBackgroundSource = File`, `LiveBackgroundFilePath = extensions\<id>\background.*`; hedef Kapalıysa çentik.
- `profile`: profil kitaplığına içe alınır (uygulamaz; profiller bölümünden uygulanır).

Kaldırma, uygulanmış bir paketse ayarı varsayılana döndürür (simge paketi → Builtin, arka plan → Ambiyans, tema → yedek geri yüklenir).

## Katmanlar

| Katman | Yer |
|---|---|
| Core | `src/Core/Extensions/Store/`: indeks modeli ve ayrıştırıcı, paket giriş denetimi (zip slip, sınırlar, tür uzantıları), sürüm karşılaştırma, mağaza listesi durumu (süzgeç, sıralama, arama, kurulu/güncelleme), kurulu kayıt (`extensions.json`), uygulama planı, `IExtensionStoreClient` |
| Platform | `src/Platform/Extensions/`: `AppHttp` ile indeks ve paket indirme (boyut sınırlı akış), SHA256, zip açma, kurulum klasörü |
| App | `src/App/Features/Extensions/` (`ExtensionStoreViewModel`, `StoreCardViewModel`, `ExtensionFileActionRunner`), `src/Platform/Extensions/StorePreviewLoader.cs` (önizleme + önbellek), `src/App/Features/Settings/Pages/EklentilerPage.xaml`: KoyWindow'da "Eklentiler" sayfası. Yapıldı; ayrıntı `docs/MAP.md` › Eklenti mağazası |
| Depo | `store/index.json`, `store/packages/`, `store/src/<id>/` (paket kaynakları), `scripts/build-store.py` (zip + sha256 + indeks), `store/README.md` (gönderim kuralları) |
