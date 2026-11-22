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
| Core | `src/Core/Notch/NotchServicePolicy.cs`, `src/Core/Notch/NotchSizePolicy.cs` | Hangi sayfa görünürken hangi servis çalışır (AGENTS #5 tablosu, `NotchViewModel.UpdateActivePanels` yalnız iletir) ve çentik hedef boyutları (kapalı, Peek, gösterge, kart, açık; `UpdateTargetSize`). Yeni sayfa/servis eklenince önce bu tabloya ve testine |
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
- Ayar araması ve "yalnız değişenler" satırı sayfadan kendisi bulur (`src/App/Features/Settings/SettingsPageIndex.cs`): satır `SettingRowStyle` kenarlığı, başlığı `SettingTitleStyle` ve `[Anahtar]` Loc bağı olmalı. Değişti noktası için denetimin bağlandığı VM özelliği `src/Core/Settings/SettingsKeyMap.cs` içinde olmalı; yoksa satır aranır ama noktası çıkmaz.
- Ayarlar penceresinde bir ViewModel özelliği değişince ne olacağı (kaydedilir mi, tema yeniden uygulanır mı, dock / çentik yeniden kurulur mu) `src/Core/Settings/SettingsPropertyEffects.cs` içindeki adlandırılmış kümelerdedir (`NotSaved`, `AppliesTheme`, `RebuildsNotch`…); `KoyWindowViewModel.OnPropertyChanged` yalnız sorar. Yeni ViewModel özelliği ayara yazılmayacaksa `NotSaved`'a, çentik/dock'u etkiliyorsa ilgili kümeye eklenir; test (`SettingsPropertyEffectsTests`) adların ayar anahtarıyla uyumunu denetler.
- Sayfa sıfırlama (tek ad "Varsayılana döndür", `SettingsResetPage`; sayfa başlığının sağında): hangi sayfa neyi sıfırlar `SettingsPageReset.TargetsFor` (Core, testli; Dock sayfası "Dock etkin" ve görev çubuğu gizleme anahtarlarını korur, Performans yalnız kendi iki alanını sıfırlar, Ekranlar sıfırlanmaz); `KoyWindowViewModel.ResetSectionsFor` buna devreder. Satır düzeyinde sıfırlama (kısayol, Gelişmiş) aynı metni kullanır. Mağaza adları yerleşik canlı arka plan adlarıyla çakışmaz (`RepositoryIndexTests`).
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
| Core | `src/Core/Shell/WindowHookPolicy.cs` | Pencere izleyicisinin WinEvent kancaları: hangi tüketici (çentik, dock, bağlam kuralları) hangi bilgiyi ister (`WindowHookNeeds`), buna yetecek en az kanca kümesi (`HooksFor`), olay sınıflandırma ve ucuz ön süzgeç |
| Platform | `src/Platform/Shell/WindowTracker.cs` + `WindowHookLease.cs` | Kancalar tüketici kiralarının birleşimine göre kurulur/sökülür (`Start(WindowHookNeeds)` kira döner, bırakılınca kancalar çekilir); dock yokken pencere listesi ve başlık kancaları kurulmaz |

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
| Core | (main'de yok) | Kodlu efektler (`src/Core/Notch/Effects/`, 17 efekt, `BackgroundEffects.All`, `EffectMath`) birleşmedi; kod `rescue/notch-effects` dalında. Alınırsa testleri `tests/Core.Tests/Notch/BackgroundEffectsTests.cs`, görsel `Koy.UiSnapshots --scene efektler`, CPU `--scene efekt-cpu` |
| App | `src/App/Features/Notch/LiveBackground/` | `LiveBackgroundState` (karar girdileri), `LiveBackgroundHost` (çizim, animasyon saatleri, video), `LiveBackgroundLoader` (GIF çözücü, 620 px, dondurulmuş kareler). Video `MediaElement` ile değil, `IVideoFrameReader` ile kare kare oynar (arka plan iş parçacığı, tek `WriteableBitmap`, 15 fps) |
| Core | `src/Core/Media/IVideoFrameReader.cs`, `VideoFramePacer.cs` | Video kare okuyucu sözleşmesi; döngü ve hız zamanlaması, kare hızı sınırı (testli) |
| Platform | `src/Platform/Media/MfVideoFrameReader.cs` | Media Foundation SourceReader, **yalnız yazılımla** çözme (DXVA kapalı, Direct3D yüklenmez; MediaElement donanımla çözünce ~150 MB kalıcı Direct3D yığını yüklüyordu, `docs/MEMORY.md` 2026-11-20) |
| App | `NotchWindow.xaml`, `Home/HomePanel.xaml` | Host, çentik zemininin ve müzik kartı zemininin içeriğin altında |
| App | `Settings/Pages/CentikPage.xaml`, `KoyWindowViewModel.cs` | "Görseller" bölümü: hedef, kaynak, önayar, dosya seçici, perde |

Opak müzik kartı (Kapak resmi stili ya da Katı malzemede albüm rengi) çentik zeminini örttüğü için o anda zemin durağan kalır: `MusicCardCoverage` (Core) girdisi `LiveBackgroundPlaybackPolicy.Decide(isBackdropCovered)`'e gider, `NotchViewModel.UpdateLiveBackground` besler.

Unutma: çentik kapalı / gizliyken hiçbir şey oynamaz ve kare tutulmaz (AGENTS #5, #6); hareketi azalt ve pil tasarrufunda yalnız ilk kare. Tuval 620×352 büyütülmez (AGENTS #13).
## Müzik sayfası hareketleri (#182)
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Media/TrackTransition.cs` | Parça değişimi geçişi: tek ilerleme yayı, kapak/başlık kaydırma, ölçek ve saydamlık değerleri (testli) |
| App | `src/App/Features/Music/TrackTransitionPresenter.cs` | Değerleri öğelere yazar; kare yalnız geçiş sürerken dinlenir, yüz gizlenince biter. Sekme geçişleri anahtarına uyar |
| Core | `src/Core/Animation/StaggeredEntrance.cs` | Kademeli giriş zamanlaması (sıra gecikmesi, ölçek, kayma) ve kapanış solması süresi (testli) |
| App | `src/App/Features/Notch/StaggeredEntrancePresenter.cs` | Öğeleri sırayla içeri alır; müzik yüzü görünür olunca oynar (`HomePanel`). Açılış/kapanış anahtarına uyar |
| App | `src/App/Features/Notch/ExpandedExitFade.cs` | Kapanışta açık içeriğin 120 ms solması: durum değişmeden önce (`PropertyChanging`) görünüm sabitlenir |
| Core, App | `src/Core/Notch/MirrorWave.cs`, `src/App/Features/Music/SpectrumBars.cs` (`Mirror`) | Ayna dalga: ortadan simetrik bant örneklemesi (testli), bütün genişliği dolduran çizim; fırça `MusicViewModel.MirrorWaveBrush` (kapak paleti) |
| Core, App | `src/Core/Media/CoverBlobs.cs`, `src/App/Features/Music/CoverBlobsView.cs` | Akan kapak rengi: leke renkleri paletten, yavaş döngü (testli); çizim 10 fps, yalnız görünür ve çalarken, renk değişiminde 0,6 sn geçiş. Kart kapaktan boyanıyorsa lekeler kartta canlı arka plana göre önceliklidir: `HomePanel` karttaki `LiveBackgroundHost`'u `Off` yapar |
| Core, App | `src/Core/Media/FrameGrid.cs`, `src/App/Features/Music/MusicFrameClock.cs` | Müzik sayfasının ortak saati: tek `CompositionTarget.Rendering` aboneliği, 30 Hz ızgara; lekeler (10 fps) ve plak (30 fps) aynı vuruşta çizilir, abonesi kalmayınca abonelik kalkar. Yeni müzik animasyonu kendi saatini kurmaz, buraya abone olur. Canlı arka plan (WPF saati / video iş parçacığı) ve ses çubukları (yakalama olayı) ızgaraya bağlı değildir |
| Core, App | `src/Core/Media/VinylMotion.cs`, `src/App/Features/Music/VinylView.cs` | Plak modu: dönüş hızı ve kol yayları (testli); yalnız etiket döner (30 fps). Ayar `MusicSettings.VinylMode`, düğme denetim sırasının solunda, kayıt `NotchFeature.SaveVinylMode` |
| Core | `src/Core/Lyrics/LyricsEmphasis.cs` | Söz satırının etkin çizgiye uzaklığına göre saydamlık ve ölçeği (testli) |
| App | `src/App/Features/Music/LyricsView.xaml(.cs)` | Etkin satırı ortaya yayla getirir, vurguyu konumdan hesaplar; kenar maskesi piksel cinsinden kurulur (göreli maske uzun listede bütün sözleri soldurur) |
| Core, App | `src/Core/Media/ScrubState.cs`, `HomePanel.xaml(.cs)` (`ScrubHit`), `MusicViewModel` (`BeginScrub`/`MoveScrub`/`EndScrub`/`CancelScrub`/`StepSeek`) | İlerleme çubuğunda sürükleyerek sarma: karar (kıstırma, önizleme, süre metni, canlıda kapalı) Core'da testli; sürüklerken seek yok, bırakınca tek `SeekAsync`, Esc / yakalama kaybı iptal, ←/→ 5 sn. Anlık görüntü: `--scene muzik-sarma` |
| App | `src/App/Features/Music/ProgressRing.cs`, `NotchWindow.xaml` (`MusicStateView`) | Kapalı çentikte kapak çevresindeki ilerleme halkası; ilerleme zamanlayıcısı kapalı çentik görünürken de çalışır |
| App | `src/App/Features/Home/HomePanel.xaml` | `OutgoingCover`, `IncomingCover`, `TrackTitleBlock`; kademeli giriş öğeleri `CoverFrame`, `TitleRow`, `MusicWave`/`MusicLyrics`, `ProgressRow`, `ControlsRow` |
| Araç | `tools/Koy.UiSnapshots` (`--scene centik`, `--scene sozler-canli`) | `muzik-gecis-*`, `muzik-acilis-*` ara kareleri, `kapali-muzik`, `muzik-dalga`, `muzik-plak`, `muzik-sozler`; `sozler-canli` sözleri gösterilen bir pencerede, zaman akarken dener ve etkin satır, kayma, saydamlık değerlerini yazar; `muzik-kayit` tanıtım GIF'i için müzik sayfası senaryosunu gerçek zamanlı kaydeder (`MusicPromoRecorder`) |

Unutma: shader efekti yok (`DropShadowEffect`, `BlurEffect` Direct3D yığınını yükler); yalnız dönüşüm, saydamlık, gradyan, kırpma.

## Geri al (onaysız silme) ve çentik erişilebilirliği
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Undo/UndoBuffer.cs` | Tek yuvalı geri alma kaydı (tür, sayı, bitiş, eylem): `Push`, `TryUndo`, `ExpireIfDue`, `Clear`; süre `TimeProvider`'dan (5 sn). Yeni geri alınabilir silme: `UndoKind`'a değer + `UndoToastViewModel.KeyFor`'a metin anahtarı (iki resx) |
| Core | `EventBook.Restore`, `ITaskService.Restore`, `ClipboardHistory.Restore` / `IClipboardHistoryService.Restore`, `IShelfService.Restore` | Silinen öğeyi aynı kimlikle geri koyar. Raf yalnız yol tutar: "Temizle" dosyaya dokunmaz |
| App | `src/App/Features/Notch/UndoToastViewModel.cs`, `NotchWindow.xaml` ("Geri al şeridi") | Metin ve süre zamanlayıcısı; zamanlayıcı yalnız şerit görünürken çalışır, çentik kapanınca ya da gizlenince kayıt bırakılır (`NotchViewModel.UpdateActivePanels` → `Undo.SetActive`). NotchFeature bir örneği Pano/Raf/Takvim/Görev VM'lerine `Undo` olarak verir; onlar silmeden sonra `Undo?.Offer(...)` çağırır |
| Core | `src/Core/Notch/QuickAddValidation.cs` | Takvim ve görev formunun hata kararı (boş başlık, geçersiz saat); App `FormError` metnini gösterir |
| Core, App | `src/Core/Appearance/NotchHighContrastMap.cs`, `src/App/Resources/NotchHighContrast.cs` | Windows Yüksek Karşıtlıkta çentik token'ları sistem renklerine bağlanır (ThemeManager override sözlüğünün sonunda). Yeni çentik rengi token'ı eklenince tabloya da eklenir (test token dosyasını okur) |
| App | `src/App/Resources/StylesNotch.xaml` (`NotchFocusVisual`), `Tokens.xaml` (`KoyNotchFocusRing`) | Odak halkası; çentikteki düğmeler `FocusVisualStyle="{DynamicResource NotchFocusVisual}"` kullanır (yeni düğmede `{x:Null}` yazma) |
| App | `NotchWindow.xaml.cs` (`FocusForKeyboard`, `ReleaseKeyboardFocus`, `OnPreviewKeyDown`) | Çentik yalnız kısayolla açılınca (`NotchFeature.ToggleExpanded`) klavye odağı alır; Tab / ok tuşları WPF gezintisi, Ctrl+Tab sekme, Esc kapatır ve önceki pencereye döner. Fareyle açılınca odak çalınmaz (AGENTS #5 ruhu) |

Unutma: ekran okuyucu için yeni denetimde `AutomationProperties.Name`; durum yalnız renkle verilmez (`ItemStatus`, simge).

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
| Core | `src/Core/Shell/TaskbarStatePolicy.cs`, `TaskbarBarSelector.cs`, `TaskbarRunOncePolicy.cs`, `TaskbarShutdownGuard.cs`, `WatchdogPolicy.cs` | Görev çubuğu durumu, hangi çubuk gizlenir, RunOnce yedeği, oturum kapanışı ve gözcü kararları |
| Core, Platform, App | `src/Core/Dock/AppBarMath.cs` (`ReservedPixels`, `FinalRect`), `src/Core/Dock/AppBarOwnership.cs` (`OwnsBottomEdgeAtStart`), `src/Platform/Shell/AppBarService.cs`, `DockFeature.ApplyTaskbarPolicy` | "Yer ayır" payı: görev çubuğu gizlenecekse AppBar ilk kayıttan itibaren monitör kenarından hesaplanır (Gezgin'in çubuğunun üstüne yığılmaz); pay = pencere kalınlığı + kenar boşluğu, pencere yerleşimiyle aynı piksel yuvarlaması. Uyuşmazlık tanısı `%LocalAppData%\Koy\crash.log` ("AppBar yeniden iddia") |
| Platform | `src/Platform/Shell/TaskbarService.cs`, `TaskbarService.Watchdog.cs` (gözcüyü explorer ebeveynli başlatır), `TaskbarRunOnce.cs`, `ShellTrayLocator.cs` | Görev çubuğunu gizleme ve geri getirme |
| Core, App | `src/Core/Dock/DockReorderMath.cs` (`Evaluate`, `CommitOffsets`), `src/Core/Dock/Items/DockItemLayout.cs` (`Reorder`, `Rebase`), `src/App/Features/Dock/DockReorderHelper.cs` | Sürükleyerek sıralama ve klasörleme (kapsül, tam genişlik, dikey; ek öğeler varken de): yuva genişlikleri eşit değil, orta bantta (%50) komşu kaymaz ve hedef büyür, kenar bölgelerde kayar; bırakınca düzen (Items) ve PinnedOrder tek yazmada kaydedilir. Görsel kanıt `Koy.UiSnapshots --scene dock-surukle` |
| Core | `src/Core/Dock/DockClickPolicy.cs`, `src/Core/Dock/DockMonitorFilter.cs`, `src/Core/Dock/DockWindowGrouping.cs` | Sol tık eylemi kararı, "yalnız bu monitörün pencereleri" süzgeci (pencere sınırlarının merkezi); `DockWindowGrouping.ShouldGroup` + `SlotLimit` (16): "yer kalmayınca grupla" eşiği, `DockController` kullanır |
| Core | `src/Core/Dock/DockPresentationPolicy.cs`, `src/Core/Dock/DockEntryPlanner.cs` | `DockViewModel.EvaluatePosition` kararları (boşluk, köşe, görünürlük, gizlenirken kayma, kenar çizgisi, fare izleme: `DockPresentationPolicy`) ve `UpdateItems` kararları (hangi simge hangi sırada, klasör üyeleri, yedek model: `DockEntryPlanner`; şerit koleksiyonunu yerinde eşleme işlemleri: `DockListSync`). ViewModel yalnız bağlar |
| App | `src/App/Features/Dock/DockFeature.cs` | Yaşam döngüsü |
| App | `src/App/Features/Dock/DockViewModel.cs`, `src/App/Features/Dock/DockFullWidth.xaml`, `src/App/Features/Dock/DockCapsule.xaml`, `src/App/Features/Settings/Pages/DockPage.xaml` | Görünüm, kenar/uzunluk/boşluk bağlamaları ve ayar sayfası |
| Core, App | `src/Core/Dock/DockPresentationMath.cs`, `src/App/Features/Dock/DockPresentationStyle.cs`, `src/App/Features/Dock/DockItemsStrip.xaml` | Simge göstergesi (nokta, çizgi, arka plan), rozet, kenar açma gecikmesi ve gizli dock çizgisi: karar Core'da, fırça ve köşe eşlemesi App'te |
| Core, App | `src/Core/Dock/Items/` (`DockItemLayout`, `DockCustomCommand`, `DockClockTick`, `DockBatteryPolicy`), `src/App/Features/Dock/DockExtraViewModel.cs`, `DockViewModel.ComposeExtras` | Dock öğeleri (ayırıcı, boşluk, saat, çöp kutusu, özel düğme, pil): düzen ve komut kuralları Core'da, çizim ve çalıştırma App'te. Pil durumu, simge işareti (yıldırım/fiş/yaprak), dolum, renk ve ipucu türü `src/Core/Power/BatteryDisplayPolicy.cs`'te (prizde %100 dahil; `IPowerService` priz, şarj ve pil tasarrufunu ayrı verir); çizim `src/App/Features/Status/BatteryIcon.cs`, ipucu metni `BatteryText.cs`; dock ve çentik aynı kararı kullanır; görsel kanıt `Koy.UiSnapshots --scene pil`. Pil öğesi pili olmayan makinede yer tutmaz; güç servisi (`SharedServices.AcquirePowerService`) yalnız şeritte pil öğesi varken alınır. Eklemek için dock sağ tık menüsü "Öğe ekle" |
| Core, App | `src/Core/Dock/DockSettingsMatrix.cs` (hangi ayar hangi stilde geçerli; tek kaynak), `src/App/Features/Settings/DockScopeConverters.cs`, `SettingsPreview.cs`, `Pages/SubPages/DockBehaviorSubPage.xaml` | Dock ayar sayfaları: seçili stilde geçersiz satırlar GİZLENİR (`StackPanel Visibility` + `ConverterParameter=<DockSettings özellik adı>`; Windows karşılığı türetilmiş satırlar `@FullWidth`). Yeni dock ayarı eklenince matrise de eklenir (test eksik adı yakalar); `TaskbarLayout.ApplyWindowsStyle` ile sabitlenen ayar "yalnız kapsül" olmalıdır. Tam genişlik renkleri `KoyTaskbar*` (sistem temasına uyar, `TaskbarTheme`), kapsül `KoyDock*` sabit koyu. Küçük vektör önizleme `SettingsPreview` (Kind). Görsel kanıt `Koy.UiSnapshots --scene ayar` |
| Core, App | `src/Core/Dock/SystemArea/TaskbarLayout.cs` (tam düzen kararı: hiza, bölüm sırası, pencere öğeleri yeri, `ApplyWindowsStyle`), `DockFullWidth.xaml` (sol/orta bölüm), `DockWeatherViewModel.cs` + `DockWeatherWidget.xaml`, `Styles.xaml` (`DockPillButtonStyle`) | Tam genişlik dock'un sol ve orta bölümü Windows 11 gibi: Başlat (Windows logosu, Win tuşu), Ara (Win+S), Görev görünümü (Win+Tab), 24 px simge / 44 DIP yuva, Windows gösterge dili, büyütme yok, hiza Orta/Sol (mevcut simge hizası ayarı; Sağ = Orta), pencere öğeleri (hava, Win+W). Windows'ta olmayan ayarlar `DockViewModel` içinde sabitlenir (kullanıcı ayarı değişmez). Görsel kanıt `Koy.UiSnapshots --scene taskbar` |
| Core, App | `src/Core/Dock/SystemArea/DockSystemArea.cs` (parça kararı, `HidesExtra`, saat metni), `src/App/Features/Dock/DockSystemAreaControl.xaml(.cs)`, `DockStatusViewModel.cs`, `DockViewModel` (`VisibleTrayIcons`, `SystemArea`) | Tam genişlik dock'un sağ ucu (Windows 11 sistem alanı; yatay kenar): taşma oku (`TrayButton IsSystemArea`), görünür tepsi simgeleri, Wi-Fi + ses + pil hapı (Hızlı ayarlar), saat/tarih, zil, masaüstünü göster şeridi; ayarı yok. Sistem alanı pili ve saati çiziyorsa Pil/Saat ek öğeleri çizilmez. Servisler yalnız tam genişlikte `DockFeature.CreateStatus` ile alınır. Görsel kanıt `Koy.UiSnapshots --scene dock-sistem` |
| Core, Platform, App | `src/Core/StartMenu/`, `src/Platform/StartMenu/StartMenuHost.cs`, `src/App/Features/Dock/DockStartMenu.cs` | Başlat düğmesinin sağ tık (Win+X) menüsü: model ve eylemler Core'da, Windows tarafı Platform'da, çizim App'te |
| Gözcü | `src/Watchdog/` | Koy ölürse görev çubuğunu geri getiren ayrı süreç |

Dock'la ilgili her değişiklikte elle sınama (AGENTS #7):
- [ ] Görev Yöneticisi'nden Koy'u öldür: görev çubuğu birkaç saniyede kendiliğinden geri gelmeli.
- [ ] "Koy'dan çık" ile çık: görev çubuğu eski haline dönmeli.
- [ ] Dock kapalıyken Windows görev çubuğuna hiç dokunulmamalı (AGENTS #12).
- [ ] Tam ekranda ve iki ekranda davranış.

Görev çubuğu dosyalarını değiştirirken dikkat: gözcü `TaskbarService.cs`, `TaskbarRunOnce.cs` ve `ShellTrayLocator.cs` dosyalarını bağlayarak derler.

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
| Kullanıcı stil dosyası (`user-theme.json`) | Core: `src/Core/Appearance/UserStyleOverrides.cs` (izinli anahtarlar `AllowedTokens`, kısıtlı ayrıştırıcı `UserStyleParser`), `UserStyleFile.cs` (okuma, boyut sınırı). App: `ThemeManager.cs` (tema sonrası uygular), `UserStyleResources.cs` (WPF'ye çevirir), `UserStyleWatcher.cs` (yalnız ayarlar açıkken izler), `Pages/GorunumPage.xaml`. Çentik zemini `KoyNotchSurface`'tan, dock zemini `KoyDockSurface` / `KoyDockCapsuleSurface`'tan okunur (`NotchViewModel.UpdateGlassSurface`, `DockViewModel.UpdateMaterialSurfaces`; `ThemeManager.Applied` ile yenilenir). Dock token'ları Theme.Dark/Light.xaml'de: `KoyDockSurface`, `KoyDockCapsuleSurface`, `KoyDockBorder`, `KoyDockCapsuleBorder`, `KoyDockHighlight`, `KoyDockSeparator`, `KoyDockDivider`, `KoyDockHover`, `KoyDockActive`, `KoyDockPill`, `KoyDockCover`, `KoyDockIndicator`, `KoyDockText`, `KoyDockTextSecondary`, `KoyDockGlyph`, `KoyDockClockText`, `KoyDockClockSubtext`, `KoyDockPreviewSurface`, `KoyDockPreviewText`. Tema denemek için: `Koy.UiSnapshots --user-theme store/src/<id>/theme.json`. Yeni renk/ölçü anahtarı eklersen `AllowedTokens`'a da ekle; `UserStyleOverridesTests` XAML ile eşitliği denetler. XAML okuyucu kullanılmaz |
| Marka simgeleri | `assets/branding/`, sonra `python scripts/generate-icons.py` |
| Güncelleme denetimi ve kurma | Kaynak ve tek denetim yolu: `src/App/UpdateClient.cs`. Otomatik denetim (açılış, uykudan dönüş, kilit açma; zamanlayıcı yok): `src/App/AutoUpdater.cs`, ne zaman: `src/Core/Updates/UpdateCheckSchedule.cs`. Durum: `src/App/UpdateStatus.cs`. Pencere ve indirme: `KoyWindowViewModel` (`OpenUpdateOverlay`, `StartUpdateAsync`), görünüm `KoyWindow.xaml` (`UpdateOverlay`), Hakkında kartı `Pages/HakkindaPage.xaml`. Delta paketi: `.github/workflows/release.yml` (önceki sürümün full paketi indirilir, `vpk pack` farkı üretir) |
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
- Müzik sayfası GIF'i: `--scene muzik-kayit --lang tr|en` kareleri, GIF/WebP'ye çeviren betik git dışında (`marketing/tanitim-gif/araclar/make_music.py`).
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
| Core, App | `src/Core/Notch/NotchEdgeLine.cs`, `src/App/Features/Notch/NotchLineWindow.cs` | Üst kenar çizgisi: kalınlık, renk kaynağı, görünürlük kararı Core'da; pencere App'te, `NotchFeature` çentik penceresiyle birlikte kurar ve z-sırasını korur |

## Malzeme (Katı / Buzlu / Cam) ve okunabilirlik koruması
| Katman | Dosya | Ne yapılır |
|---|---|---|
| Core | `src/Core/Appearance/MaterialSetting.cs` | Seviyeler, opaklık, eski "Yarı saydam" çevirisi |
| Core | `src/Core/Appearance/ReadabilityGuard.cs` | Duvar kağıdı ortalamasına göre etkin seviye (ikincil metin ≥ 4,5:1) |
| Platform | `src/Platform/Display/WallpaperSampler.cs` | Duvar kağıdının ortalama rengi (16 px çözüm) |
| App | `src/App/Resources/MaterialState.cs` | Ne zaman okunur, etkin seviye (pencere / çentik / dock), `Changed` olayı |
| App | `src/App/Resources/ThemeManager.cs` | Pencere, satır, segment ve çentik kartı tonları |
| App | `NotchViewModel.SurfaceBrush`, `DockViewModel.FullWidthSurface` / `CapsuleSurface` | Çentik ve dock zemini |
| Core, App | `src/Core/Dock/DockMaterialLook.cs` (`DockMaterialPolicy.For(level, edge, fullWidth)`), `src/App/Features/Dock/DockSurfaceBrushes.cs` | Dock'ta Buzlu/Cam hissi (bulanıklık yok): opaklık tablosu aynı (`LayeredAlpha`), ek olarak ekran merkezine bakan kenarda hafif parlama (gradyan yönü dock kenarını izler) ve iç kenar çizgisi (`KoyDockHighlight`; tam genişlikte kenarlığın kendisi, kapsülde köşeyi izleyen ışıklı kenarlık). Tam genişlikte parlama bir banttır (kalınlığın ~%55'inde söner), çizgi ortada parlak uçlara söner; etkin/hover simge zemini Cam/Buzlu'da köşegen kenarlıklı karodur (`TileActiveFill` / `TileHoverFill` / `TileBorderBrush`, `DockItemsStrip`). Katı'da ipucu yok, eski fırçalar aynen. Fırçalar dondurulur, `MaterialState.Changed` / `ThemeManager.Applied` / kenar değişiminde yenilenir. Görsel kanıt: `Koy.UiSnapshots --scene dockbar` (`dockmat-*` dosyaları, renkli zeminde Katı/Buzlu/Cam, alt + sol kenar) |

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
