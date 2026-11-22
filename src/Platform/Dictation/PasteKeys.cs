using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Koy.Platform.Dictation;

/// <summary>Odaktaki pencereye Ctrl+V gönderir (doğrudan giriş kuyruğuna).</summary>
public static class PasteKeys
{
    private static readonly VIRTUAL_KEY[] Modifiers =
    [
        VIRTUAL_KEY.VK_CONTROL, VIRTUAL_KEY.VK_MENU, VIRTUAL_KEY.VK_SHIFT, VIRTUAL_KEY.VK_LWIN, VIRTUAL_KEY.VK_RWIN
    ];

    /// <summary>Basılı değiştirici tuş kalmadıysa true: Ctrl+Alt+V gibi yanlış kombinasyon gitmesin.</summary>
    public static bool ModifiersReleased()
    {
        foreach (var key in Modifiers)
        {
            if ((PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0)
                return false;
        }

        return true;
    }

    public static unsafe void SendCtrlV()
    {
        var inputs = new INPUT[4];

        inputs[0].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[0].Anonymous.ki.wVk = VIRTUAL_KEY.VK_CONTROL;

        inputs[1].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[1].Anonymous.ki.wVk = VIRTUAL_KEY.VK_V;

        inputs[2].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[2].Anonymous.ki.wVk = VIRTUAL_KEY.VK_V;
        inputs[2].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        inputs[3].type = INPUT_TYPE.INPUT_KEYBOARD;
        inputs[3].Anonymous.ki.wVk = VIRTUAL_KEY.VK_CONTROL;
        inputs[3].Anonymous.ki.dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;

        PInvoke.SendInput(inputs, sizeof(INPUT));
    }
}
