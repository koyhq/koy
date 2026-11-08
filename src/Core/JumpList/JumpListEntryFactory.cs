namespace Koy.Core.JumpList;

/// <summary>
/// Platformun okuduğu ham alanlardan menü girdisi üretir. Kural: başlık uydurulmaz. Başlığı ve hedefi boş olan
/// (ya da kendini ayırıcı işaretleyen) öğe ayırıcıdır; başlığı boş ama hedefi olan öğe (ör. Spotify) atlanır.
/// </summary>
public static class JumpListEntryFactory
{
    /// <summary>
    /// Görev / kategori kısayolu için girdi. Atlanacaksa null döner.
    /// </summary>
    public static JumpListEntry? FromLink(string? title, string? target, string? arguments, string? workingDirectory, bool markedSeparator = false)
    {
        var cleanTitle = JumpListText.Sanitize(title, JumpListLimits.MaxTitleChars);
        var cleanTarget = target?.Trim();

        if (markedSeparator || (cleanTitle.Length == 0 && string.IsNullOrEmpty(cleanTarget)))
            return JumpListEntry.Separator;

        // Başlık yoksa uydurmak yerine atla; hedef yoksa çalıştırılacak bir şey yoktur.
        if (cleanTitle.Length == 0 || string.IsNullOrEmpty(cleanTarget))
            return null;

        if (cleanTarget.Length > JumpListLimits.MaxTargetChars || (arguments?.Length ?? 0) > JumpListLimits.MaxArgumentsChars)
            return null;

        var args = string.IsNullOrWhiteSpace(arguments) ? null : arguments;
        var dir = string.IsNullOrWhiteSpace(workingDirectory) ? null : workingDirectory.Trim();
        return new JumpListEntry(cleanTitle, new JumpListLaunch(cleanTarget, args, dir, IsDocument: false));
    }

    /// <summary>Son / sık kullanılan belge girdisi (kabuk öğesi). Başlık ya da hedef yoksa null.</summary>
    public static JumpListEntry? FromDocument(string? title, string? target)
    {
        var cleanTitle = JumpListText.Sanitize(title, JumpListLimits.MaxTitleChars);
        var cleanTarget = target?.Trim();
        if (cleanTitle.Length == 0 || string.IsNullOrEmpty(cleanTarget) || cleanTarget.Length > JumpListLimits.MaxTargetChars)
            return null;

        return new JumpListEntry(cleanTitle, new JumpListLaunch(cleanTarget, IsDocument: true));
    }
}

/// <summary>Güvenilmeyen metni menüye uygun hâle getirir.</summary>
public static class JumpListText
{
    /// <summary>
    /// Denetim karakterlerini (satır sonu, sekme, NUL…) boşluğa çevirir, kenar boşluklarını ve art arda boşlukları
    /// atar, <paramref name="maxChars"/> üstünü "…" ile keser. Boş ya da null girdi boş dize verir.
    /// </summary>
    public static string Sanitize(string? text, int maxChars)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var buffer = new System.Text.StringBuilder(Math.Min(text.Length, maxChars + 1));
        var lastWasSpace = true; // baştaki boşlukları atar
        foreach (var ch in text)
        {
            var c = char.IsControl(ch) || char.IsWhiteSpace(ch) ? ' ' : ch;
            if (c == ' ')
            {
                if (lastWasSpace)
                    continue;
                lastWasSpace = true;
            }
            else
            {
                lastWasSpace = false;
            }

            buffer.Append(c);
            if (buffer.Length > maxChars + 1)
                break;
        }

        while (buffer.Length > 0 && buffer[^1] == ' ')
            buffer.Length--;

        if (buffer.Length <= maxChars)
            return buffer.ToString();

        // Vekil çiftini ortadan bölme.
        var cut = maxChars - 1;
        if (cut > 0 && char.IsHighSurrogate(buffer[cut - 1]))
            cut--;
        return buffer.ToString(0, Math.Max(cut, 0)).TrimEnd() + "…";
    }

    /// <summary><c>@dll,-id</c> ya da <c>@{ms-resource…}</c> gibi dolaylı (kaynak) dize mi.</summary>
    public static bool IsIndirect(string? text) => text is { Length: > 1 } && text[0] == '@';
}
