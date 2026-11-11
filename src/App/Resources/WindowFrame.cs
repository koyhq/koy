using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.Core.Appearance;
using Koy.Core.Settings;
using Koy.Platform.Shell;

namespace Koy.App.Resources;

/// <summary>
/// Koy'un kendi pencereleri (Koy penceresi, sağ alt menü, kurulum sihirbazı) için Windows 11 çerçevesi. Pencere katmansız
/// olmalı (AllowsTransparency = false) ve XAML'de <c>WindowChrome</c> ile çerçeveyi istemci alanına uzatmalı
/// (GlassFrameThickness -1); bulanıklık, koyu / açık çerçeve, gölge ve köşe yuvarlaması DWM'den gelir. Buzlu ve Cam'da
/// sistem akriliği istenir (gerçek bulanıklık), Katı'da arka plan yok. Katmanlı pencerede DWM bulanıklık uygulamıyordu
/// ve bulanıksız saydamlık arkadaki pencereleri çamur griye karıştırıyordu (proje sahibi kararı 2026-11-11).
/// Tema, malzeme, köşe ayarı ya da duvar kağıdı değişince bağlı pencerelerin çerçevesi kendiliğinden yenilenir.
/// </summary>
public static class WindowFrame
{
    /// <summary>Pencereyi bağlar; kaynak kurulunca çerçeveyi uygular, pencere kapanınca bırakır.</summary>
    public static void Attach(Window window)
    {
        if (new WindowInteropHelper(window).Handle != 0)
            Initialize(window);
        else
            window.SourceInitialized += (_, _) => Initialize(window);
    }

    private static void Initialize(Window window)
    {
        // Katmansız pencerede WPF yüzeyi siyahla temizler; DWM arka planı görünsün diye saydam temizlenir.
        if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget is not null)
            source.CompositionTarget.BackgroundColor = Colors.Transparent;

        void Refresh() => Apply(window);
        Refresh();
        ThemeManager.Applied += Refresh;
        MaterialState.Changed += Refresh;
        window.Closed += (_, _) =>
        {
            ThemeManager.Applied -= Refresh;
            MaterialState.Changed -= Refresh;
        };
    }

    private static void Apply(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0)
            return;

        var settings = ThemeManager.LastSettings ?? new AppSettings();
        var level = MaterialState.ForWindow(settings, ThemeManager.Current);
        var sharp = CornerSetting.Resolve(settings.CornerRadius) == CornerStyle.Sharp;
        BackdropService.ApplyWindowFrame(
            hwnd,
            level == MaterialLevel.Solid ? WindowBackdropType.None : WindowBackdropType.Acrylic,
            ThemeManager.Current == ResolvedTheme.Dark,
            sharp ? WindowCornerStyle.RoundSmall : WindowCornerStyle.Round);
    }
}
