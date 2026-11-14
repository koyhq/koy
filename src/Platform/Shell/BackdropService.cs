using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Koy.Platform.Shell;

public enum WindowBackdropType
{
    None = 1,
    Mica = 2,
    Acrylic = 3,
    MicaAlt = 4
}

/// <summary>DWM köşe tercihi (DWM_WINDOW_CORNER_PREFERENCE).</summary>
public enum WindowCornerStyle
{
    Default = 0,
    Square = 1,
    Round = 2,
    RoundSmall = 3
}

/// <summary>
/// Windows 11 pencere malzemesi: sistem arka planı (akrilik / mika), koyu çerçeve ve köşe yuvarlaması DWM'den.
/// Yalnız katmansız pencerelerde (AllowsTransparency = false) etkilidir; katmanlı pencerede DWM arka planı çizmez.
/// Hata vermez: eski Windows'ta (Windows 10) köşe ve sistem arka planı öznitelikleri atlanır, koyu çerçeve uygulanır.
/// </summary>
public static class BackdropService
{
    private const int UseImmersiveDarkMode = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE
    private const int WindowCornerPreference = 33; // DWMWA_WINDOW_CORNER_PREFERENCE

    /// <summary>DWM sistem arka planı (akrilik/mika) ve köşe yuvarlama desteği (Windows 11 / 22000+).</summary>
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

    public static void ApplyBackdrop(nint hwnd, WindowBackdropType type)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            return;

        Set(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)type);
    }

    /// <summary>Arka plan, koyu / açık çerçeve (akriliğin tonu da buna uyar) ve köşe tercihi birlikte.</summary>
    public static void ApplyWindowFrame(nint hwnd, WindowBackdropType type, bool dark, WindowCornerStyle corners)
    {
        Set(hwnd, (DWMWINDOWATTRIBUTE)UseImmersiveDarkMode, dark ? 1 : 0);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            Set(hwnd, (DWMWINDOWATTRIBUTE)WindowCornerPreference, (int)corners);

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            Set(hwnd, DWMWINDOWATTRIBUTE.DWMWA_SYSTEMBACKDROP_TYPE, (int)type);
    }

    private static void Set(nint hwnd, DWMWINDOWATTRIBUTE attribute, int value)
    {
        if (hwnd == 0)
            return;

        try
        {
            unsafe
            {
                PInvoke.DwmSetWindowAttribute((HWND)hwnd, attribute, &value, sizeof(int));
            }
        }
        catch
        {
        }
    }
}
