using System.Globalization;
using Koy.Core.SystemStats;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.Audio;
using Koy.Core.Display;
using Koy.Core.Power;
using Koy.Core.Voice;

namespace Koy.App.Features.Notch.OSD;

/// <summary>
/// Çentik OSD (ses, parlaklık ve pil HUD göstergesi) görünüm modeli.
/// 260×40 px ebadında, 1.5 saniye hareketsizlik sonrası otomatik kapanan gösterge mantığı.
/// </summary>
public sealed partial class OsdIndicatorViewModel : ObservableObject, IDisposable
{
    private readonly DispatcherTimer _autoDismissTimer;
    private IAudioService? _attachedAudio;
    private IBrightnessService? _attachedBrightness;
    private bool _disposed;

    /// <summary>
    /// Göstergenin şimdi çıkıp çıkamayacağını söyler (ör. çentik açıkken panel yerine gösterge çıkmasın).
    /// Atanmamışsa her zaman çıkar.
    /// </summary>
    public Func<bool>? CanShow { get; set; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private OsdType _type = OsdType.None;

    [ObservableProperty]
    private int _percent;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private string _percentText = "";

    [ObservableProperty]
    private BatteryAlertKind _batteryKind = BatteryAlertKind.None;

    [ObservableProperty]
    private string _batteryStatusText = "";

    [ObservableProperty]
    private string _bluetoothDeviceName = "";

    [ObservableProperty]
    private bool _bluetoothConnected;

    [ObservableProperty]
    private string _bluetoothStatusText = "";

    [ObservableProperty]
    private string _voicePrompt = "";

    [ObservableProperty]
    private string _voiceStatusText = "";

    [ObservableProperty]
    private VoiceState _voiceState = VoiceState.Idle;

    public GridLength FillStarWidth => new(Math.Clamp(Percent, 0, 100), GridUnitType.Star);
    public GridLength RemainingStarWidth => new(Math.Max(0, 100 - Math.Clamp(Percent, 0, 100)), GridUnitType.Star);

    public event EventHandler? Activated;
    public event EventHandler? Dismissed;

    public OsdIndicatorViewModel(Dispatcher? dispatcher = null)
    {
        var disp = dispatcher ?? (Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _autoDismissTimer = new DispatcherTimer(DispatcherPriority.Normal, disp)
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _autoDismissTimer.Tick += OnAutoDismissTick;
    }

    /// <summary>
    /// Ses seviyesi ve sessize alma durumunu gösterir.
    /// </summary>
    public void ShowVolume(float volume, bool isMuted)
    {
        var pct = (int)Math.Round(Math.Clamp(volume, 0f, 1f) * 100);
        Type = OsdType.Volume;
        Percent = pct;
        IsMuted = isMuted;
        PercentText = pct.ToString();
        IsActive = true;

        OnPropertyChanged(nameof(FillStarWidth));
        OnPropertyChanged(nameof(RemainingStarWidth));

        ResetTimer();
        Activated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Ekran parlaklık seviyesini gösterir.
    /// </summary>
    public void ShowBrightness(int brightness)
    {
        var pct = Math.Clamp(brightness, 0, 100);
        Type = OsdType.Brightness;
        Percent = pct;
        IsMuted = false;
        PercentText = pct.ToString();
        IsActive = true;

        OnPropertyChanged(nameof(FillStarWidth));
        OnPropertyChanged(nameof(RemainingStarWidth));

        ResetTimer();
        Activated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pil bildirimini gösterir (şarja takıldı, şarjdan çıktı, düşük pil, tam şarj).
    /// 1.5 saniye sonra otomatik kapanır.
    /// </summary>
    public void ShowBattery(BatteryAlertKind kind, int percent)
    {
        RunOnUi(() =>
        {
            var pct = Math.Clamp(percent, 0, 100);
            Type = OsdType.Battery;
            BatteryKind = kind;
            Percent = pct;
            PercentText = $"{pct}%";
            IsMuted = false;
            BatteryStatusText = kind switch
            {
                BatteryAlertKind.PluggedIn => Strings.BatteryCharging,
                BatteryAlertKind.Unplugged => Strings.Get("BatteryOnBattery"),
                BatteryAlertKind.LowBattery => Strings.Get("BatteryLow"),
                BatteryAlertKind.FullyCharged => Strings.Get("BatteryFull"),
                _ => $"{pct}%"
            };
            IsActive = true;

            OnPropertyChanged(nameof(FillStarWidth));
            OnPropertyChanged(nameof(RemainingStarWidth));

            ResetTimer(TimeSpan.FromSeconds(1.5));
            Activated?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Bluetooth ses cihazı bağlantı/bağlantı kesilme HUD göstergesini açar.
    /// 2.0 saniye sonra otomatik kapanır.
    /// </summary>
    public void ShowBluetoothDevice(string name, bool connected, int? battery)
    {
        RunOnUi(() =>
        {
            Type = OsdType.Bluetooth;
            BluetoothDeviceName = name;
            BluetoothConnected = connected;
            Percent = battery ?? 0;
            PercentText = battery is >= 0 and <= 100 ? SystemFormat.Percent(battery.Value, CultureInfo.CurrentCulture) : "";
            BluetoothStatusText = connected
                ? Strings.Get("BluetoothConnected")
                : Strings.Get("BluetoothDisconnected");
            IsMuted = false;
            IsActive = true;

            OnPropertyChanged(nameof(FillStarWidth));
            OnPropertyChanged(nameof(RemainingStarWidth));

            ResetTimer(TimeSpan.FromSeconds(2.0));
            Activated?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// İsteğe bağlı olarak harici IAudioService dinlemesi bağlar.
    /// </summary>
    public void Attach(IAudioService audioService)
    {
        DetachAudio();
        _attachedAudio = audioService;
        _attachedAudio.StateChanged += OnAudioStateChanged;
    }

    /// <summary>
    /// İsteğe bağlı olarak harici IBrightnessService dinlemesi bağlar.
    /// </summary>
    public void Attach(IBrightnessService brightnessService)
    {
        DetachBrightness();
        _attachedBrightness = brightnessService;
        _attachedBrightness.BrightnessChanged += OnBrightnessChanged;
    }

    private void OnAudioStateChanged(object? sender, AudioStateChangedEventArgs e)
    {
        RunOnUi(() => ShowVolume(e.Volume, e.IsMuted));
    }

    private void OnBrightnessChanged(object? sender, int e)
    {
        RunOnUi(() => ShowBrightness(e));
    }

    // Servis olayları ses ve WMI iş parçacıklarından gelir; durum ve görünüm yalnız UI thread'inde değişir.
    private void RunOnUi(Action show)
    {
        var disp = _autoDismissTimer.Dispatcher;
        if (disp.CheckAccess())
        {
            if (!_disposed && (CanShow?.Invoke() ?? true))
                show();
        }
        else
        {
            disp.InvokeAsync(() =>
            {
                if (!_disposed && (CanShow?.Invoke() ?? true))
                    show();
            });
        }
    }

    private void ResetTimer(TimeSpan? interval = null)
    {
        _autoDismissTimer.Stop();
        _autoDismissTimer.Interval = interval ?? TimeSpan.FromMilliseconds(1500);
        _autoDismissTimer.Start();
    }

    private void OnAutoDismissTick(object? sender, EventArgs e)
    {
        _autoDismissTimer.Stop();
        Dismiss();
    }

    public void ShowVoice(VoiceState state, string statusText, string prompt)
    {
        RunOnUi(() =>
        {
            if (state == VoiceState.Idle)
            {
                Dismiss();
                return;
            }

            Type = OsdType.Voice;
            VoiceState = state;
            VoiceStatusText = statusText;
            VoicePrompt = prompt;
            PercentText = state switch
            {
                VoiceState.AwaitingConfirmation => "Enter ↵",
                VoiceState.Listening => "• REC",
                VoiceState.Running => "...",
                _ => ""
            };
            IsActive = true;

            if (state is VoiceState.Completed or VoiceState.Failed)
            {
                ResetTimer(TimeSpan.FromSeconds(3));
            }
            else
            {
                _autoDismissTimer.Stop();
            }

            Activated?.Invoke(this, EventArgs.Empty);
        });
    }

    public void Dismiss()
    {
        if (!IsActive)
            return;

        IsActive = false;
        Type = OsdType.None;
        BatteryKind = BatteryAlertKind.None;
        BatteryStatusText = "";
        BluetoothDeviceName = "";
        BluetoothConnected = false;
        BluetoothStatusText = "";
        VoicePrompt = "";
        VoiceStatusText = "";
        VoiceState = VoiceState.Idle;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void DetachAudio()
    {
        if (_attachedAudio is not null)
        {
            _attachedAudio.StateChanged -= OnAudioStateChanged;
            _attachedAudio = null;
        }
    }

    private void DetachBrightness()
    {
        if (_attachedBrightness is not null)
        {
            _attachedBrightness.BrightnessChanged -= OnBrightnessChanged;
            _attachedBrightness = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _autoDismissTimer.Stop();
        _autoDismissTimer.Tick -= OnAutoDismissTick;
        DetachAudio();
        DetachBrightness();
    }
}
