using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.Core.Diagnostics;
using Koy.Core.Settings;
using Koy.Core.Weather;
using Koy.Platform.Shell;
using Koy.Platform.Weather;

namespace Koy.App.Features.Wizard;

public sealed partial class SetupWizardViewModel : ObservableObject
{
    public const int LastStep = 4;

    private readonly SettingsStore _store;
    private readonly ILocationService _location;
    private LocationResult? _locationResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsStep4))]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    [NotifyPropertyChangedFor(nameof(NextButtonText))]
    private int _currentStep = 1;

    [ObservableProperty]
    private int _selectedLayoutIndex; // 0: Çentik ve dock, 1: Yalnız çentik, 2: Yalnız dock

    [ObservableProperty]
    private string _selectedLanguage = "tr";

    [ObservableProperty]
    private bool _runAtStartup = true;

    /// <summary>Hava durumu için konum kullanılsın mı. Varsayılan hayır: izin yalnız kullanıcı isteyince sorulur.</summary>
    [ObservableProperty]
    private bool _useLocation;

    /// <summary>Konum alınıyor (Windows izin sorusu çıkmış olabilir); bu sürede düğmeler kapalı.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextCommand), nameof(BackCommand))]
    private bool _isLocating;

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool CanGoBack => CurrentStep > 1;
    public string NextButtonText => Koy.App.Resources.Strings.Get(CurrentStep == LastStep ? "WizStart" : "WizContinue");

    public event Action? Completed;

    public SetupWizardViewModel(SettingsStore? store = null, ILocationService? location = null)
    {
        _store = store ?? new SettingsStore(SettingsStore.DefaultPath);
        _location = location ?? new LocationService();
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private async Task NextAsync()
    {
        if (CurrentStep == 3 && UseLocation && _locationResult is null)
        {
            // Konum adımından çıkarken tek seferlik, düşük doğruluklu sorgu; reddedilse ya da zaman aşımına uğrasa da
            // kurulum devam eder (hava durumu o zaman Ayarlar'dan şehir seçilene kadar gizli kalır).
            IsLocating = true;
            try
            {
                _locationResult = await _location.GetLocationAsync();
            }
            catch (Exception ex)
            {
                CrashLog.Write("Sihirbaz: cihaz konumu", ex);
                _locationResult = new LocationResult(LocationStatus.Error);
            }
            finally
            {
                IsLocating = false;
            }
        }

        if (CurrentStep < LastStep)
            CurrentStep++;
        else
            Complete();
    }

    [RelayCommand(CanExecute = nameof(CanNavigate))]
    private void Back()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    private bool CanNavigate() => !IsLocating;

    // Seçim değişirse eski sorgu sonucu geçersiz olur (Evet → Hayır → Evet yeniden sorar).
    partial void OnUseLocationChanged(bool value) => _locationResult = null;

    [RelayCommand]
    private void Complete()
    {
        var settings = _store.Load();

        var notchEnabled = SelectedLayoutIndex is 0 or 1;
        var dockEnabled = SelectedLayoutIndex is 0 or 2;

        var updated = settings with
        {
            FirstRunCompleted = true,
            LastSeenVersion = AppInfo.Version,
            Language = SelectedLanguage,
            NotchEnabled = notchEnabled,
            DockEnabled = dockEnabled,
            HideWindowsTaskbar = dockEnabled,
            WeatherUseLocation = UseLocation
        };

        // Konum alındıysa koordinat ayara yazılır; alınamadıysa hava durumu gizli kalır.
        if (UseLocation && _locationResult is { } result)
            updated = WeatherLocationPolicy.ApplyDeviceLocation(updated, result, DateTimeOffset.UtcNow);

        _store.Save(updated);

        StartupService.SetEnabled(RunAtStartup);

        Completed?.Invoke();
    }
}
