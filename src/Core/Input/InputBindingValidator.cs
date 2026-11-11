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
    UnknownCustomAction
}

/// <param name="Index">Sorunlu bağın giriş listesindeki sırası.</param>
/// <param name="ConflictsWithIndex">Çakıştığı bağın sırası (Duplicate ve Conflict için).</param>
public sealed record InputBindingIssue(InputIssueKind Kind, int Index, InputBinding Binding, int? ConflictsWithIndex = null);

public sealed record InputBindingReport(IReadOnlyList<InputBinding> Accepted, IReadOnlyList<InputBindingIssue> Issues);

/// <summary>
/// Çözülmüş bağları denetler. Kural: giriş sırasında ilk gelen kazanır. Aynı tetikleyici tekrar gelirse
/// aynı eylemse Duplicate, farklı eylemse Conflict olur. Bir çakışan bağ kabul listesine girmez.
/// </summary>
public static class InputBindingValidator
{
    public static InputBindingReport Validate(IReadOnlyList<InputBinding> bindings, IReadOnlySet<string> customActionIds)
    {
        var accepted = new List<InputBinding>();
        var issues = new List<InputBindingIssue>();
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];

            if (binding.Action == InputAction.RunCustomAction &&
                (binding.CustomActionId is null || !customActionIds.Contains(binding.CustomActionId)))
            {
                issues.Add(new InputBindingIssue(InputIssueKind.UnknownCustomAction, i, binding));
                continue;
            }

            if (binding.Trigger is ShortcutTrigger shortcut && ShortcutConflicts.IsReservedBySystem(shortcut.Hotkey))
            {
                issues.Add(new InputBindingIssue(InputIssueKind.ReservedBySystem, i, binding));
                continue;
            }

            var key = KeyOf(binding.Trigger);
            if (owners.TryGetValue(key, out var first))
            {
                var previous = bindings[first];
                var sameAction = previous.Action == binding.Action &&
                                 string.Equals(previous.CustomActionId, binding.CustomActionId, StringComparison.Ordinal);
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
