using System.Globalization;
using Koy.App.Resources;
using Koy.Core.Notch;

namespace Koy.App.Features.Notch.LiveBackground;

/// <summary>Core doğrulama sonucunu kullanıcıya gösterilen metne çevirir (karar Core'da, burada yalnız metin).</summary>
public static class LiveBackgroundMessages
{
    /// <summary>Sorun yoksa boş metin.</summary>
    public static string For(LiveBackgroundFileError error) => error switch
    {
        LiveBackgroundFileError.None => "",
        LiveBackgroundFileError.MissingPath => Strings.Get("LiveBgErrorMissing"),
        LiveBackgroundFileError.UnsupportedExtension => Strings.Get("LiveBgErrorExtension"),
        LiveBackgroundFileError.NotLocalPath => Strings.Get("LiveBgErrorNotLocal"),
        LiveBackgroundFileError.Empty => Strings.Get("LiveBgErrorEmpty"),
        LiveBackgroundFileError.TooLarge => string.Format(CultureInfo.CurrentCulture, Strings.Get("LiveBgErrorTooLarge"), LiveBackgroundLimits.MaxFileSizeBytes / (1024 * 1024)),
        _ => Strings.Get("LiveBgErrorInvalid"),
    };
}

/// <summary>Önayar seçici satırı.</summary>
public sealed record LiveBackgroundPresetOption(string Id, string Name);
