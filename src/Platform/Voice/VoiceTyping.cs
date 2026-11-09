using Koy.Platform.Shell;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Koy.Platform.Voice;

/// <summary>
/// Windows sesle yazmayı (Win+H) açar; Windows, odaktaki metin kutusuna kullanıcının klavye dilinde yazar
/// (Türkçe, İngilizce…). Konuşma tanıma dili paketi gerekmez; ses Microsoft'un çevrimiçi hizmetine gider.
/// </summary>
public static class VoiceTyping
{
    private static readonly VIRTUAL_KEY[] Modifiers =
    [
        VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN
    ];

    /// <summary>
    /// Kısayolun (Ctrl+Alt+V) tuşları bırakılana kadar bekler, sonra Win+H gönderir; basılı Ctrl/Alt ile
    /// gönderilen Win+H başka bir kısayola dönüşürdü. Tuşlar en çok <paramref name="maxWait"/> içinde bırakılmazsa yine gönderir.
    /// </summary>
    public static async Task OpenAsync(TimeSpan maxWait, CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + maxWait;
        while (AnyModifierDown() && DateTime.UtcNow < deadline)
            await Task.Delay(30, cancellationToken).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        ShellActionService.SendWinCombo(VIRTUAL_KEY.VK_H);
    }

    private static bool AnyModifierDown()
    {
        foreach (var key in Modifiers)
        {
            // En yüksek bit: tuş şu an basılı.
            if ((PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0)
                return true;
        }

        return false;
    }
}
