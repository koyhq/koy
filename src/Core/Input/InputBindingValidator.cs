using Koy.Core.Shortcuts;

namespace Koy.Core.Input;

public enum InputIssueKind
{
    /// <summary>Aynı eylem aynı tetikleyiciye iki kez bağlanmış; ikincisi atılır.</summary>
    Duplicate,

    /// <summary>Aynı tetikleyici başka bir eyleme zaten bağlı; sonraki atılır.</summary>
    Conflict,

    /// <summary>Windows'un hiçbir uygulamaya bırakmadığı kısayol (Win+L, Ctrl+Alt+Del).</summary>
    ReservedBySystem,

    /// <summary>RunCustomAction bağı var olmayan bir özel eyleme işaret ediyor.</summary>
    UnknownCustomAction,

    /// <summary>Kısayol, Koy'un yerleşik bir kısayoluyla (Ctrl+Alt+N gibi) aynı; yerleşik olan kalır.</summary>
    ConflictsWithBuiltIn,

    /// <summary>Bağ sayısı <see cref="InputBindingValidator.MaxBindings"/> sınırını aştı.</summary>
    TooMany
}

/// <param name="Index">Sorunlu bağın giriş listesindeki sırası.</param>
/// <param name="ConflictsWithIndex">Çakıştığı bağın sırası (Duplicate ve Conflict için).</param>
/// <param name="BuiltIn">Çakıştığı yerleşik kısayol (ConflictsWithBuiltIn için).</param>
public sealed record InputBindingIssue(
    InputIssueKind Kind,
    int Index,
    InputBinding Binding,
    int? ConflictsWithIndex = null,
    ShortcutAction? BuiltIn = null);

public sealed record InputBindingReport(IReadOnlyList<InputBinding> Accepted, IReadOnlyList<InputBindingIssue> Issues);

/// <summary>
/// Çözülmüş bağları denetler. Kural: giriş sırasında ilk gelen kazanır. Aynı tetikleyici tekrar gelirse
/// aynı eylemse Duplicate, farklı eylemse Conflict olur. Bir çakışan bağ kabul listesine girmez.
/// Yerleşik kısayollar (<see cref="ShortcutBindings"/>) her zaman önce gelir: ShortcutManager de ilk sıradakini tutar.
/// </summary>
public static class InputBindingValidator
{
    /// <summary>Kabul edilecek en çok bağ; fazlası TooMany olur (elle bozulmuş dosya listeyi şişirmesin).</summary>
    public const int MaxBindings = 64;

    /// <param name="builtInShortcuts">Yerleşik kısayolların metni (<see cref="ShortcutBindings.ReadAll"/>); null ise denetlenmez.</param>
    public static InputBindingReport Validate(
        IReadOnlyList<InputBinding> bindings,
        IReadOnlySet<string> customActionIds,
        IReadOnlyDictionary<ShortcutAction, string>? builtInShortcuts = null)
    {
        var accepted = new List<InputBinding>();
        var issues = new List<InputBindingIssue>();
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);

        // CustomActionParser kimlikleri büyük-küçük harf duyarsız tekil tutar; burada da öyle eşleşmeli.
        var knownIds = new HashSet<string>(customActionIds, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];

            if (accepted.Count >= MaxBindings)
            {
                issues.Add(new InputBindingIssue(InputIssueKind.TooMany, i, binding));
                continue;
            }

            if (binding.Action == InputAction.RunCustomAction &&
                (binding.CustomActionId is null || !knownIds.Contains(binding.CustomActionId)))
            {
                issues.Add(new InputBindingIssue(InputIssueKind.UnknownCustomAction, i, binding));
                continue;
            }

            if (binding.Trigger is ShortcutTrigger shortcut && ShortcutConflicts.IsReservedBySystem(shortcut.Hotkey))
            {
                issues.Add(new InputBindingIssue(InputIssueKind.ReservedBySystem, i, binding));
                continue;
            }

            if (binding.Trigger is ShortcutTrigger own && builtInShortcuts is not null &&
                FindBuiltIn(own.Hotkey, builtInShortcuts) is { } builtIn)
            {
                issues.Add(new InputBindingIssue(InputIssueKind.ConflictsWithBuiltIn, i, binding, BuiltIn: builtIn));
                continue;
            }

            var key = KeyOf(binding.Trigger);
            if (owners.TryGetValue(key, out var first))
            {
                var previous = bindings[first];
                var sameAction = previous.Action == binding.Action &&
                                 string.Equals(previous.CustomActionId, binding.CustomActionId, StringComparison.OrdinalIgnoreCase);
                issues.Add(new InputBindingIssue(
                    sameAction ? InputIssueKind.Duplicate : InputIssueKind.Conflict,
                    i,
                    binding,
                    first));
                continue;
            }

            owners[key] = i;
            accepted.Add(binding);
        }

        return new InputBindingReport(accepted, issues);
    }

    /// <summary>Aynı kombinasyonu tutan yerleşik eylem (ShortcutConflicts.Check ile aynı karşılaştırma); yoksa null.</summary>
    private static ShortcutAction? FindBuiltIn(Hotkey hotkey, IReadOnlyDictionary<ShortcutAction, string> builtIn)
    {
        foreach (var (action, text) in builtIn)
        {
            if (Hotkey.TryParse(text, out var other) && other == hotkey)
                return action;
        }

        return null;
    }

    /// <summary>
    /// Tetikleyici kimliği. Köşede süre ve geri itme mesafesi kimliğe girmez: aynı köşe iki kez bağlanamaz.
    /// </summary>
    internal static string KeyOf(InputTrigger trigger) => trigger switch
    {
        ShortcutTrigger s => $"S:{(int)s.Hotkey.Modifiers}:{s.Hotkey.VirtualKey}",
        HotCornerTrigger h => $"C:{h.Corner}",
        MouseTrigger m => $"M:{m.Surface}:{m.Gesture}",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger))
    };
}
