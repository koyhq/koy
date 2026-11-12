using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.Notch;
using Koy.Core.Settings;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>
/// Canlı arka planın çentik ömrü boyunca değişmeyen seçimi (ayardan çözülmüş) ve yüzey başına anlık oynatma kararı.
/// Kararların hepsi Core'dadır (<see cref="LiveBackgroundPlaybackPolicy"/>, <see cref="LiveBackgroundResolver"/>);
/// burası yalnız girdileri toplar ve sonucu görünümlere iletir. Ayar değişince çentik yeniden kurulur, bu yüzden seçim sabittir.
/// </summary>
public sealed partial class LiveBackgroundState : ObservableObject
{
    private readonly LiveBackgroundTarget _target;

    public LiveBackgroundSelection Selection { get; }

    /// <summary>Koyu perdenin opaklığı (0-1).</summary>
    public double ScrimOpacity { get; }

    /// <summary>Açık çentiğin zemini için karar.</summary>
    [ObservableProperty]
    private LiveBackgroundPlaybackMode _notchMode = LiveBackgroundPlaybackMode.Off;

    /// <summary>Müzik kartının zemini için karar.</summary>
    [ObservableProperty]
    private LiveBackgroundPlaybackMode _musicCardMode = LiveBackgroundPlaybackMode.Off;

    public LiveBackgroundState(NotchSettings settings)
    {
        _target = settings.LiveBackgroundTarget;
        ScrimOpacity = LiveBackgroundLimits.ScrimOpacity(settings.LiveBackgroundScrimPercent);

        // Özellik kapalıyken dosya sistemine hiç dokunulmaz (varsayılan yol).
        Selection = _target == LiveBackgroundTarget.None
            ? LiveBackgroundSelection.None
            : Resolve(settings);
    }

    private static LiveBackgroundSelection Resolve(NotchSettings s)
    {
        var path = s.LiveBackgroundFilePath;
        long size = 0;
        // Boyut yalnız yol metni doğrulandıktan sonra okunur: UNC / URL yoluna ağ erişimi yapılmaz.
        if (s.LiveBackgroundSource == LiveBackgroundSourceKind.LocalFile
            && LiveBackgroundFileValidator.Validate(path, 1) == LiveBackgroundFileError.None)
        {
            size = FileLength(path);
        }

        return LiveBackgroundResolver.Resolve(s.LiveBackgroundTarget, s.LiveBackgroundSource, s.LiveBackgroundPreset, path, size);
    }

    /// <summary>Dosya boyutu; yoksa ya da okunamıyorsa 0 (doğrulayıcı bunu "boş dosya" sayar).</summary>
    internal static long FileLength(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists ? info.Length : 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Durum değişince (çentik açıldı/kapandı, görünürlük, pil) çağrılır. Hareketi azalt = Koy'un hareket çarpanı 0
    /// (Windows "animasyonları azalt", Animasyon: Kapalı ya da pil tasarrufu); pil tasarrufu = pille çalışırken
    /// "pil tasarrufunda animasyonları azalt" açık.
    /// </summary>
    public void Update(bool isNotchExpanded, bool isWindowVisible, bool isBatterySaver)
    {
        if (Selection.Kind == LiveBackgroundContentKind.None)
        {
            NotchMode = MusicCardMode = LiveBackgroundPlaybackMode.Off;
            return;
        }

        var reduceMotion = !Motion.Enabled;
        NotchMode = LiveBackgroundPlaybackPolicy.Decide(
            isNotchExpanded, isWindowVisible, reduceMotion, isBatterySaver, _target, LiveBackgroundSurface.Notch);
        MusicCardMode = LiveBackgroundPlaybackPolicy.Decide(
            isNotchExpanded, isWindowVisible, reduceMotion, isBatterySaver, _target, LiveBackgroundSurface.MusicCard);
    }
}
