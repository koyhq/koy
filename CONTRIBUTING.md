# Katkıda Bulunma Rehberi (Contributing to Koy)

**Koy** projesine katkıda bulunmak istediğiniz için teşekkürler! Bu belge; kod mimarisini, açık kaynak geliştirme kurallarını, kod yazım standartlarını ve PR sürecini açıklar.

> *English summary:* Koy is a lightweight, zero-browser, single-process Windows 11 Notch & Dock written in C# (.NET 10) and WPF. Please follow the strict 3-layer architecture (`Core` -> `Platform` -> `App`), zero-idle-CPU spring animation rules, and memory discipline outlined below.

---

## 1. Mimari ve Katman Kuralları (Strict Layering)

Koy, tek bir süreçte çalışan üç katmanlı temiz bir mimariye sahiptir:

```text
src/
  Core/         Saf C# (net10.0) — WPF, Win32 ve WinRT KESİNLİKLE YOK
  Platform/     Windows 11 API uygulamaları (CsWin32 & WinRT) — WPF YOK
  App/          WPF arayüzü, ince ViewModel'ler (CommunityToolkit.Mvvm) ve XAML görünümleri
tests/
  Core.Tests/   xUnit birim testleri (Yalnızca Core katmanını test eder)
```

### Değişmez Mimari İlkeler
1. **Bağımlılık Yönü:** `App → Platform → Core` ve `App → Core`. `Core` hiçbir dış katmana veya işletim sistemi API'sine bağımlı olamaz.
2. **Sözleşmeler Core'da:** Her platform servisinin arayüzü (`IMediaService`, `IAudioService`, `IMonitorService` vb.) ve veri modeli `src/Core/<Alan>/` içinde tanımlanır.
   - `Core` arayüzlerinde **WinRT veya Win32 tipi bulunamaz** (`HWND`, `HMONITOR`, `IRandomAccessStreamReference` yasaktır).
   - Pencere ve ekran tanıtıcıları `nint`, görseller `byte[]` olarak taşınır.
3. **CsWin32 Kullanımı:** Elle `[DllImport]` veya `[LibraryImport]` yazılmaz. Gerekli Win32 API veya COM arayüzünün adını `src/Platform/NativeMethods.txt` dosyasına ekleyin; `Microsoft.Windows.CsWin32` kaynak üreticisi tip güvenli P/Invoke kodunu otomatik üretir.
4. **Özellik Giriş Noktası (`IFeature`):** Her ana özellik (`NotchFeature`, `DockFeature`) `IFeature` (`Start` / `Stop`) arayüzünü uygular. Kapalı olan bir özelliğin penceresi oluşturulmaz, arka plan servisi veya WinEvent kancası (`SetWinEventHook`) kurulmaz.
5. **Monolitik Dosya Yasağı:** Tek bir dosyaya hem iş mantığı, hem P/Invoke, hem de UI mantığı yığılamaz. Alt sekmeler (`Pages/`), pencereler ve dönüştürücüler (`Converters`) ayrı dosyalarda modüler tutulur.

---

## 2. Performans ve Bellek Disiplini

Koy'u elektron/tarayıcı tabanlı alternatiflerden (Tauri/WebView2/Electron) ayıran en temel özellik **hafifliğidir**:

1. **Tarayıcı Yok:** WebView2, CefSharp, Electron veya Blazor Hybrid kesinlikle kullanılmaz.
2. **Ağır Kütüphane Yok:** Serilog, Autofac, ReactiveUI, MahApps gibi ağır NuGet paketleri eklenmez. Ayarlar serileştirmesi için yansımalı (reflection) JSON yerine kaynak üreticili `SettingsJsonContext` kullanılır.
3. **Yazılımla Çizim (`RenderMode.SoftwareOnly`):** GPU sürücülerinin sürece ~135 MB eklemesini önlemek için varsayılan olarak yazılımla çizim kullanılır.
4. **Sıfır Boşta İşlemci (`0% Idle CPU`):**
   - WPF `Storyboard` yerine `Koy.Core.Animation.Spring` fizik motoru kullanılır.
   - `CompositionTarget.Rendering` yalnızca animasyon başladığında bağlanır, yay dengeye ulaştığında (`Spring.IsAtRest`) derhal çözülür.
   - Görünmeyen panellerde `DispatcherTimer` çalıştırılmaz; Kontrol Merkezi ve Durum servisleri ilgili panel ilk kez açılana kadar tembel (lazy) başlatılır.
5. **Görsel Optimizasyonu:** Albüm kapakları ve uygulama simgeleri gösterilecek boyutta (`DecodePixelWidth`) çözülür ve derhal `.Freeze()` çağrılır.
6. **Sabit Tuval Kuralı:** Çentik penceresi (`NotchWindow`) animasyon sırasında `Window.Width/Height` değiştirmez (saydam pencerede titreme yapar); sabit tuval içinde `NotchShape` geometrisi büyüyüp küçülür.

---

## 3. Görev Çubuğu Güvenliği (Kritik Kural)

Koy, Dock etkinken Windows görev çubuğunu (`Shell_TrayWnd` ve ikincil ekranlardaki `Shell_SecondaryTrayWnd`) gizler.
- Uygulama nasıl kapanırsa kapansın (normal çıkış, yakalanmamış istisna, güncelleme, kaldırma veya Görev Yöneticisi'nden zorla sonlandırma sonrası yeniden açılış), **Windows görev çubuğu mutlaka geri gelmelidir** (`TaskbarService.Restore()`).
- **Dock kapalıyken (`DockEnabled = false`)** açılıştaki güvenlik `Restore()` çağrısı dışında Windows görev çubuğuna asla dokunulmaz.
- Dock ile ilgili her PR'da şu testi elle yapın:
  1. Koy çalışırken Görev Yöneticisi'nden `Koy.exe` sürecini öldürün.
  2. Koy'u yeniden başlatın veya Hakkında sayfasındaki **"Windows görev çubuğunu geri getir"** düğmesine basın.
  3. Görev çubuğunun eksiksiz geri geldiğini doğrulayın.

---

## 4. Kod Biçimi ve UI Kuralları

- **C# Standartları:** Dosya kapsamlı namespace (`namespace Koy.Core.Notch;`), `<Nullable>enable</Nullable>`, private alanlar `_camelCase`.
- **MVVM:** ViewModel'lerde `CommunityToolkit.Mvvm` kaynak üreticileri (`[ObservableProperty]`, `[RelayCommand]`) kullanılır.
- **Sıfır Düz Emoji:** Arayüzde düz Unicode emojiler (`🚀`, `🎵`, `⚙️` vb.) **kesinlikle kullanılmaz**. Tüm ikonlar `Styles.xaml` veya ilgili XAML içinde temiz vektörel `Path` (`Geometry`) olarak tanımlanır.
- **İşlevsiz Buton Yasağı:** Arayüze tıklanınca çalışmayan sahte buton veya işlevsiz kısayol eklenmez.
- **Çoklu Dil (TR / EN):** Kullanıcıya görünen hiçbir metin XAML veya C# içine sabit yazılmaz. Her yeni metin hem `src/App/Resources/Strings.resx` (İngilizce) hem de `src/App/Resources/Strings.tr.resx` (Türkçe) dosyalarına eklenir.

---

## 5. Geliştirme Ortamı ve Komutlar

### Gereksinimler
- Windows 11 (22H2 / Derleme 22621 veya üzeri)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Derleme, Test ve Paketleme
```powershell
# Derle (Release, sıfır uyarı)
dotnet build Koy.slnx -c Release -warnaserror

# Birim testlerini çalıştır (xUnit)
dotnet test Koy.slnx -c Release

# Uygulamayı çalıştır
dotnet run --project src/App

# Bellek kullanımını ölç (Private Bytes)
powershell -ExecutionPolicy Bypass -File scripts\measure-memory.ps1

# Velopack kurulum paketi ve Portable ZIP üret
powershell -ExecutionPolicy Bypass -File scripts\pack.ps1 -Version 0.1.0
```

---

## 6. Git Akışı ve Pull Request Süreci

1. **Dal (Branch) İsimlendirmesi:**
   - Yeni özellikler: `feat/kisa-aciklama`
   - Hata düzeltmeleri: `fix/kisa-aciklama`
   - Performans iyileştirmeleri: `perf/kisa-aciklama`
2. **Commit Mesajları:** Konvansiyonel (`feat:`, `fix:`, `perf:`, `refactor:`, `docs:`, `test:`) önekleriyle, Türkçe veya İngilizce net emir kipinde yazılır.
3. **Test Zorunluluğu:** `Core` katmanına eklenen her durum makinesi, yerleşim hesaplayıcısı veya kural için `tests/Core.Tests/` altında xUnit testi yazılmalıdır.
4. **Lisans:** Gönderdiğiniz her katkı, projenin [GPLv3](LICENSE) lisansı altında yayınlanmayı kabul ettiğiniz anlamına gelir.

---

## 7. Sürüm Nasıl Çıkar

Sürümler [release-please](https://github.com/googleapis/release-please) ile otomatik hazırlanır; elle etiket atılmaz.

1. **Commit önekleri sürümü belirler** (`main` dalına birleşen commit'lere bakılır):
   - `fix:` → yama sürümü, değişiklik günlüğünde **Düzeltmeler**.
   - `perf:` → yama sürümü, **Performans**.
   - `!` işareti ya da `BREAKING CHANGE:` (ör. `fix!:`) → 1.0 öncesinde minör sürüm.
   - `docs:`, `refactor:`, `test:`, `chore:`, `ci:` değişiklik günlüğüne girmez ve tek başına sürüm çıkarmaz.
2. `main`'e her birleştirmede release-please bir **sürüm PR'ı** açar ya da günceller (`CHANGELOG.md` ve `Directory.Build.props` içindeki sürüm numarası).
3. Yayınlamaya hazır olunca bu PR'ı birleştirin. Başka bir şey yapmayın.
4. Birleştirmeden sonra otomatik olarak etiket (`vX.Y.Z`) ve GitHub release'i oluşur, aynı iş akışı kurulum dosyasını, taşınabilir zip'i ve `SHA256SUMS.txt` dosyasını release'e ekler.

Sürüm PR'ının metnini (`CHANGELOG.md`) birleştirmeden önce düzenleyebilirsiniz. `CHANGELOG.md` içindeki elle yazılmış `[Unreleased]` bölümü release-please'in ürettiği girdiyle aynı değişiklikleri anlatır: sürüm PR'ında `[Unreleased]` başlığını silin ve içeriğini yeni sürüm başlığının altına taşıyın (release-please yalnız `fix:`/`perf:` satırlarını listeler, `refactor:` önekli kullanıcıya dönük işler günlüğe girmez). Bir etiketi yeniden paketlemek gerekirse Actions sekmesinden **Release** iş akışını elle (`workflow_dispatch`) etiket adıyla çalıştırın.
