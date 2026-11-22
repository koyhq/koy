using System.Text;

namespace Koy.Core.Dictation;

/// <summary>Bir kayıt için seçilen profil: kayıt başladığı an alınan anlık görüntü, işle birlikte taşınır.</summary>
public sealed record ProfileChoice(WritingProfile Profile, bool IsDefault)
{
    public static ProfileChoice Default(WritingProfile? profile = null) => new(profile ?? new WritingProfile(), true);
}

/// <summary>
/// Hangi yazım profili kullanılacak: elle seçim her şeyin üstündedir; yoksa önde olan program, yoksa tarayıcı sekmesindeki site;
/// hiçbiri tutmazsa varsayılan. Programı sistem söylemezse varsayılan kullanılır: yazı, nereye gideceğini bilmediği için
/// başarısız olmaz.
/// </summary>
public static class ProfileSelector
{
    private static readonly HashSet<string> Browsers = new(StringComparer.Ordinal)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc", "zen", "librewolf", "waterfox",
    };

    /// <summary>"Code.EXE" → "code".</summary>
    public static string ProgramKey(string? program)
    {
        var name = (program ?? "").Trim().Trim('"');
        var slash = name.LastIndexOfAny(['\\', '/']);
        if (slash >= 0)
            name = name[(slash + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        return name.ToLowerInvariant();
    }

    public static bool IsBrowser(string? program) => Browsers.Contains(ProgramKey(program));

    public static ProfileChoice Choose(DictationConfig config, string? program, string? windowTitle)
    {
        ArgumentNullException.ThrowIfNull(config);

        var overridden = ByName(config, config.ProfileOverride);
        if (overridden is not null)
            return new ProfileChoice(overridden, false);

        if (!config.ProfileByApp || config.Profiles.Count == 0)
            return ProfileChoice.Default(config.DefaultProfile);

        var key = ProgramKey(program);
        if (key.Length > 0)
        {
            var byProgram = config.Profiles.FirstOrDefault(p => p.Programs.Any(x => ProgramKey(x) == key));
            if (byProgram is not null)
                return new ProfileChoice(byProgram, false);

            if (IsBrowser(key) && !string.IsNullOrWhiteSpace(windowTitle))
            {
                var bySite = config.Profiles.FirstOrDefault(p => p.Sites.Any(site => TitleHasSite(windowTitle, site)));
                if (bySite is not null)
                    return new ProfileChoice(bySite, false);
            }
        }

        return ProfileChoice.Default(config.DefaultProfile);
    }

    /// <summary>Kimlik, sonra ad (büyük/küçük harf duyarsız); boş ya da bulunamazsa null (varsayılan).</summary>
    public static WritingProfile? ByName(DictationConfig config, string? idOrName)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (string.IsNullOrWhiteSpace(idOrName))
            return null;

        var wanted = idOrName.Trim();
        return config.Profiles.FirstOrDefault(p => string.Equals(p.Id, wanted, StringComparison.Ordinal))
            ?? config.Profiles.FirstOrDefault(p => string.Equals(p.Name, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Anahtar sözcük başlıkta <b>tam sözcük</b> olarak geçiyor mu ("Gmail" "Gmailer"da geçmez).</summary>
    public static bool TitleHasSite(string title, string keyword)
    {
        var site = HallucinationFilter.Fold(keyword);
        if (site.Length == 0)
            return false;

        var haystack = new StringBuilder(" ").Append(HallucinationFilter.Fold(title)).Append(' ').ToString();
        return haystack.Contains(" " + site + " ", StringComparison.Ordinal);
    }
}
