using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Settings;

namespace Koy.App.Features.Settings;

/// <summary>
/// Gelişmiş sayfasında bir satır (issue #69). Değer doğrulanmadan yazılmaz: <see cref="Commit"/> Core'a gider
/// (<see cref="AdvancedSettings.TrySet"/>), geçersizse satır kırmızı ipucu gösterir ve dosyaya dokunulmaz.
/// </summary>
public sealed partial class AdvancedSettingRow : ObservableObject
{
    private readonly Func<string, string, AdvancedSettingError> _apply;
    private readonly Func<string, AdvancedSettingError> _reset;
    private readonly string _value;

    public AdvancedSettingRow(AdvancedSetting model, Func<string, string, AdvancedSettingError> apply, Func<string, AdvancedSettingError> reset)
    {
        _apply = apply;
        _reset = reset;
        _value = model.Value;
        Key = model.Key;
        Kind = model.Kind;
        Default = model.Default;
        Choices = model.Choices;
        IsChanged = model.IsChanged;
        _text = model.Value;
        _boolValue = model.Value == "true";
    }

    public string Key { get; }

    public AdvancedSettingKind Kind { get; }

    public bool IsBool => Kind == AdvancedSettingKind.Bool;

    public bool IsChoice => Kind == AdvancedSettingKind.Choice;

    public bool IsTextual => Kind is AdvancedSettingKind.Integer or AdvancedSettingKind.Number or AdvancedSettingKind.Text;

    public string Default { get; }

    public string DefaultText => string.Format(Strings.Get("AdvancedDefault"), Default.Length == 0 ? "\"\"" : Default);

    public IReadOnlyList<string> Choices { get; }

    public bool IsChanged { get; }

    [ObservableProperty]
    private string _text;

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private string _error = "";

    // Anahtar ve seçim hemen yazılır; metin kutusu odak kaybında ya da Enter ile (Commit).
    partial void OnBoolValueChanged(bool value) => Apply(value ? "true" : "false");

    partial void OnTextChanged(string value)
    {
        if (IsChoice)
            Apply(value);
    }

    [RelayCommand]
    private void Commit()
    {
        if (IsTextual && !string.Equals(Text, _value, StringComparison.Ordinal))
            Apply(Text);
    }

    [RelayCommand]
    private void Reset() => Show(_reset(Key));

    private void Apply(string text) => Show(_apply(Key, text));

    private void Show(AdvancedSettingError error) =>
        Error = error == AdvancedSettingError.None ? "" : Strings.Get("AdvancedInvalid");
}
