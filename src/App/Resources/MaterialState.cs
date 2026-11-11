using System.Windows;
using Koy.Core.Appearance;
using Koy.Core.Settings;
using Koy.Platform.Display;
using Microsoft.Win32;

namespace Koy.App.Resources;

/// <summary>
/// Etkin malzeme: seçilen seviye (Katı / Buzlu / Cam) okunabilirlik korumasından geçer. Duvar kağıdının ortalama rengi
/// yalnız saydam bir malzeme ve koruma açıkken okunur (ilk istekte bir kez, sonra duvar kağıdı değişince); yoklama yok.
/// Okuma bitince ya da duvar kağıdı değişince <see cref="Changed"/> UI iş parçacığında çağrılır: tema ezmeleri,
/// açık çentik ve dock yüzeyi yeniden hesaplanır.
/// </summary>
public static class MaterialState
{
    private static bool _watching;
    private static bool _sampling;
    private static bool _sampled;

    /// <summary>Duvar kağıdının ortalama rengi; okunmadıysa ya da okunamadıysa null (koruma seçileni aynen kullanır).</summary>
    public static Rgb? Wallpaper { get; private set; }

    public static event Action? Changed;

    /// <summary>Koy penceresi, sağ alt menü ve sihirbaz için etkin seviye.</summary>
    public static MaterialLevel ForWindow(AppSettings settings, ResolvedTheme theme) =>
        Resolve(settings, theme == ResolvedTheme.Light ? ReadabilityGuard.WindowLight : ReadabilityGuard.WindowDark);

    /// <summary>Açık çentik ve kartlar için etkin seviye. "Açık çentik malzemeyi izlesin" kapalıysa hep Katı.</summary>
    public static MaterialLevel ForNotch(AppSettings settings) =>
        settings.Notch.FollowsMaterial ? Resolve(settings, ReadabilityGuard.Notch) : MaterialLevel.Solid;

    /// <summary>Dock yüzeyi için etkin seviye (çentikle aynı koyu taban).</summary>
    public static MaterialLevel ForDock(AppSettings settings) => Resolve(settings, ReadabilityGuard.Notch);

    /// <summary>Görüntü aracı ve testler: duvar kağıdını okumadan sabit bir renk ver.</summary>
    public static void SetWallpaperForPreview(Rgb? wallpaper)
    {
        Wallpaper = wallpaper;
        _sampled = true;
        _watching = true;
    }

    private static MaterialLevel Resolve(AppSettings settings, ReadabilityGuard.Surface surface)
    {
        var requested = settings.Appearance.Material;
        if (requested != MaterialLevel.Solid && settings.Appearance.MaterialReadabilityGuard)
            EnsureSampled();
        return ReadabilityGuard.Effective(requested, settings.Appearance.MaterialReadabilityGuard, Wallpaper, surface);
    }

    private static void EnsureSampled()
    {
        if (Application.Current is not { } app)
            return;

        if (!_watching)
        {
            _watching = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category == UserPreferenceCategory.Desktop)
                    app.Dispatcher.BeginInvoke(Sample);
            };
        }

        if (!_sampled)
        {
            _sampled = true;
            app.Dispatcher.BeginInvoke(Sample);
        }
    }

    private static async void Sample()
    {
        if (_sampling)
            return;
        _sampling = true;
        try
        {
            var color = await WallpaperSampler.SampleAsync();
            if (color != Wallpaper)
            {
                Wallpaper = color;
                Changed?.Invoke();
            }
        }
        finally
        {
            _sampling = false;
        }
    }
}
