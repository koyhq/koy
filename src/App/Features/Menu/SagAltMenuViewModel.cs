using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Features.Settings;
using Koy.Core.Settings;
using Koy.Platform.Display;
using Koy.Platform.Shell;

namespace Koy.App.Features.Menu;

public sealed partial class SagAltMenuViewModel : ObservableObject
{
    private readonly SettingsStore _store;

    [ObservableProperty]
    private int _monitorCount = 1;

    [ObservableProperty]
    private string _monitorSummary = "1 ekranda çalışıyor";

    [ObservableProperty]
    private bool _notchEnabled = true;

    [ObservableProperty]
    private bool _dockEnabled = true;

    [ObservableProperty]
    private bool _isPaused;

    /// <summary>Çıkış tek tıkla olmaz: ilk tıklama düğmeyi "Çıkışı onayla"ya çevirir, menü kapanınca sıfırlanır.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExitText))]
    private bool _isExitConfirming;

    public string ExitText => Koy.App.Resources.Loc.Instance[IsExitConfirming ? "ExitKoyConfirm" : "ExitKoyAction"];

    [ObservableProperty]
    private string _pauseText = "1 saat duraklat";

    public event Action? RequestClose;
    public event Action? RequestOpenKoyWindow;
    public event Action<bool>? NotchToggled;
    public event Action<bool>? DockToggled;
    public event Action<bool>? PauseToggled;

    [ObservableProperty]
    private bool _hasAvailableUpdate;

    [ObservableProperty]
    private string _availableVersionText = "";

    public SagAltMenuViewModel(SettingsStore? store = null)
    {
        _store = store ?? new SettingsStore(SettingsStore.DefaultPath);
        UpdateStatus.Changed += () =>
        {
            Application.Current?.Dispatcher?.BeginInvoke(Refresh);
        };
        Refresh();
    }

    public void Refresh()
    {
        IsExitConfirming = false;
        var settings = _store.Load();
        NotchEnabled = settings.NotchEnabled;
        DockEnabled = settings.DockEnabled;

        // Duraklatma süresi dolunca uygulama kendiliğinden devam eder; menü gerçek durumu göstermeli.
        if (Application.Current is Koy.App.App app)
            IsPaused = app.IsPaused;

        try
        {
            using var monitorService = new MonitorService();
            var monitors = monitorService.GetMonitors();
            MonitorCount = monitors.Count > 0 ? monitors.Count : 1;
        }
        catch
        {
            MonitorCount = 1;
        }

        MonitorSummary = string.Format(System.Globalization.CultureInfo.CurrentCulture, Koy.App.Resources.Strings.MonitorSummary, MonitorCount);
        PauseText = IsPaused ? Koy.App.Resources.Loc.Instance["ResumePauseAction"] : Koy.App.Resources.Loc.Instance["PauseOneHourAction"];

        HasAvailableUpdate = UpdateStatus.HasAvailableUpdate;
        AvailableVersionText = UpdateStatus.AvailableVersion is { } v
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, Koy.App.Resources.Strings.Get("UpdateAvailableBadge"), v)
            : string.Empty;
    }

    [RelayCommand]
    private void OpenUpdate()
    {
        RequestClose?.Invoke();
        KoyWindow.ShowInstance();
        KoyWindow.Instance.ViewModel.OpenUpdateOverlay();
    }

    [RelayCommand]
    private void ToggleNotch()
    {
        var settings = _store.Load();
        NotchEnabled = !settings.NotchEnabled;
        _store.Save(settings with { NotchEnabled = NotchEnabled });
        NotchToggled?.Invoke(NotchEnabled);
    }

    [RelayCommand]
    private void ToggleDock()
    {
        var settings = _store.Load();
        DockEnabled = !settings.DockEnabled;
        _store.Save(settings with { DockEnabled = DockEnabled });
        DockToggled?.Invoke(DockEnabled);
    }

    [RelayCommand]
    private void OpenKoyWindow()
    {
        RequestClose?.Invoke();
        RequestOpenKoyWindow?.Invoke();
    }

    [RelayCommand]
    private void PauseOneHour()
    {
        IsPaused = !IsPaused;
        PauseText = IsPaused ? Koy.App.Resources.Loc.Instance["ResumePauseAction"] : Koy.App.Resources.Loc.Instance["PauseOneHourAction"];
        PauseToggled?.Invoke(IsPaused);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Exit()
    {
        if (!IsExitConfirming)
        {
            IsExitConfirming = true;
            return;
        }

        TaskbarService.Restore();
        Application.Current?.Shutdown();
    }
}
