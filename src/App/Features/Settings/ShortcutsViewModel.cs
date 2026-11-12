using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Shortcuts;
using Koy.App.Resources;
using Koy.Core.Settings;
using Koy.Core.Shortcuts;

namespace Koy.App.Features.Settings;

/// <summary>Kısayollar sayfasındaki tek satır: eylem adı, geçerli kombinasyon, yakalama durumu ve hata metni.</summary>
public sealed partial class ShortcutRowViewModel : ObservableObject
{
    private readonly ShortcutsViewModel _owner;

    public ShortcutAction Action { get; }
    public string Title { get; private set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeyTokens), nameof(HasShortcut), nameof(IsDefault))]
    private string _text = "";

    [ObservableProperty]
    private bool _isCapturing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _errorText;

    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    public bool HasShortcut => !string.IsNullOrWhiteSpace(Text);
    public bool IsDefault => ShortcutBindings.IsDefault(Action, Text);

    /// <summary>Rozet olarak gösterilecek tuş adları (Ctrl, Alt, N...).</summary>
    public IReadOnlyList<string> KeyTokens =>
        Text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t == "Space" ? Strings.Get("KeySpace") : t)
            .ToList();

    public ShortcutRowViewModel(ShortcutsViewModel owner, ShortcutAction action)
    {
        _owner = owner;
        Action = action;
        RefreshTitle();
    }

    public void RefreshTitle()
    {
        Title = Strings.Get("Shortcut" + Action);
        OnPropertyChanged(nameof(Title));
    }

    [RelayCommand]
    private void StartCapture()
    {
        ErrorText = null;
        IsCapturing = true;
        _owner.BeginHookCapture(this);
    }

    [RelayCommand]
    private void ResetToDefault() => _owner.Assign(this, ShortcutBindings.DefaultText(Action));

    public void CancelCapture()
    {
        IsCapturing = false;
        _owner.EndHookCapture(this);
    }

    /// <summary>Klavye kancasından gelen kombinasyon (tek başına Win ve Windows'un kısayolları yalnız böyle yakalanır).</summary>
    public void HandleCaptured(Hotkey hotkey)
    {
        if (IsCapturing)
            Complete(hotkey);
    }

    /// <summary>Yakalama kutusuna gelen tuş. true dönerse tuş işlendi (olay tüketilir).</summary>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (!IsCapturing)
            return false;

        // Yalnız değiştirici basılı: kombinasyon henüz bitmedi.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System or Key.None)
            return true;

        Complete(new Hotkey(ToHotkeyModifiers(modifiers), KeyInterop.VirtualKeyFromKey(key)));
        return true;
    }

    private void Complete(Hotkey hotkey)
    {
        if (hotkey.Modifiers == HotkeyModifiers.None)
        {
            if (hotkey.VirtualKey == KeyInterop.VirtualKeyFromKey(Key.Escape))
            {
                CancelCapture();
                return;
            }

            if (hotkey.VirtualKey == KeyInterop.VirtualKeyFromKey(Key.Back) || hotkey.VirtualKey == KeyInterop.VirtualKeyFromKey(Key.Delete))
            {
                CancelCapture();
                _owner.Assign(this, "");
                return;
            }
        }

        if (!hotkey.IsValid)
        {
            // Yakalama sürer: kullanıcı yeni bir kombinasyon deneyebilir.
            ErrorText = Strings.Get("ShortcutErrInvalid");
            return;
        }

        CancelCapture();
        _owner.Assign(this, hotkey.Format());
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys m)
    {
        var result = HotkeyModifiers.None;
        if ((m & ModifierKeys.Control) != 0) result |= HotkeyModifiers.Control;
        if ((m & ModifierKeys.Alt) != 0) result |= HotkeyModifiers.Alt;
        if ((m & ModifierKeys.Shift) != 0) result |= HotkeyModifiers.Shift;
        if ((m & ModifierKeys.Windows) != 0) result |= HotkeyModifiers.Win;
        return result;
    }
}

/// <summary>
/// Kısayollar sayfasının mantığı: satırlar, kaydetme, varsayılana dönme ve kayıt hatalarının satıra yansıması.
/// Kısayollar ayarlara yazılır, <see cref="App"/> üzerinden yeniden kaydedilir.
/// </summary>
public sealed class ShortcutsViewModel
{
    private readonly SettingsStore _store;
    private ShortcutController? _subscribedTo;
    private ShortcutRowViewModel? _hookCaptureRow;

    public ObservableCollection<ShortcutRowViewModel> Rows { get; } = [];

    public ShortcutsViewModel(SettingsStore store)
    {
        _store = store;
        foreach (var action in ShortcutBindings.All)
            Rows.Add(new ShortcutRowViewModel(this, action));
    }

    public void BeginHookCapture(ShortcutRowViewModel row)
    {
        if ((Application.Current as App)?.Shortcuts is { } controller && controller.BeginCapture(row.HandleCaptured))
            _hookCaptureRow = row;
    }

    public void EndHookCapture(ShortcutRowViewModel row)
    {
        if (!ReferenceEquals(_hookCaptureRow, row))
            return;

        _hookCaptureRow = null;
        (Application.Current as App)?.Shortcuts?.EndCapture();
    }

    private void OnResultsChanged() => Application.Current?.Dispatcher.InvokeAsync(ShowRegistrationResults);

    /// <summary>Ayarlardaki metinleri satırlara yükler.</summary>
    public void Load(AppSettings settings)
    {
        // Denetleyici uygulama açılışında kurulur; sayfa ilk yüklendiğinde bağlanır.
        if (_subscribedTo is null && (Application.Current as App)?.Shortcuts is { } controller)
        {
            _subscribedTo = controller;
            controller.ResultsChanged += OnResultsChanged;
        }

        foreach (var row in Rows)
        {
            row.CancelCapture();
            row.Text = ShortcutBindings.GetText(settings, row.Action);
            row.RefreshTitle();
        }

        ShowRegistrationResults();
    }

    /// <summary>Yeni kombinasyonu denetler, kaydeder ve yeniden kayıt ister. Sorun varsa satırda gösterilir.</summary>
    public void Assign(ShortcutRowViewModel row, string text)
    {
        var settings = _store.Load();

        if (!string.IsNullOrEmpty(text))
        {
            if (!Hotkey.TryParse(text, out var hotkey))
            {
                row.ErrorText = Strings.Get("ShortcutErrInvalid");
                return;
            }

            // Windows'a ait ya da başka bir Koy eylemine atanmış kombinasyon kaydedilmez.
            var problem = ShortcutConflicts.Check(hotkey, row.Action, ShortcutBindings.ReadAll(settings));
            if (problem is not null)
            {
                row.ErrorText = Describe(problem);
                return;
            }
        }

        _store.Save(ShortcutBindings.WithText(settings, row.Action, text));
        row.Text = text;
        row.ErrorText = null;

        // Yeniden kayıt sonucu ResultsChanged ile satırlara döner (başka uygulama tutuyorsa hata görünür).
        if (Application.Current is App app)
            app.ApplyShortcuts();
        else
            ShowRegistrationResults();
    }

    private void ShowRegistrationResults()
    {
        var results = (Application.Current as App)?.Shortcuts?.Results;
        foreach (var row in Rows)
        {
            if (row.IsCapturing)
                continue;

            row.ErrorText = results is not null && results.TryGetValue(row.Action, out var result) && result.IsError
                ? Describe(result)
                : Hotkey.TryParse(row.Text, out var hotkey) && hotkey.CapturesTyping
                    ? Strings.Get("ShortcutWarnTyping")
                    : null;
        }
    }

    private string Describe(ShortcutResult result) => result.Status switch
    {
        ShortcutStatus.Invalid => Strings.Get("ShortcutErrInvalid"),
        ShortcutStatus.ReservedBySystem => Strings.Get("ShortcutErrReserved"),
        ShortcutStatus.DuplicateOfOtherAction => string.Format(CultureInfo.CurrentCulture, 
            Strings.Get("ShortcutErrDuplicate"),
            Rows.FirstOrDefault(r => r.Action == result.ConflictsWith)?.Title ?? ""),
        ShortcutStatus.InUseByOtherApp => Strings.Get("ShortcutErrInUse"),
        ShortcutStatus.Failed => Strings.Get("ShortcutErrFailed"),
        _ => ""
    };
}
