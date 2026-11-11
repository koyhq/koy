using Koy.Core.Shortcuts;

namespace Koy.Core.Input;

/// <summary>Ayar dosyasından okunan ham bağ. Alanlar boş ya da bilinmeyen olabilir; çözüm <see cref="InputBindingParser"/>'dadır.</summary>
/// <param name="Trigger">Shortcut: "Ctrl+Alt+N". HotCorner: "TopLeft". MouseButton: "Notch.Wheel".</param>
public sealed record InputBindingData(
    string? Action,
    string? Kind,
    string? Trigger,
    int? DwellMs = null,
    int? PushBackPx = null,
    string? CustomActionId = null);

/// <summary>Bir bağın düşürülme nedeni. Düşürülen bağ hiçbir zaman çalıştırılmaz.</summary>
public enum InputBindingDropReason
{
    UnknownAction,
    UnknownKind,
    InvalidTrigger,
    InvalidParameter
}

public sealed record InputBindingParseResult(InputBinding? Binding, InputBindingDropReason? DropReason)
{
    public bool IsValid => Binding is not null;
}

/// <summary>
/// Ham bağı çözer. Bilinmeyen eylem ya da tür, bozuk tetikleyici ve aralık dışı değer düşürülür; hiçbiri
/// hata fırlatmaz, böylece bir bozuk satır bütün ayarı bozmaz.
/// </summary>
public static class InputBindingParser
{
    /// <summary>Varsayılan bekleme süresi (ms). Köşeye bilinçsiz değmeyi eler.</summary>
    public const int DefaultDwellMs = 300;

    /// <summary>En çok bekleme süresi (ms); bunun üstü köşeyi ölü bırakır.</summary>
    public const int MaxDwellMs = 5000;

    /// <summary>Varsayılan geri itme mesafesi (piksel).</summary>
    public const int DefaultPushBackPx = 4;

    public const int MaxPushBackPx = 200;

    public static InputBindingParseResult Parse(InputBindingData? data)
    {
        // JSON listesindeki null öğe; hata fırlatmadan düşer.
        if (data is null)
            return Drop(InputBindingDropReason.UnknownAction);

        if (!TryParseName<InputAction>(data.Action, out var action))
            return Drop(InputBindingDropReason.UnknownAction);

        if (!TryParseName<InputTriggerKind>(data.Kind, out var kind))
            return Drop(InputBindingDropReason.UnknownKind);

        var text = data.Trigger?.Trim() ?? string.Empty;
        InputTrigger? trigger;

        switch (kind)
        {
            case InputTriggerKind.Shortcut:
                if (!Hotkey.TryParse(text, out var hotkey))
                    return Drop(InputBindingDropReason.InvalidTrigger);
                trigger = new ShortcutTrigger(hotkey);
                break;

            case InputTriggerKind.HotCorner:
                if (!TryParseName<ScreenCorner>(text, out var corner))
                    return Drop(InputBindingDropReason.InvalidTrigger);

                var dwell = data.DwellMs ?? DefaultDwellMs;
                var pushBack = data.PushBackPx ?? DefaultPushBackPx;
                if (dwell is < 0 or > MaxDwellMs || pushBack is < 0 or > MaxPushBackPx)
                    return Drop(InputBindingDropReason.InvalidParameter);

                trigger = new HotCornerTrigger(corner, dwell, pushBack);
                break;

            case InputTriggerKind.MouseButton:
                var parts = text.Split('.', StringSplitOptions.TrimEntries);
                if (parts.Length != 2 ||
                    !TryParseName<InputSurface>(parts[0], out var surface) ||
                    !TryParseName<MouseGesture>(parts[1], out var gesture))
                    return Drop(InputBindingDropReason.InvalidTrigger);

                trigger = new MouseTrigger(surface, gesture);
                break;

            default:
                return Drop(InputBindingDropReason.UnknownKind);
        }

        string? customId = null;
        if (action == InputAction.RunCustomAction)
        {
            customId = data.CustomActionId?.Trim();
            if (string.IsNullOrEmpty(customId))
                return Drop(InputBindingDropReason.InvalidParameter);
        }

        return new InputBindingParseResult(new InputBinding(action, trigger, customId), null);
    }

    private static InputBindingParseResult Drop(InputBindingDropReason reason) => new(null, reason);

    /// <summary>Ad eşleşmesi büyük-küçük harf duyarsız; sayısal metinler (örn. "3") kabul edilmez.</summary>
    private static bool TryParseName<TEnum>(string? text, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<TEnum>(name);
                return true;
            }
        }

        return false;
    }
}
