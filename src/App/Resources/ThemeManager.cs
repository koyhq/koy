using System.Windows;
using System.Windows.Media;
using Koy.Core.Appearance;
using Koy.Core.Settings;
using Microsoft.Win32;

namespace Koy.App.Resources;

/// <summary>
/// Görünüm ayarlarını (tema, vurgu, malzeme, köşe, hareket) uygulama kaynaklarına uygular.
/// Sıra: [0] tema sözlüğü (Koyu/Açık, değişince yenisiyle değiştirilir), Tokens, Icons, Styles, en sonda
/// ezmeler (vurgu tonları, köşe yarıçapları, Buzlu / Cam yüzeyler). Hepsi DynamicResource ile okunduğu için
/// değişiklik açık pencerelere anında yansır.
/// Koy penceresi, ilk kurulum sihirbazı ve sağ alt menü bu kaynakları kullanır; çentik ve dock temadan bağımsız koyu
/// kalır, yalnız malzeme saydamken çentik kartlarının (KoyNotchCard / KoyNotchChip) tonu buradan ezilir.
/// </summary>
public static class ThemeManager
{
    private const string Base = "pack://application:,,,/Koy;component/Resources/";

    private static ResourceDictionary? _themeDictionary;
    private static ResourceDictionary? _overrides;
    private static AppSettings? _last;

    /// <summary>Son uygulanan ayarlar (pencere çerçeveleri okur); henüz uygulanmadıysa null.</summary>
    public static AppSettings? LastSettings => _last;

    /// <summary>Kaynaklar her uygulandığında (tema, vurgu, malzeme, köşe, duvar kağıdı); pencere çerçeveleri dinler.</summary>
    public static event Action? Applied;
    private static bool _watching;
    private static bool _watchingMaterial;

    public static ResolvedTheme Current { get; private set; } = ResolvedTheme.Dark;

    /// <summary>Ortak sözlükleri yükler (App.xaml'in yerini tutar; araçlar da aynı yolu kullanır).</summary>
    public static void Install(Application app)
    {
        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0)
            return;

        merged.Add(Load("Theme.Dark.xaml"));
        _themeDictionary = merged[0];
        merged.Add(Load("Tokens.xaml"));
        merged.Add(Load("Icons.xaml"));
        merged.Add(Load("IconsNotch.xaml"));
        merged.Add(Load("StylesNotch.xaml"));
        merged.Add(Load("Styles.xaml"));
        _overrides = new ResourceDictionary();
        merged.Add(_overrides);
    }

    private static ResourceDictionary Load(string name) => new() { Source = new Uri(Base + name, UriKind.Absolute) };

    /// <summary>Ayarları uygular. "Sistem" seçiliyken Windows uygulama teması izlenir.</summary>
    public static void Apply(Application app, AppSettings settings, bool? systemUsesLightOverride = null)
    {
        Install(app);
        _last = settings;

        var theme = ThemeSetting.Resolve(settings.Appearance.Theme, systemUsesLightOverride ?? SystemUsesLightTheme());
        Current = theme;

        var merged = app.Resources.MergedDictionaries;
        var wanted = Load(theme == ResolvedTheme.Light ? "Theme.Light.xaml" : "Theme.Dark.xaml");
        var index = _themeDictionary is null ? 0 : merged.IndexOf(_themeDictionary);
        if (index < 0)
            index = 0;
        merged[index] = wanted;
        _themeDictionary = wanted;

        BuildOverrides(settings, theme, wanted);
        Motion.Refresh(settings);

        // Duvar kağıdı okununca ya da değişince okunabilirlik koruması yeniden değerlendirilir.
        if (!_watchingMaterial)
        {
            _watchingMaterial = true;
            MaterialState.Changed += () =>
            {
                if (_last is { } s)
                    Apply(app, s);
            };
        }

        if (settings.Appearance.Theme == AppTheme.System)
            WatchSystemTheme(app);

        Applied?.Invoke();
    }

    private static void BuildOverrides(AppSettings settings, ResolvedTheme theme, ResourceDictionary palette)
    {
        var o = _overrides ??= new ResourceDictionary();
        o.Clear();

        ApplyTypography(o, settings.Appearance);

        // Köşeler
        var r = CornerRadii.For(CornerSetting.Resolve(settings.Appearance.CornerRadius));
        o["KoyRadiusWindow"] = new CornerRadius(r.Window);
        o["KoyRadiusWindowTop"] = new CornerRadius(Math.Max(0, r.Window - 1), Math.Max(0, r.Window - 1), 0, 0);
        o["KoyRadiusWindowBottomLeft"] = new CornerRadius(0, 0, 0, Math.Max(0, r.Window - 1));
        o["KoyRadiusRow"] = new CornerRadius(r.Row);
        o["KoyRadiusControl"] = new CornerRadius(r.Control);
        o["KoyRadiusSmall"] = new CornerRadius(r.Small);

        // Vurgu: Windows vurgusu ya da seçilen renk; varsayılan Koy mavisi tema sözlüğünden gelir.
        string? accentHex = settings.Appearance.UseWindowsAccent ? WindowsAccentHex() : settings.Appearance.AccentColor;
        if (!string.IsNullOrWhiteSpace(accentHex))
        {
            var surface = palette["KoyBgRow"] is SolidColorBrush b ? ToHex(b.Color) : "#1F1F23";
            var p = AccentPalette.Derive(accentHex, theme, surface);
            o["KoyAccent"] = Brush(p.Base);
            o["KoyAccentHover"] = Brush(p.Hover);
            o["KoyAccentPressed"] = Brush(p.Pressed);
            o["KoyOnAccent"] = Brush(p.OnAccent);
            o["KoyAccentText"] = Brush(p.Text);
            o["KoyFocusRing"] = Brush(p.Focus);
            var c = (Color)ColorConverter.ConvertFromString(p.Base);
            // Çentik de aynı vurguyu kullanır (Kontrol sekmesi, anahtarlar, kaydırıcılar); çentik zemini siyah kalır.
            o["KoyNotchAccentControl"] = Brush(p.Base);
            o["KoyNotchControlIconBg"] = new SolidColorBrush(Color.FromArgb(0x38, c.R, c.G, c.B));
            o["KoyAccentSubtle"] = new SolidColorBrush(Color.FromArgb(theme == ResolvedTheme.Dark ? (byte)0x26 : (byte)0x22, c.R, c.G, c.B));
        }

        // Saydam malzeme (Buzlu %86, Cam %72; onaylı kaynak CamKoy.dc.html): pencere yüzeyleri seviyenin opaklığında,
        // satırlar duvar kağıdını hafifçe gösteren beyaz katman (koyu %5, açık %62). Okunabilirlik koruması açık duvar
        // kağıdında seviyeyi kendiliğinden yükseltir (MaterialState).
        var windowLevel = MaterialState.ForWindow(settings, theme);
        if (windowLevel != MaterialLevel.Solid)
        {
            // Başlık çubuğu ve kenar menüsü (KoyBgShell) içerikle aynı tonda: demoda kendi zeminleri yok, pencere tek
            // yüzey. Ayrı tonlar akriliğin üstünde iki farklı gri gibi duruyordu (proje sahibi, 2026-11-11).
            var alpha = MaterialSetting.SurfaceAlpha(windowLevel);
            if (palette["KoyBgWindow"] is SolidColorBrush src)
            {
                var surface = Color.FromArgb(alpha, src.Color.R, src.Color.G, src.Color.B);
                o["KoyBgWindow"] = new SolidColorBrush(surface);
                o["KoyBgShell"] = new SolidColorBrush(surface);
            }
            o["KoyBgRow"] = new SolidColorBrush(theme == ResolvedTheme.Dark
                ? Color.FromArgb(0x0D, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x9E, 0xFF, 0xFF, 0xFF));
            // Segment kutusu: opak gri, saydam satırın üstünde delik gibi durmasın (demo --seg-bg). KoyBgControl'e
            // dokunulmaz; üzerine gelme vurgusu da onu kullanır ve koyulaşırsa vurgu tersine dönerdi.
            o["KoyBgSegment"] = new SolidColorBrush(theme == ResolvedTheme.Dark
                ? Color.FromArgb(0x38, 0x00, 0x00, 0x00)
                : Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
        }

        // Açık çentik camlaşınca kartlar opak gri yerine beyaz katman olur (CamCentik.dc.html: kart %6, vurgulu %9);
        // opak zeminde bu değerler bugünkü #141416 / #1F1F23 ile neredeyse aynı tonu verir.
        if (MaterialState.ForNotch(settings) != MaterialLevel.Solid)
        {
            o["KoyNotchCard"] = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
            o["KoyNotchChip"] = new SolidColorBrush(Color.FromArgb(0x17, 0xFF, 0xFF, 0xFF));
            // Müzik kartı palet yokken (Düz nötr) diğer kartlarla aynı cam tonunda; ikincil metin ve çubuklar zaten
            // nötr (Tokens). Albüm rengi ve kapak resmi MusicViewModel fırçalarından gelir.
            o["KoyNotchMusicCard"] = new SolidColorBrush(Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF));
        }
    }

    // Tokens.xaml'deki yazı ölçüleri (taban 13 DIP'te); taban boyu değişince oranla ölçeklenir.
    private static readonly (string Key, double AtDefault)[] FontTokens =
    [
        ("KoyFontPageTitle", 20), ("KoyFontSubtitle", 12.5), ("KoyFontSection", 11.5), ("KoyFontRowTitle", 13.5),
        ("KoyFontBody", 13), ("KoyFontControl", 12.5), ("KoyFontCaption", 12), ("KoyFontKey", 11.5),
    ];

    private static IReadOnlyList<string>? _installedFonts;

    /// <summary>
    /// Yazı tipi, saat yazı tipi, taban boyu ve kalınlık (issue #71). Varsayılanlarda hiçbir şey ezilmez; yüklü yazı tipi
    /// listesi yalnız kullanıcı bir yazı tipi seçtiyse okunur (açılışta gereksiz yazı tipi taraması olmasın).
    /// </summary>
    private static void ApplyTypography(ResourceDictionary o, AppearanceSettings a)
    {
        var size = FontScalePolicy.ClampBaseFontSize(a.BaseFontSize);
        if (Math.Abs(size - FontScalePolicy.DefaultBaseFontSize) > 0.01)
        {
            foreach (var (key, atDefault) in FontTokens)
                o[key] = FontScalePolicy.ScaleFontToken(atDefault, size);
        }

        var weight = FontScalePolicy.ClampFontWeight(a.FontWeight);
        if (weight != FontScalePolicy.DefaultFontWeight)
            o["KoyFontWeightBase"] = FontWeight.FromOpenTypeWeight(weight);

        if (string.IsNullOrWhiteSpace(a.FontFamily) && string.IsNullOrWhiteSpace(a.ClockFontFamily))
            return;

        var installed = InstalledFonts();
        // Boş ya da kaldırılmış yazı tipi: ezme yok, Tokens varsayılanı kalır (fallback boş metin).
        var ui = FontScalePolicy.ResolveFontFamily(a.FontFamily, installed, fallback: "");
        if (ui.Length > 0)
        {
            o["KoyFont"] = new FontFamily(ui);
            o["KoyNotchFont"] = new FontFamily(ui);
        }

        var clock = FontScalePolicy.ResolveClockFontFamily(a.ClockFontFamily, ui, installed);
        if (clock.Length > 0)
            o["KoyClockFont"] = new FontFamily(clock);
    }

    /// <summary>Yüklü yazı tipi adları (sıralı, bir kez okunur). Ayarlar sayfasının listesi de bunu kullanır.</summary>
    public static IReadOnlyList<string> InstalledFonts() =>
        _installedFonts ??= Fonts.SystemFontFamilies.Select(f => f.Source).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase).ToList();

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static string WindowsAccentHex() => ToHex(SystemParameters.WindowGlassColor);

    /// <summary>Windows uygulama teması açık mı (kayıt defteri: AppsUseLightTheme).</summary>
    public static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void WatchSystemTheme(Application app)
    {
        if (_watching)
            return;
        _watching = true;

        // Windows teması değişince (yalnız ayar "Sistem" iken) kaynaklar yeniden uygulanır.
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle))
                return;
            app.Dispatcher.BeginInvoke(() =>
            {
                if (_last is { } s && (s.Appearance.Theme == AppTheme.System || s.Appearance.UseWindowsAccent))
                    Apply(app, s);
            });
        };
    }
}
