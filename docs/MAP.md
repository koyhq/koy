# Proje haritası

"Şunu değiştirmek istersem nereye bakarım?" sorusunun cevabı. Kurallar `AGENTS.md`'de, tasarım `docs/DESIGN.md`'de, sıradaki işler GitHub issue'larında (`docs/ROADMAP.md` tarihçe).

Dosyalar her başlıkta **Core → Platform → App** sırasıyla yazılıdır. Bağımlılık yönü de budur: Core başka katmanı bilmez.

## Her işin sonunda
- `dotnet build Koy.slnx` ve `dotnet test Koy.slnx` temiz.
- Kullanıcıya görünen yeni metin iki resx dosyasında da var.
- Arayüz değiştiyse `tools/Koy.UiSnapshots` ile görsel kanıt alındı.
- Aşama bittiyse bellek ölçüldü, `docs/MEMORY.md` güncellendi (AGENTS #1).
- İlgili issue PR'da `Closes #N` ile bağlandı.

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
| Core | `src/Core/Settings/SettingsSections.cs` | Varsayılan değerli alan, ilgili bölüm kaydına (`GeneralSettings`, `DockSettings`…). Eşitlik kayıttan kendiliğinden gelir |
| Core | `src/Core/Settings/SettingsJsonContext.cs` | Yeni enum ya da liste tipi varsa `[JsonSerializable]` |
| Core | `src/Core/Settings/SettingsMigration.cs` | Yalnız eski değerin anlamı değiştiyse |
| App | `src/App/Features/Settings/KoyWindowViewModel.cs` | Özellik; `LoadFromSettings` içinde okuma, kaydetme bölümünde yazma |
| App | `src/App/Features/Settings/Pages/` | İlgili sayfaya satır (`CentikPage`, `DockPage`, `GenelPage`…) |
| App | `src/App/Features/Wizard/SetupWizardViewModel.cs` | Yalnız ilk açılışta sorulacaksa |

Unutma:
- Yansımalı `JsonSerializer` çağrısı yazılmaz (AGENTS #10).
- Dock ayarıysa `src/Core/Dock/DockSettingsDiff.cs` neyin yeniden kurulacağına karar verir.
- Ayar araması ve "yalnız değişenler" satırı sayfadan kendisi bulur (`src/App/Features/Settings/SettingsPageIndex.cs`): satır `SettingRowStyle` kenarlığı, başlığı `SettingTitleStyle` ve `[Anahtar]` Loc bağı olmalı. Değişti noktası için denetimin bağlandığı VM özelliği `src/Core/Settings/SettingsKeyMap.cs` içinde olmalı; yoksa satır aranır ama noktası çıkmaz. Sayfa sıfırlama bölümleri: `KoyWindowViewModel.ResetSectionsFor`, `src/Core/Settings/SettingsPageReset.cs`.
- Test örnekleri: `tests/Core.Tests/SettingsStoreTests.cs`, `tests/Core.Tests/SettingsMigrationTests.cs`.

## Ayar profilleri (kaydet, uygula, içe/dışa aktar)
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Settings/Profiles/SettingsProfileCodec.cs` | Dosya biçimi: dışa aktar, doğrulamalı oku, bölümleri uygula. Yeni taşınabilir bölüm `ProfileSection`'a eklenir |
| Core | `src/Core/Settings/Profiles/ProfileLibrary.cs` | Kayıtlı liste mantığı (oluştur, içe al, adlandır, çoğalt, sil, `Find`, `Apply`); saf, testli |
| Core | `src/Core/Settings/Profiles/ProfileStore.cs` | `profiles.json` (ayar klasöründe) |
| App | `src/App/Features/Settings/ProfileService.cs` | Listele / uygula girişi (`ProfileService.Instance.Apply(idOrName)`): ayarı kaydeder, tema ve özellikleri yeniden kurar. Kurallar da bunu çağırır |
| App | `src/App/Features/Settings/ProfilesViewModel.cs` + `Pages/GenelPage.xaml` | Genel sayfasındaki Profiller bölümü |
| Core | `src/Core/Rules/` | Bağlam kuralları: `RuleEvaluator` (eşleşme), `ContextProfileSwitcher` (titreşim önleme, tekrar uygulamama, geri dönüş), `RuleListEditor`, `RuleStore` (`rules.json`, settings.json dışında), `RuleSources` |
| App | `src/App/Features/Rules/ContextRuleService.cs` | Ön plan olayını (`WindowTracker`) dinler, kural profilini `ProfileService` ile uygular/geri alır; etkin kural yoksa hiçbir şeye abone olmaz |
| App | `src/App/Features/Settings/RulesViewModel.cs` + `Pages/GenelPage.xaml` | Genel sayfasındaki Kurallar bölümü |

## Çentik kartı ve Windows bildirimleri
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Notch/NotchCard.cs`, `src/Core/Notch/NotchLayout.cs` | Kart modeli (güncelleme ve bildirim ortak) ve 380×72 ölçü |
| Core | `src/Core/Notifications/` | `NotificationDiff` (yeni gelen = kimlik farkı, ilk okuma temel çizgi), `NotificationPoller` (yoklama döngüsü, aralık 2 sn), `NotificationQueue` (tek kart, 6 sn, en çok 5 bekleyen, Koy ve Rahatsız Etme süzgeci), `NotificationPresentation` (üst satır, kısaltma, göreli zaman) |
| Platform | `src/Platform/Notifications/WinRtNotificationListener.cs` | `UserNotificationListener` okuması; olay yok, yoklama |
| Platform | `src/Platform/Shell/FocusAssistService.cs` | `IsDoNotDisturb` (Sessiz saatler, sunum modu) |
| App | `src/App/Features/Notch/NotchFeature.cs` | Dinleyiciyi kurar/söker (ayar açık + çentik var + izin), kuyruk → `Osd.ShowCard` |
| App | `src/App/Features/Notch/OSD/OsdIndicatorViewModel.cs`, `NotchCardView.xaml` | Kart gösterimi, simge çözme, başlık üstü satırı |
| App | `src/App/SharedServices.cs` | `AcquireNotificationListener` / `ReleaseNotificationListener` (sayaçlı) |
| App | `src/App/Features/Settings/Pages/CentikPage.xaml` | `NotchShowNotifications`, `NotchNotificationsHideBody` satırları |

Unutma: Bildirim yoklaması AGENTS #5'in tek yoklama istisnasıdır (paketsiz süreçte olay yok); yalnız ayar açıkken ve çentik varken çalışır, `Stop`'ta sökülür. Core arayüzünde WinRT tipi yok: simge `byte[]?`, kimlik `uint`. Bildirim kartı yalnız birincil ekranın çentiğinde çıkar.

## Canlı arka plan (çentik ve müzik kartı)
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Notch/LiveBackgroundPolicy.cs` | Hedef/kaynak enum'ları, önayar kataloğu, `LiveBackgroundPlaybackPolicy` (Off / ilk kare / oynat), yerel dosya doğrulayıcı, kare ve bellek sınırları (`GifFramePlan`, `LiveBackgroundLimits`) |
| Core | `src/Core/Background/` | Eski dilim: `BackgroundAnimationPolicy` (hareket kuralı, `LiveBackgroundPlaybackPolicy` buna devreder) ve `BackgroundSourceValidator` (yol denetimi) |
| Core | `src/Core/Settings/SettingsSections.cs` | `NotchSettings.LiveBackground*` (varsayılan hedef: Kapalı) |
| Core | `src/Core/Notch/Effects/` | Kodlu efektler: `BackgroundEffect` (küçük tampon, `Downscale`, 15 fps), `BackgroundEffects.All`, `EffectMath` (gürültü, fbm, palet, ışık noktası). Yeni efekt: sınıf + `All` + `LiveBackgroundPresets`'e `Effect` alanlı satır + `LiveBgPreset<Ad>` (iki resx). Testler `tests/Core.Tests/Notch/BackgroundEffectsTests.cs` (kare süresi bütçesi dahil); görsel `Koy.UiSnapshots --scene efektler`, CPU `--scene efekt-cpu` |
| App | `src/App/Features/Notch/LiveBackground/` | `LiveBackgroundState` (karar girdileri), `LiveBackgroundHost` (çizim, animasyon saatleri, video), `LiveBackgroundLoader` (GIF çözücü, 620 px, dondurulmuş kareler) |
| App | `NotchWindow.xaml`, `Home/HomePanel.xaml` | Host, çentik zemininin ve müzik kartı zemininin içeriğin altında |
| App | `Settings/Pages/CentikPage.xaml`, `KoyWindowViewModel.cs` | "Görseller" bölümü: hedef, kaynak, önayar, dosya seçici, perde |

Unutma: çentik kapalı / gizliyken hiçbir şey oynamaz ve kare tutulmaz (AGENTS #5, #6); hareketi azalt ve pil tasarrufunda yalnız ilk kare. Tuval 620×352 büyütülmez (AGENTS #13).
## Aramaya yeni kaynak eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Search/` | `SearchSource` türevi (örnekler: `UnitConverterSource` + `UnitConverter`, `ColorConverterSource` + `ColorParser`); kopyalama `ISearchShell.CopyText` ile |
| App | `src/App/Features/Search/SearchFactory.cs` | `SourceCatalog` satırı (Ayarlar'daki aç/kapat listesi) ve `Create` içinde `Add(...)` + `Syntaxes` |
| App | `src/App/Resources/Strings.resx`, `Strings.tr.resx` | `SearchCat<Ad>` ve `SearchSyntax<Ad>` metinleri |
| Test | `tests/Core.Tests/Search/` | Kaynak testleri (`SearchSourceTests`, `UnitAndColorConverterTests`) |

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
| Core | `src/Core/Dock/` | Konum, yakınlık, kenar ve hiza kararları (`DockPositionResolver`, `DockLayoutMath`, `DockFitMath`) |
| Core | `src/Core/Shell/TaskbarStatePolicy.cs`, `src/Core/Shell/WatchdogPolicy.cs` | Görev çubuğu durumu ve gözcü kararları |
| Platform | `src/Platform/Shell/TaskbarService.cs`, `src/Platform/Shell/ShellTrayLocator.cs` | Görev çubuğunu gizleme ve geri getirme |
| Core | `src/Core/Dock/DockClickPolicy.cs`, `src/Core/Dock/DockMonitorFilter.cs`, `src/Core/Dock/DockWindowGrouping.cs` | Sol tık eylemi kararı, "yalnız bu monitörün pencereleri" süzgeci (pencere sınırlarının merkezi); gruplama kararı henüz bağlı değil |
| App | `src/App/Features/Dock/DockFeature.cs` | Yaşam döngüsü |
| App | `src/App/Features/Dock/DockViewModel.cs`, `src/App/Features/Dock/DockFullWidth.xaml`, `src/App/Features/Dock/DockCapsule.xaml`, `src/App/Features/Settings/Pages/DockPage.xaml` | Görünüm, kenar/uzunluk/boşluk bağlamaları ve ayar sayfası |
| Core, App | `src/Core/Dock/DockPresentationMath.cs`, `src/App/Features/Dock/DockPresentationStyle.cs`, `src/App/Features/Dock/DockItemsStrip.xaml` | Simge göstergesi (nokta, çizgi, arka plan), rozet, kenar açma gecikmesi ve gizli dock çizgisi: karar Core'da, fırça ve köşe eşlemesi App'te |
| Core, App | `src/Core/Dock/Items/` (`DockItemLayout`, `DockCustomCommand`, `DockClockTick`), `src/App/Features/Dock/DockExtraViewModel.cs`, `DockViewModel.ComposeExtras` | Dock öğeleri (ayırıcı, boşluk, saat, çöp kutusu, özel düğme): düzen ve komut kuralları Core'da, çizim ve çalıştırma App'te. Eklemek için dock sağ tık menüsü "Öğe ekle" |
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

## Ekran köşesi eylemi eklemek
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Input/CornerActions.cs` | `CornerAction` değeri ve `ToInputAction` eşlemesi; bekleme durumu `HotCornerTracker`, koordinat kararı `HotCornerMath` |
| Core | `src/Core/Settings/SettingsSections.cs` | `HotCornersSettings` (köşe başına eylem, bekleme, tam ekranda kapalı) |
| App | `src/App/Features/Actions/HotCornerController.cs` | Yoklama yok: köşeye giriş/çıkış olayıyla tek seferlik bekleme zamanlayıcısı, tam ekran ve tuş denetimi |
| Platform | `src/Platform/Shell/CornerHookService.cs` | `WH_MOUSE_LL` kancası (kendi iş parçacığında, hiçbir şey yutmaz); yalnız bir köşe bağlıyken kurulur, yalnız köşe değişince haber verir (`Core/Input/ICornerHookService`) |
| App | `src/App/App.xaml.cs` | `RunInputAction`: eylemin ne yapacağı |
| App | `src/App/Features/Settings/Pages/KisayollarPage.xaml`, `HotCornersViewModel.cs` | Kısayollar sayfasındaki "Ekran köşeleri" bölümü |

## Tema, renk, ikon, hareket
| Değişiklik | Dosya |
|---|---|
| Renkler | `src/App/Resources/Theme.Dark.xaml`, `src/App/Resources/Theme.Light.xaml` (ikisine birden) |
| Ölçü, yazı, köşe | `src/App/Resources/Tokens.xaml` |
| Ortak stiller | `src/App/Resources/Styles.xaml`, `src/App/Resources/StylesNotch.xaml` |
| Simgeler | `src/App/Resources/Icons.xaml`, `src/App/Resources/IconsNotch.xaml` |
| Hareket | `src/App/Resources/Motion.cs`, `src/Core/Appearance/MotionPolicy.cs`, `src/Core/Animation/Spring.cs` |
| Tema kararı | `src/App/Resources/ThemeManager.cs`, `src/Core/Appearance/` |
| Kullanıcı stil dosyası (`user-theme.json`) | Core: `src/Core/Appearance/UserStyleOverrides.cs` (izinli anahtarlar `AllowedTokens`, kısıtlı ayrıştırıcı `UserStyleParser`), `UserStyleFile.cs` (okuma, boyut sınırı). App: `ThemeManager.cs` (tema sonrası uygular), `UserStyleResources.cs` (WPF'ye çevirir), `UserStyleWatcher.cs` (yalnız ayarlar açıkken izler), `Pages/GorunumPage.xaml`. Çentik zemini `KoyNotchSurface`'tan, dock zemini `KoyDockSurface` / `KoyDockCapsuleSurface`'tan okunur (`NotchViewModel.UpdateGlassSurface`, `DockViewModel.UpdateMaterialSurfaces`; `ThemeManager.Applied` ile yenilenir). Dock token'ları Theme.Dark/Light.xaml'de: `KoyDockSurface`, `KoyDockCapsuleSurface`, `KoyDockBorder`, `KoyDockCapsuleBorder`, `KoyDockSeparator`, `KoyDockDivider`, `KoyDockHover`, `KoyDockActive`, `KoyDockPill`, `KoyDockCover`, `KoyDockIndicator`, `KoyDockText`, `KoyDockTextSecondary`, `KoyDockGlyph`, `KoyDockClockText`, `KoyDockClockSubtext`, `KoyDockPreviewSurface`, `KoyDockPreviewText`. Tema denemek için: `Koy.UiSnapshots --user-theme store/src/<id>/theme.json`. Yeni renk/ölçü anahtarı eklersen `AllowedTokens`'a da ekle; `UserStyleOverridesTests` XAML ile eşitliği denetler. XAML okuyucu kullanılmaz |
| Marka simgeleri | `assets/branding/`, sonra `python scripts/generate-icons.py` |
| Dock uygulama simgesi: paket klasörü, uygulama başına değişim | Karar: `src/Core/Appearance/` (`IconResolver`, `IconPackFiles`, `AppIconKey`, `AppIconOverrides`). Yükleme: `src/App/Features/Dock/DockAppIcons.cs`. Menü: `DockItemsStrip.xaml`, kayıt: `DockViewModel.ChangeAppIcon`. Ayar satırı: `GorunumPage.xaml` |

Unutma: Animasyon `Spring` ile yapılır ve yalnız hareket varken kare çizer (AGENTS #6). Yazılımla çizim varsayılandır (AGENTS #3).

## Siteyi güncellemek
- Dosyalar: `site/index.html`, `site/style.css`, `site/js/`, `site/img/`. Derleme adımı yok.
- Dene: `python -m http.server --directory site`.
- `main`'e `site/**` değişikliği gelince `.github/workflows/pages.yml` yayınlar.
- Görsel, `style.css` ya da `js/` dosyası değişirse `?v=` sürüm etiketini yenile (`index.html`, `js/main.js` ve `js/notch.js` içindeki import'lar); yoksa ziyaretçi önbellekteki eskisini görür.

## Ekran görüntüsü üretmek
```powershell
dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix <ad> --theme both --scene centik
```
- Sahneler `tools/Koy.UiSnapshots/Program.cs` içinde: `all`, `uninstall`, `centik`, `dockbar`, `dock`, `jumplist`, `startmenu`.
- Araç Win32 kancası, AppBar ya da görev çubuğu servisi başlatmaz; ayarları geçici dosyada tutar.
- Sitedeki dock: `dotnet run --project tools/Koy.UiSnapshots -- --out <klasör> --prefix site --scene sitedock --transparent --scale 2`. Simgeler görev çubuğuna sabitlenmiş gerçek uygulamalardan gelir; çıktı kırpılıp `site/img/dock.webp` olur.
- README görselleri `docs/screenshots/` altındadır. Site için WebP'ye çevrilip `site/img/` içine konur.
- Sitedeki çentik görüntüleri saydam zeminlidir (gece mavisi `.aurora` zemininin üstünde durur). Yeni görüntüyü WebP'ye çevirdikten sonra `python scripts/cut-notch-shots.py site/img/<dosya>.webp` ile gri zeminden ayır.

## Sürüm çıkarmak
Sürümler release-please ile otomatiktir, elle etiket atılmaz. Ayrıntı: `CONTRIBUTING.md`.
- Ayar: `release-please-config.json`. Akışlar: `.github/workflows/release-please.yml`, `.github/workflows/release.yml`.
- `main`'e birleşen commit öneki sürümü belirler; release-please bir sürüm PR'ı açar. O PR birleşince etiket, GitHub release ve kurulum dosyaları oluşur.
- Yerelde paket: `powershell -ExecutionPolicy Bypass -File scripts\pack.ps1`.

## Çentik biçimi (yüzen ada)
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Notch/NotchStyle.cs` | Biçim kuralları: kulak yarıçapı, üst boşluk (9 DIP), köşe yarıçapı, kenar açma bölgesi |
| Core | `src/Core/Display/WindowPlacementMath.cs` | `TopCenter` ofset aşırı yüklemesi (tuval sabit, yalnız pencere Top'u kayar) |
| App | `src/App/Features/Notch/NotchShape.cs`, `NotchWindow.xaml(.cs)`, `NotchViewModel.cs` | Kulaksız şekil, ofsetli yerleşim, biçime göre köşe yarıçapı |
| App | `src/App/Features/Notch/NotchFeature.cs` | `NotchBounds` ve fare kenar bölgesi (`CheckEdgePeek`) ofseti sayar |

## Malzeme (Katı / Buzlu / Cam) ve okunabilirlik koruması
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Appearance/MaterialSetting.cs` | Seviyeler, opaklık, eski "Yarı saydam" çevirisi |
| Core | `src/Core/Appearance/ReadabilityGuard.cs` | Duvar kağıdı ortalamasına göre etkin seviye (ikincil metin ≥ 4,5:1) |
| Platform | `src/Platform/Display/WallpaperSampler.cs` | Duvar kağıdının ortalama rengi (16 px çözüm) |
| App | `src/App/Resources/MaterialState.cs` | Ne zaman okunur, etkin seviye (pencere / çentik / dock), `Changed` olayı |
| App | `src/App/Resources/ThemeManager.cs` | Pencere, satır, segment ve çentik kartı tonları |
| App | `NotchViewModel.SurfaceBrush`, `DockViewModel.FullWidthSurface` / `CapsuleSurface` | Çentik ve dock zemini |

## Eklenti mağazası (#51)
Tasarım ve dosya biçimleri: `docs/EXTENSION-STORE.md`. Yalnız veri paketi (tema, simge paketi, canlı arka plan, ayar profili); kod çalıştıran tür yok.
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Extensions/Store/` | İndeks ayrıştırıcı, paket giriş denetimi, kurulum (`ExtensionPackageInstaller`), kurulu kayıt (`InstalledRegistry`, `extensions.json`), liste durumu (`StoreListState`, `StoreCatalog`), uygulama planı (`ExtensionApplyPlanner`: Uygula / Kaldırırken geri al / `IsActive`) |
| Platform | `src/Platform/Extensions/ExtensionStoreClient.cs` | İndeks ve paket indirme (boyut sınırlı, SHA256), kurulum, kaldırma |
| Platform | `src/Platform/Extensions/StorePreviewLoader.cs` | Kart önizlemesi: https, en çok 1 MB, SHA256 doğrulama, `%LocalAppData%\Koy\store-cache\` önbelleği (en çok 200 dosya) |
| App | `src/App/Features/Extensions/ExtensionStoreViewModel.cs`, `StoreCardViewModel.cs` | Sayfa durumu: sorgu, kartlar, kurulum onayı, kur / güncelle / uygula / kaldır, dosyadan kur. `SetActive` ile yalnız sayfa görünürken indeks çekilir (AGENTS #5) |
| App | `src/App/Features/Extensions/ExtensionFileActionRunner.cs` | Planın dosya işleri: `user-theme.json` yedeği / kopyası / geri yükleme, profil içe alma |
| App | `src/App/Features/Settings/ProfileService.cs` | `CommitSettings`: hazır ayarı kaydedip tema ve özellikleri canlı yeniden kurar; `SettingsCommitted` olayıyla Koy penceresi değerleri yeniler |
| App | `src/App/Features/Settings/Pages/EklentilerPage.xaml`, `KoyWindow.xaml` | Sayfa (dizin 10, `KoyWindowViewModel.ExtensionsPageIndex`; kenar çubuğunda Görünüm'den sonra), kurulum onayı katmanı |
| App | `Pages/GorunumPage.xaml`, `CentikPage.xaml`, `GenelPage.xaml` | "Mağazadan daha fazla al…" bağlantıları (`OpenStoreCommand`, parametre: `IconPack`, `Theme`, `Background`, `Profile`) |
| Araç | `tools/Koy.UiSnapshots/ExtensionSnapshots.cs` | `--scene eklentiler`: depodaki `store/` klasöründen sahte ağla sayfanın görüntüleri |
| Depo | `store/`, `scripts/build-store.py` | İndeks, paketler, önizlemeler, kaynaklar |
| Depo | `store/palettes/`, `scripts/make-theme.py` | Yeni tema: 20 temel renkten bütün anahtarları türetir, karşıtlığı denetler, kart önizlemesini çizer (`store/README.md`) |

Unutma: Kurulum uygulama demek değildir. Kaldırmadan önce `RevertOnUninstall` uygulanır. Yeni hata enum değeri eklenirse `Strings*.resx` içine `ExtInstallError<Ad>`, `ExtIndexError<Ad>`, `ExtApplyError<Ad>` anahtarı da eklenir. Sayfa dizini en sona eklendi (Gelişmiş 9, Eklentiler 10); `SettingsPageIndex.PageNameKeys` buna bağlı.
