// Sitenin iki dili. Modül değil, düz betik: sayfa çizilmeden çalışsın, Türkçe ziyaretçi İngilizce metin görüp sıçramasın.
// index.html içindeki İngilizce, JS kapalıyken ve arama motorlarında görünen metindir; onu değiştirirsen burayı da değiştir.
// Dil sırası: ?lang= > önceki seçim (localStorage) > tarayıcı dili > İngilizce.
(function () {
  const STORAGE_KEY = 'koy-lang';
  const LANGS = ['en', 'tr'];

  const dict = {
    en: {
      'meta.title': 'Koy: notch and dock for Windows 10 / 11',
      'meta.description': 'Koy is a spring-animated notch and a dock that replaces the taskbar on Windows 10 / 11. One process, no browser engine, easy on the CPU at idle. Free and open source.',
      'meta.ogDescription': 'A spring-animated notch and a dock that replaces the taskbar. No browser engine, free and open source.',
      'meta.ogImageAlt': 'Koy: a Turkish tagline on a blue background',
      'meta.locale': 'en_US',

      'skip': 'Skip to content',
      'brand.home': 'Koy, home',
      'nav.sections': 'Sections',
      'nav.notch': 'Notch',
      'nav.dock': 'Dock',
      'nav.install': 'Install',
      'nav.download': 'Download',
      'nav.language': 'Language',

      'hero.openAlt': 'The open Koy notch: now playing, headphone battery, CPU and memory, network speed and quick launch',
      'hero.zone': 'Open or close the notch',
      'hero.hint': 'Point at the top edge',
      'hero.hintTouch': 'Tap the notch',
      'badge.notes': 'See the release notes',
      'hero.line1': 'Your desktop,',
      'hero.line2': 'a lighter touch.',
      'hero.sub': 'A spring-animated notch and a dock that replaces the taskbar. One process, no browser engine, easy on the CPU at idle.',
      'cta.download': 'Download Koy',
      'cta.source': 'Source code',
      'hero.facts': 'Windows 10 / 11 · Free and open source (GPLv3)',

      'notch.title': 'Put the top of your screen to work.',
      'notch.lead': 'Move the mouse to the top edge and the notch springs open. Music, calendar, Wi-Fi and system status, all in one place.',
      'notch.tablist': 'Notch pages',
      'tab.home': 'Home',
      'tab.calendar': 'Calendar',
      'tab.control': 'Control Center',
      'tab.wifi': 'Wi-Fi',
      'tab.closed': 'When closed',
      'panel.home': 'Now playing, headphone battery, CPU and memory, network speed and quick launch at a glance. Music comes from Spotify, your browser or Windows Media Player.',
      'panel.calendar': 'Month, week and day views, a timer, stopwatch and Pomodoro. Events stay on your computer. When time is up, the notch shakes to let you know.',
      'panel.control': 'Wi-Fi, Bluetooth, Do Not Disturb, volume and brightness. Drag the tiles into any order you like.',
      'panel.wifi': 'Pick a network from the notch, just like in Windows. Connect to Bluetooth devices here too.',
      'panel.closed': 'When closed, the notch stays small: cover art and a waveform if music is playing, otherwise the clock and battery. A brief indicator appears when volume or brightness changes.',
      'panel.note': 'There are also Focus, Shelf (drag and drop files and text), Clipboard and System pages.',
      'shot.home': 'Notch Home page: music, headphones, system and quick launch',
      'shot.calendar': 'Notch Calendar page: month view, timer and events',
      'shot.control': 'Control Center: Wi-Fi, Bluetooth, volume and brightness',
      'shot.wifi': 'Wi-Fi network list in the notch',
      'shot.closed': 'Closed notch: cover art and waveform while music plays',

      'dock.title': "Replaces the taskbar. Doesn't copy it.",
      'dock.lead': 'A floating capsule or a full-width bar. The Windows taskbar hides and the dock takes over.',
      'dock.f1': '<strong>Reacts to your windows.</strong> Reserve space, dock down, dodge or stay hidden. It never appears in fullscreen or games.',
      'dock.f2': '<strong>Previews over icons.</strong> Hover to see a live thumbnail of an open window.',
      'dock.f3': '<strong>System tray on the dock.</strong> Tray icons and menus of apps like Discord and Steam work from the dock.',
      'dock.f4': '<strong>Drag to reorder.</strong> The order of pinned apps is yours and survives restarts.',
      'demo.openAlt': 'The opened notch: music, headphone battery, system and quick launch',
      'demo.dockAlt': 'The Koy dock at the bottom of the screen: pinned apps and open windows',
      'demo.hint': 'Move the mouse over the notch and it springs open. On touch screens, tap it.',

      'light.title': "No browser. That's why it's light.",
      'light.lead': 'Competitors run on web engines. Koy is native C# and WPF, and keeps itself honest by measuring.',
      'stat.browser': 'browser engines. No WebView2, Electron or Tauri.',
      'stat.process': 'process. Notch, dock and settings share one process; only a small watchdog runs alongside while the dock is on.',
      'stat.cpuValue': '~0%',
      'stat.cpu': 'idle CPU. Animations draw frames only while something moves, and hidden panels run nothing.',
      'stat.memory': 'memory target. 99–100 MB measured with two monitors on v0.1.1. <a href="https://github.com/koyhq/koy/blob/main/docs/MEMORY.md">Measurement log</a>',

      'details.title': 'We sweated the small stuff.',
      'details.multi.title': 'Multiple displays',
      'details.multi.text': "A separate notch and dock on each monitor, each at its own DPI. Fullscreen video on a side display doesn't hide Koy on the main one.",
      'details.taskbar.title': 'Taskbar guarantee',
      'details.taskbar.text': 'Even if Koy closes or crashes, the Windows taskbar comes back within seconds.',
      'details.shortcuts.title': 'Shortcuts',
      'details.shortcuts.text': 'All global shortcuts can be customized. If two shortcuts clash, Koy tells you.',
      'details.theme.title': 'Dark and light themes',
      'details.theme.text': 'Follows the Windows theme, or pick one yourself. The accent color is yours too.',
      'details.lang.title': 'English and Turkish',
      'details.lang.text': 'The language changes instantly, without restarting the app.',
      'details.uninstall.title': 'Clean uninstall',
      'details.uninstall.text': 'Uninstall from inside Koy in one step. Keep your settings if you like; your taskbar goes back to normal.',

      'install.title': 'Setup takes a minute.',
      'install.lead': 'Requires Windows 10 (2004 / 19041 or later) or Windows 11. Koy is free, and every release is built on GitHub Actions after the tests pass.',
      'install.s1.title': 'Download',
      'install.s1.text': "Setup installs in one click and updates itself. Prefer no install? There's a portable zip.",
      'install.s1.zip': 'Portable zip',
      'install.s2.title': 'If SmartScreen warns you',
      'install.s2.text': "Koy isn't code-signed yet. If you downloaded the file only from GitHub Releases, choose <em>More info</em>, then <em>Run anyway</em>. Don't run a Koy file that came from anywhere else.",
      'install.s3.title': 'Verify it if you like',
      'install.s3.text': 'Compare the resulting hash with <a href="https://github.com/koyhq/koy/releases/latest/download/SHA256SUMS.txt">SHA256SUMS.txt</a> in the release.',
      'copy.aria': 'Copy command',
      'copy.label': 'Copy',
      'copy.done': 'Copied',
      'copy.selected': 'Selected, press Ctrl+C',

      'footer.title': 'Try it in a minute.',
      'footer.lead': "Free, open source, for Windows 10 / 11. Don't like it? Uninstall in one step.",
      'footer.note': 'An open source project released under GPLv3.',
      'footer.links': 'Links',
      'footer.releases': 'Releases',
      'footer.changelog': 'Changelog',
      'footer.bug': 'Report a bug',
      'footer.discussions': 'Discussions',
      'footer.security': 'Security',
    },

    tr: {
      'meta.title': 'Koy: Windows 10 / 11 için çentik ve dock',
      'meta.description': 'Koy, Windows 10 / 11 için yay fiziğiyle açılan bir çentik ve görev çubuğunun yerine geçen bir dock. Tek süreç, tarayıcı motoru yok, boşta işlemciyi yormaz. Ücretsiz ve açık kaynak.',
      'meta.ogDescription': 'Yay fiziğiyle açılan çentik, görev çubuğunun yerine geçen dock. Tarayıcı motoru yok, ücretsiz ve açık kaynak.',
      'meta.ogImageAlt': "Koy: mavi zeminde 'Masaüstünü hafifçe değiştir.' yazısı",
      'meta.locale': 'tr_TR',

      'skip': 'İçeriğe geç',
      'brand.home': 'Koy, ana sayfa',
      'nav.sections': 'Bölümler',
      'nav.notch': 'Çentik',
      'nav.dock': 'Dock',
      'nav.install': 'Kurulum',
      'nav.download': 'İndir',
      'nav.language': 'Dil',

      'hero.openAlt': 'Açık Koy çentiği: çalan müzik, kulaklık pili, işlemci ve bellek, ağ hızı ve hızlı başlat',
      'hero.zone': 'Çentiği aç ya da kapat',
      'hero.hint': 'Fareyi üst kenara götür',
      'hero.hintTouch': 'Çentiğe dokun',
      'badge.notes': 'Sürüm notlarına bak',
      'hero.line1': 'Masaüstünü',
      'hero.line2': 'hafifçe değiştir.',
      'hero.sub': 'Yay fiziğiyle açılan bir çentik ve görev çubuğunun yerine geçen bir dock. Tek süreç, tarayıcı motoru yok, boşta işlemciyi yormaz.',
      'cta.download': "Koy'u indir",
      'cta.source': 'Kaynak kodu',
      'hero.facts': 'Windows 10 / 11 · Ücretsiz ve açık kaynak (GPLv3)',

      'notch.title': 'Ekranın üstü boşa durmasın.',
      'notch.lead': 'Fareyi üst kenara götür, çentik yayla açılsın. Müzik, takvim, Wi-Fi ve sistem durumu hep aynı yerde.',
      'notch.tablist': 'Çentik sayfaları',
      'tab.home': 'Ana',
      'tab.calendar': 'Takvim',
      'tab.control': 'Kontrol Merkezi',
      'tab.wifi': 'Wi-Fi',
      'tab.closed': 'Kapalıyken',
      'panel.home': "Çalan müzik, kulaklık pili, işlemci ve bellek, ağ hızı ve hızlı başlat tek bakışta. Müzik Spotify'dan, tarayıcıdan ya da Windows Medya Oynatıcı'dan gelir.",
      'panel.calendar': 'Ay, hafta ve gün görünümü, zamanlayıcı, kronometre ve Pomodoro. Etkinlikler bilgisayarında kalır. Süre bitince çentik titreyerek haber verir.',
      'panel.control': 'Wi-Fi, Bluetooth, Rahatsız Etme, ses ve parlaklık. Döşemeleri sürükleyerek istediğin sıraya koy.',
      'panel.wifi': "Ağları çentikten seç, Windows'taki gibi. Bluetooth cihazlarına da buradan bağlan.",
      'panel.closed': 'Kapalıyken çentik küçük kalır: müzik çalıyorsa kapak ve dalga, çalmıyorsa saat ve pil. Ses ya da parlaklık değişince kısa bir gösterge çıkar.',
      'panel.note': 'Ayrıca Odak, Raf (dosya ve metin sürükle-bırak), Pano ve Sistem sayfaları var.',
      'shot.home': 'Çentik Ana sayfası: müzik, kulaklık, sistem ve hızlı başlat',
      'shot.calendar': 'Çentik Takvim sayfası: ay görünümü, zamanlayıcı ve etkinlikler',
      'shot.control': 'Kontrol Merkezi: Wi-Fi, Bluetooth, ses ve parlaklık',
      'shot.wifi': 'Çentikte Wi-Fi ağ listesi',
      'shot.closed': 'Kapalı çentik: müzik çalarken kapak ve dalga',

      'dock.title': 'Görev çubuğunun yerine. Aynısı değil.',
      'dock.lead': 'Yüzen bir kapsül ya da ekran genişliğinde bir çubuk. Windows görev çubuğu gizlenir, işi dock devralır.',
      'dock.f1': '<strong>Pencereye göre davranır.</strong> Yer ayır, aşağı yapış, çekil ya da hep gizli. Tam ekranda ve oyunda asla çıkmaz.',
      'dock.f2': '<strong>Simgenin üstünde önizleme.</strong> Fareyle üzerine gel, açık pencerenin canlı küçük görüntüsünü gör.',
      'dock.f3': "<strong>Sistem tepsisi dock'ta.</strong> Discord, Steam gibi uygulamaların tepsi simgeleri ve menüleri dock'tan çalışır.",
      'dock.f4': '<strong>Sürükleyerek sırala.</strong> Sabitli uygulamaların sırası senin düzenin, yeniden başlatınca korunur.',
      'demo.openAlt': 'Açılmış çentik: müzik, kulaklık pili, sistem ve hızlı başlat',
      'demo.dockAlt': "Ekranın altında Koy dock'u: sabitli uygulamalar ve açık pencereler",
      'demo.hint': 'Fareyi çentiğin üstüne götür, yayla açılsın. Dokunmatikte dokun.',

      'light.title': 'Tarayıcı yok. Bu yüzden hafif.',
      'light.lead': 'Rakipler web motoruyla çalışıyor. Koy doğrudan yerel C# ve WPF, ve bunu ölçerek tutuyor.',
      'stat.browser': 'tarayıcı motoru. WebView2, Electron ya da Tauri yok.',
      'stat.process': 'süreç. Çentik, dock ve ayarlar aynı süreçte; dock açıkken yalnız küçük bir gözcü eşlik eder.',
      'stat.cpuValue': '~%0',
      'stat.cpu': 'boşta işlemci. Animasyon yalnız hareket varken kare çizer, görünmeyen panel hiçbir şey çalıştırmaz.',
      'stat.memory': "bellek hedefi. v0.1.1'de iki monitörle ölçülen 99–100 MB. <a href=\"https://github.com/koyhq/koy/blob/main/docs/MEMORY.md\">Ölçüm kayıtları</a>",

      'details.title': 'Küçük şeyler hesaba katıldı.',
      'details.multi.title': 'Çoklu ekran',
      'details.multi.text': "Her monitörde ayrı çentik ve dock, her biri kendi DPI'ında. Yan ekranda tam ekran video, ana ekrandaki Koy'u gizlemez.",
      'details.taskbar.title': 'Görev çubuğu güvencesi',
      'details.taskbar.text': 'Koy kapansa ya da çökse bile Windows görev çubuğu birkaç saniyede geri gelir.',
      'details.shortcuts.title': 'Kısayollar',
      'details.shortcuts.text': 'Genel kısayolların hepsi özelleştirilebilir. İki kısayol çakışırsa Koy söyler.',
      'details.theme.title': 'Koyu ve açık tema',
      'details.theme.text': 'Windows temasını izler ya da elle seçersin. Vurgu rengi de senin.',
      'details.lang.title': 'Türkçe ve İngilizce',
      'details.lang.text': 'Dil, uygulamayı yeniden başlatmadan anında değişir.',
      'details.uninstall.title': 'Temiz kaldırma',
      'details.uninstall.text': "Koy'dan tek adımda kaldırılır. İstersen ayarların korunur, görev çubuğun yerine döner.",

      'install.title': 'Kurulum bir dakika.',
      'install.lead': "Windows 10 (2004 / 19041 ve üstü) veya Windows 11 gerekir. Koy ücretsizdir ve her sürüm GitHub Actions'ta, testler geçtikten sonra üretilir.",
      'install.s1.title': 'İndir',
      'install.s1.text': 'Setup tek tıkla kurar ve kendini günceller. Kurulum istemezsen taşınabilir zip var.',
      'install.s1.zip': 'Taşınabilir zip',
      'install.s2.title': 'SmartScreen uyarırsa',
      'install.s2.text': "Koy henüz kod imzalı değil. Dosyayı yalnız GitHub Releases'tan indirdiysen <em>Ek bilgi</em>, sonra <em>Yine de çalıştır</em> de. Başka bir yerden gelen Koy dosyasını çalıştırma.",
      'install.s3.title': 'İstersen doğrula',
      'install.s3.text': 'Çıkan özeti sürümdeki <a href="https://github.com/koyhq/koy/releases/latest/download/SHA256SUMS.txt">SHA256SUMS.txt</a> ile karşılaştır.',
      'copy.aria': 'Komutu kopyala',
      'copy.label': 'Kopyala',
      'copy.done': 'Kopyalandı',
      'copy.selected': 'Seçildi, Ctrl+C',

      'footer.title': 'Denemesi bir dakika.',
      'footer.lead': 'Ücretsiz, açık kaynak, Windows 10 / 11 için. Beğenmezsen tek adımda kaldırırsın.',
      'footer.note': 'GPLv3 ile yayımlanan açık kaynak bir proje.',
      'footer.links': 'Bağlantılar',
      'footer.releases': 'Sürümler',
      'footer.changelog': 'Değişiklik günlüğü',
      'footer.bug': 'Hata bildir',
      'footer.discussions': 'Tartışmalar',
      'footer.security': 'Güvenlik',
    },
  };

  let current = 'en';

  function readStored() {
    try { return localStorage.getItem(STORAGE_KEY); } catch { return null; }
  }

  function writeStored(code) {
    try { localStorage.setItem(STORAGE_KEY, code); } catch { /* özel pencere */ }
  }

  function pick() {
    const fromUrl = new URLSearchParams(location.search).get('lang');
    if (LANGS.includes(fromUrl)) return fromUrl;
    const stored = readStored();
    if (LANGS.includes(stored)) return stored;
    return (navigator.language || '').toLowerCase().startsWith('tr') ? 'tr' : 'en';
  }

  function t(key) {
    return dict[current][key] ?? dict.en[key] ?? key;
  }

  function setMeta(selector, key) {
    const el = document.head.querySelector(selector);
    if (el) el.setAttribute('content', t(key));
  }

  function apply(code) {
    current = code;
    document.documentElement.lang = code;
    document.title = t('meta.title');
    setMeta('meta[name="description"]', 'meta.description');
    setMeta('meta[property="og:title"]', 'meta.title');
    setMeta('meta[property="og:description"]', 'meta.ogDescription');
    setMeta('meta[property="og:image:alt"]', 'meta.ogImageAlt');
    setMeta('meta[property="og:locale"]', 'meta.locale');
    setMeta('meta[name="twitter:title"]', 'meta.title');
    setMeta('meta[name="twitter:description"]', 'meta.ogDescription');

    // Sözlükte olmayan anahtar sayfadaki metni bozmaz (eski önbellekle yeni sayfa karışırsa).
    document.querySelectorAll('[data-i18n]').forEach((el) => {
      const key = el.dataset.i18n;
      if (key in dict[current] || key in dict.en) el.textContent = t(key);
    });
    // Yalnız yukarıdaki sözlükten gelir, dışarıdan metin girmez.
    document.querySelectorAll('[data-i18n-html]').forEach((el) => { el.innerHTML = t(el.dataset.i18nHtml); });
    document.querySelectorAll('[data-i18n-attr]').forEach((el) => {
      for (const pair of el.dataset.i18nAttr.split(',')) {
        const [attr, key] = pair.split(':');
        el.setAttribute(attr.trim(), t(key.trim()));
      }
    });
    document.querySelectorAll('[data-lang]').forEach((btn) => {
      btn.setAttribute('aria-pressed', String(btn.dataset.lang === code));
    });
  }

  // Elle seçim adresteki ?lang= değerini siler; yoksa yenileyince eski dile dönerdi.
  function choose(code) {
    writeStored(code);
    const url = new URL(location.href);
    if (url.searchParams.has('lang')) {
      url.searchParams.delete('lang');
      history.replaceState(null, '', url);
    }
    apply(code);
  }

  document.querySelectorAll('[data-lang]').forEach((btn) => {
    btn.addEventListener('click', () => choose(btn.dataset.lang));
  });

  window.KoyI18n = { t };
  apply(pick());
})();
