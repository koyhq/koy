# Proje haritası

"Şunu değiştirmek istersem nereye bakarım?" sorusunun cevabı. Kurallar `AGENTS.md`'de, tasarım `docs/DESIGN.md`'de, sıradaki işler `docs/ROADMAP.md`'de.

Dosyalar her başlıkta **Core → Platform → App** sırasıyla yazılıdır. Bağımlılık yönü de budur: Core başka katmanı bilmez.

## Her işin sonunda
- `dotnet build Koy.slnx` ve `dotnet test Koy.slnx` temiz.
- Kullanıcıya görünen yeni metin iki resx dosyasında da var.
- Arayüz değiştiyse `tools/Koy.UiSnapshots` ile görsel kanıt alındı.
- Aşama bittiyse bellek ölçüldü, `docs/MEMORY.md` güncellendi (AGENTS #1).
- `docs/ROADMAP.md` içindeki kutu işaretlendi.

## Çentiğe yeni sekme eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Notch/NotchPanel.cs` | `NotchPanel` ve `NotchPanels.Order` listesine değer |
| App | `src/App/Features/Notch/NotchTabs.cs` | Yazı anahtarı, simge ve vurgu token'ıyla bir satır |
| App | `src/App/Features/Notch/NotchViewModel.cs` | `CurrentPanelContent` içine bir satır |
| App | `src/App/Features/Notch/NotchWindow.xaml` | `BodyHost` içine sayfanın `DataTemplate`'i |
| App | `src/App/Features/Notch/NotchFeature.cs` | Sayfanın ViewModel'ini kurar |
| App | `src/App/Resources/IconsNotch.xaml`, `src/App/Resources/Tokens.xaml` | Simge ve vurgu rengi |

Unutma:
- Sayfa `src/App/Features/<Ad>/` altında kendi klasöründe durur. Mantık Core'da, ViewModel ince kalır.
- Servisler yalnız sayfa görünürken alınır (`SetActive` kalıbı; örnek: `src/App/Features/SystemInfo/SystemStatsViewModel.cs`).
- Tuval büyütülmez (AGENTS #13). Ölçüler `src/Core/Notch/NotchLayout.cs` içinde.

## Yeni ayar eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Settings/AppSettings.cs` | Varsayılan değerli alan |
| Core | `src/Core/Settings/SettingsJsonContext.cs` | Yeni enum ya da liste tipi varsa `[JsonSerializable]` |
| Core | `src/Core/Settings/SettingsMigration.cs` | Yalnız eski değerin anlamı değiştiyse |
| App | `src/App/Features/Settings/KoyWindowViewModel.cs` | Özellik; `LoadFromSettings` içinde okuma, kaydetme bölümünde yazma |
| App | `src/App/Features/Settings/Pages/` | İlgili sayfaya satır (`CentikPage`, `DockPage`, `GenelPage`…) |
| App | `src/App/Features/Wizard/SetupWizardViewModel.cs` | Yalnız ilk açılışta sorulacaksa |

Unutma:
- Yansımalı `JsonSerializer` çağrısı yazılmaz (AGENTS #10).
- Dock ayarıysa `src/Core/Dock/DockSettingsDiff.cs` neyin yeniden kurulacağına karar verir.
- Test örnekleri: `tests/Core.Tests/SettingsStoreTests.cs`, `tests/Core.Tests/SettingsMigrationTests.cs`.

## Yeni kullanıcı metni ya da çeviri eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| App | `src/App/Resources/Strings.resx` | İngilizce metin |
| App | `src/App/Resources/Strings.tr.resx` | Türkçe metin, aynı anahtar |

- XAML: `Text="{Binding [Anahtar], Source={x:Static res:Loc.Instance}}"`. Dil değişince kendisi yenilenir (`src/App/Resources/Loc.cs`).
- C#: `Strings.Get("Anahtar")` (`src/App/Resources/Strings.cs`).
- Core ve Platform resx'e erişemez: metin değil, anahtar ya da enum döndürür; çeviriyi App yapar.

## Yeni platform servisi eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/<Alan>/` | Arayüz ve veri modeli; WinRT/Win32 tipi yok (AGENTS #11) |
| Platform | `src/Platform/<Alan>/` | Windows uygulaması |
| App | `src/App/SharedServices.cs` | Birden çok yer kullanacaksa sayaçlı `Acquire…` / `Release…` |
| Test | `tests/Core.Tests/` | Karar mantığı sahte servisle |

Unutma: Servis yalnız gerektiğinde çalışır, son bırakan zamanlayıcıyı söker (AGENTS #5). Yeni NuGet paketi için proje sahibine sorulur (AGENTS #8).

## Yeni Win32 çağrısı eklemek
1. API adını `src/Platform/NativeMethods.txt` dosyasına ekle; CsWin32 kodu üretir.
2. Çağrıyı yalnız `src/Platform/` altında yap. Elle `DllImport` yazılmaz.
3. Core'a `HWND` gibi tipler sızmaz: pencere kimliği `nint`, görsel `byte[]`.

## Dock davranışı ve görev çubuğu güvenliği
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Dock/` | Konum, yakınlık ve hiza kararları |
| Core | `src/Core/Shell/TaskbarStatePolicy.cs`, `src/Core/Shell/WatchdogPolicy.cs` | Görev çubuğu durumu ve gözcü kararları |
| Platform | `src/Platform/Shell/TaskbarService.cs`, `src/Platform/Shell/ShellTrayLocator.cs` | Görev çubuğunu gizleme ve geri getirme |
| App | `src/App/Features/Dock/DockFeature.cs` | Yaşam döngüsü |
| App | `src/App/Features/Dock/DockViewModel.cs`, `src/App/Features/Dock/DockFullWidth.xaml`, `src/App/Features/Dock/DockCapsule.xaml` | Görünüm |
| Core, Platform, App | `src/Core/StartMenu/`, `src/Platform/StartMenu/StartMenuHost.cs`, `src/App/Features/Dock/DockStartMenu.cs` | Başlat düğmesinin sağ tık (Win+X) menüsü: model ve eylemler Core'da, Windows tarafı Platform'da, çizim App'te |
| Gözcü | `src/Watchdog/` | Koy ölürse görev çubuğunu geri getiren ayrı süreç |

Dock'la ilgili her değişiklikte elle sınama (AGENTS #7):
- [ ] Görev Yöneticisi'nden Koy'u öldür: görev çubuğu birkaç saniyede kendiliğinden geri gelmeli.
- [ ] "Koy'dan çık" ile çık: görev çubuğu eski haline dönmeli.
- [ ] Dock kapalıyken Windows görev çubuğuna hiç dokunulmamalı (AGENTS #12).
- [ ] Tam ekranda ve iki ekranda davranış.

Görev çubuğu dosyalarını değiştirirken dikkat: gözcü `TaskbarService.cs` ve `ShellTrayLocator.cs` dosyalarını bağlayarak derler.

## Kısayol eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Shortcuts/ShortcutBindings.cs` | `ShortcutAction` değeri, varsayılan metin, `GetText` ve `WithText` satırı |
| Core | `src/Core/Settings/AppSettings.cs` | Kısayol alanı, varsayılanı `ShortcutBindings` ile aynı |
| App | `src/App/App.xaml.cs` | Tetiklenince ne olacağı (`ShortcutAction` switch'i) |
| App | `src/App/Resources/Strings.resx`, `src/App/Resources/Strings.tr.resx` | `Shortcut<EylemAdı>` anahtarı |

Kısayollar sayfasındaki satır `ShortcutBindings.All` listesinden kendiliğinden çıkar. Sistem kısayolları `src/Core/Shortcuts/ShortcutManager.cs` içindeki `ShortcutConflicts` ile elenir.

## Tema, renk, ikon, hareket
| Değişiklik | Dosya |
|---|---|
| Renkler | `src/App/Resources/Theme.Dark.xaml`, `src/App/Resources/Theme.Light.xaml` (ikisine birden) |
| Ölçü, yazı, köşe | `src/App/Resources/Tokens.xaml` |
| Ortak stiller | `src/App/Resources/Styles.xaml`, `src/App/Resources/StylesNotch.xaml` |
| Simgeler | `src/App/Resources/Icons.xaml`, `src/App/Resources/IconsNotch.xaml` |
| Hareket | `src/App/Resources/Motion.cs`, `src/Core/Appearance/MotionPolicy.cs`, `src/Core/Animation/Spring.cs` |
| Tema kararı | `src/App/Resources/ThemeManager.cs`, `src/Core/Appearance/` |
| Marka simgeleri | `assets/branding/`, sonra `python scripts/generate-icons.py` |

Unutma: Animasyon `Spring` ile yapılır ve yalnız hareket varken kare çizer (AGENTS #6). Yazılımla çizim varsayılandır (AGENTS #3).

## Siteyi güncellemek
- Dosyalar: `site/index.html`, `site/style.css`, `site/js/`, `site/img/`. Derleme adımı yok.
- Dene: `python -m http.server --directory site`.
- `main`'e `site/**` değişikliği gelince `.github/workflows/pages.yml` yayınlar.

## Ekran görüntüsü üretmek
```powershell
dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix <ad> --theme both --scene centik
```
- Sahneler `tools/Koy.UiSnapshots/Program.cs` içinde: `all`, `uninstall`, `centik`, `dockbar`, `dock`, `jumplist`, `startmenu`.
- Araç Win32 kancası, AppBar ya da görev çubuğu servisi başlatmaz; ayarları geçici dosyada tutar.
- README görselleri `docs/screenshots/` altındadır. Site için WebP'ye çevrilip `site/img/` içine konur.

## Sürüm çıkarmak
Sürümler release-please ile otomatiktir, elle etiket atılmaz. Ayrıntı: `CONTRIBUTING.md`.
- Ayar: `release-please-config.json`. Akışlar: `.github/workflows/release-please.yml`, `.github/workflows/release.yml`.
- `main`'e birleşen commit öneki sürümü belirler; release-please bir sürüm PR'ı açar. O PR birleşince etiket, GitHub release ve kurulum dosyaları oluşur.
- Yerelde paket: `powershell -ExecutionPolicy Bypass -File scripts\pack.ps1`.
