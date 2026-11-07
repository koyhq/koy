# Güvenlik Politikası (Security Policy)

## Desteklenen Sürümler (Supported Versions)

Koy'un güvenlik güncellemeleri en son yayınlanan kararlı sürüm üzerinden sağlanır:

| Sürüm | Destek Durumu |
| --- | --- |
| `0.1.x` | Destekleniyor |

## Güvenlik Mimarisinin Temelleri

Koy, Windows 11 masaüstü ortamında çalışırken kullanıcı güvenliğini ve gizliliğini korumak için şu ilkeleri uygular:

1. **Sıfır Telemetri ve Bulut Hesabı:** Koy hiçbir kullanıcı verisini, ekran görüntüsünü veya dosya bilgisini dış sunuculara göndermez.
2. **Yerel Veri Saklama:** Ayarlar (`%AppData%\Koy\settings.json`) ve Raf öğeleri (`%AppData%\Koy\shelf.json`) yalnızca kullanıcının yerel diskinde tutulur.
3. **Minimale İndirgenmiş Ağ İstekleri:** Ağ erişimi yalnızca iki amaçla yapılır:
   - Hava durumu paneli açıldığında **Open-Meteo** API'sinden (`api.open-meteo.com`) anahtarsız hava durumu verisi çekmek (cihaz konum izni istenmez, yalnız ayardaki şehir adı kullanılır).
   - **Velopack** aracılığıyla resmi GitHub Releases (`github.com/koyhq/koy`) üzerinden sürüm denetimi yapmak.
4. **SHA256 Bütünlük Doğrulaması:** Her GitHub Release sürümü, kurulum ve taşınabilir paketlerin bütünlüğünü doğrulamak için `SHA256SUMS.txt` dosyası ile birlikte yayınlanır.

## Güvenlik Açığı Bildirimi (Reporting a Vulnerability)

Koy'da bir güvenlik açığı (ör. ayrıcalık yükselmesi, güvensiz dosya işlemi, P/Invoke bellek hatası) tespit ederseniz:

1. **Lütfen açığı herkese açık bir GitHub Issue olarak paylaşmayın.**
2. Bunun yerine GitHub deposundaki **[Security Advisories -> Report a vulnerability](https://github.com/koyhq/koy/security/advisories/new)** sekmesini kullanarak özel bildirim oluşturun.
3. Bildiriminize hatanın nasıl tetiklendiğini, etkilenen bileşeni (`Core`, `Platform`, `App`) ve varsa çözüm önerinizi ekleyin.

Bildiriminiz en geç **72 saat** içinde incelenecek ve doğrulanan açıklar için yama sürümü yayınlanacaktır.
