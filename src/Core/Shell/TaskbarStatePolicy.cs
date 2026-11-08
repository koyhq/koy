using System.Globalization;
using System.Text;

namespace Koy.Core.Shell;

/// <summary>Diske yazılan görev çubuğu durumu: gizlendi mi ve gizlemeden önceki ABS_* bayrakları.</summary>
public readonly record struct TaskbarSavedState(bool Hidden, int PreviousState);

/// <summary>
/// Görev çubuğunu gizleme/geri getirme kararları (saf mantık, Win32 yok).
/// Amaç: Hide() ikinci kez çağrılsa bile kullanıcının gerçek (önceki) durumu, bizim yazdığımız AUTOHIDE ile ezilmesin.
/// </summary>
public static class TaskbarStatePolicy
{
    public const int AutoHide = 0x1;
    public const int AlwaysOnTop = 0x2;

    /// <summary>
    /// Gizlemeden önceki durumu seçer. Zaten gizliyken (durum dosyası "hidden") görev çubuğunun
    /// o anki durumu bizim koyduğumuz AUTOHIDE'dır; onu "önceki durum" sayma, kayıtlıyı koru.
    /// </summary>
    public static int ChoosePreviousState(TaskbarSavedState? saved, int currentState)
        => saved is { Hidden: true } s ? s.PreviousState : currentState;

    /// <summary>Geri getirirken uygulanacak ABS_* değeri. Kayıt yoksa ya da geçersizse AlwaysOnTop (güvenli varsayılan).</summary>
    public static int ResolveRestoreState(TaskbarSavedState? saved)
        => saved is { PreviousState: > 0 } s ? s.PreviousState : AlwaysOnTop;

    /// <summary>
    /// Geri yükleme sırasında ABM_SETSTATE gönderilmeli mi?
    /// Koy görev çubuğunu gerçekten gizlemişse (saved is { Hidden: true }) veya kullanıcı kurtarma amacıyla
    /// zorlamışsa (force: true) yazılır. Aksi halde Windows'un mevcut durumuna dokunulmaz.
    /// </summary>
    public static bool ShouldWriteState(TaskbarSavedState? saved, bool force = false)
        => force || saved is { Hidden: true };

    public static string Serialize(TaskbarSavedState state, DateTime utcNow)
    {
        var sb = new StringBuilder();
        sb.Append("{\"taskbarHidden\": ").Append(state.Hidden ? "true" : "false");
        sb.Append(", \"previousState\": ").Append(state.PreviousState.ToString(CultureInfo.InvariantCulture));
        sb.Append(", \"timestamp\": \"").Append(utcNow.ToString("O", CultureInfo.InvariantCulture)).Append("\"}");
        return sb.ToString();
    }

    /// <summary>Bozuk ya da eksik içerikte null döner (çağıran güvenli varsayılana düşer).</summary>
    public static TaskbarSavedState? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        var hidden = ReadToken(content, "\"taskbarHidden\":") is { } h &&
                     h.Equals("true", StringComparison.OrdinalIgnoreCase);

        if (ReadToken(content, "\"previousState\":") is not { } p ||
            !int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var previous))
            return null;

        return new TaskbarSavedState(hidden, previous);
    }

    private static string? ReadToken(string content, string key)
    {
        var idx = content.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var start = idx + key.Length;
        var end = content.IndexOfAny([',', '}'], start);
        return (end > start ? content[start..end] : content[start..]).Trim();
    }
}
