using System.Globalization;
using Koy.Core.SystemStats;
using System.Windows;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Koy.App.Resources;
using Koy.Core.Audio;
using Koy.Core.Display;
using Koy.Core.Notch;
using Koy.Core.Notifications;
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
    [NotifyPropertyChangedFor(nameof(IsVoiceEditing))]
    private VoiceState _voiceState = VoiceState.Idle;

    /// <summary>Komut kutusunun metni; Windows sesle yazma da kullanıcı da buraya yazar.</summary>
    [ObservableProperty]
    private string _voiceInput = "";

    /// <summary>Kutu açıkken (yazılıyor) metin kutusu, değilse komut metni gösterilir.</summary>
    public bool IsVoiceEditing => VoiceState == VoiceState.Editing;

    /// <summary>Kutudaki metin değişti (yalnız kutu açıkken).</summary>
    public event Action<string>? VoiceInputChanged;

    partial void OnVoiceInputChanged(string value)
    {
        if (VoiceState == VoiceState.Editing)
            VoiceInputChanged?.Invoke(value);
    }

    /// <summary>Tek satıra sığmayan tam açıklama (fareyle üzerine gelince).</summary>
    [ObservableProperty]
    private string? _voiceDetail;

    /// <summary>Kart türündeki OSD'nin içeriği (<see cref="OsdType.Card"/>).</summary>
    [ObservableProperty]
    private NotchCard? _card;

    /// <summary>Kartta gösterilecek uygulama simgesi (bildirim kartı); yoksa görünüm genel simge çizer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCardIcon))]
    private ImageSource? _cardIcon;

    public bool HasCardIcon => CardIcon is not null;

    /// <summary>Kartın birincil düğmesine ("Güncelle") basıldı; kapanan kartı verir (aynı gösterge birden çok kartın sahibi olabilir).</summary>
    public event Action<NotchCard>? PrimaryActionRequested;

    /// <summary>Kartın × düğmesine basıldı; kapanan kartı verir.</summary>
    public event Action<NotchCard>? DismissRequested;

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

    /// <param name="detail">Tek satıra sığmayan tam açıklama (hata), fareyle üzerine gelince.</param>
    /// <param name="linger">Tıklanınca açılacak bir yanıt var: okumaya ve tıklamaya zaman kalsın diye daha uzun görünür.</param>
    public void ShowVoice(VoiceState state, string statusText, string prompt, string? detail = null, bool linger = false)
    {
        RunOnUi(() =>
        {
            if (state == VoiceState.Idle)
            {
                Dismiss();
                return;
            }

            // Kutu yeni açıldıysa boş başlar; açıkken gelen güncellemeler yazılanı silmez.
            if (state == VoiceState.Editing && VoiceState != VoiceState.Editing)
                VoiceInput = "";

            Type = OsdType.Voice;
            VoiceState = state;
            VoiceStatusText = statusText;
            VoicePrompt = prompt;
            VoiceDetail = string.IsNullOrEmpty(detail) ? null : detail;
            PercentText = state switch
            {
                VoiceState.Editing => "Enter ↵",
                VoiceState.Running => "...",
                _ => ""
            };
            IsActive = true;

            if (state is VoiceState.Completed or VoiceState.Failed)
            {
                ResetTimer(TimeSpan.FromSeconds(linger ? 10 : state == VoiceState.Failed ? 6 : 3));
            }
            else
            {
                _autoDismissTimer.Stop();
            }

            Activated?.Invoke(this, EventArgs.Empty);
        });
    }

    /// <summary>
    /// Çentikte düğmeli bir kart gösterir. Çentik açıkken (<see cref="CanShow"/>) gösterilmez ve false döner;
    /// çağıran çentik kapanınca yeniden dener. <paramref name="lifetime"/> sonunda kendiliğinden kapanır.
    /// </summary>
    public bool ShowCard(NotchCard card, TimeSpan lifetime)
    {
        if (_disposed || !(CanShow?.Invoke() ?? true))
            return false;

        Card = card;
        // Simge yalnız bu kartın kendi baytlarından çözülür; boş/saydam/bozuksa kabuk simgesi, o da yoksa görünüm zil çizer.
        CardIcon = DecodeIcon(card.Icon) ?? DecodeIcon(card.FallbackIcon);
        Type = OsdType.Card;
        IsActive = true;

        ResetTimer(lifetime);
        Activated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Kartın birincil düğmesi: kart kapanır, sahibi eylemi yürütür.</summary>
    public void RequestPrimaryAction()
    {
        if (Type != OsdType.Card || Card is not { } card)
            return;

        Dismiss();
        PrimaryActionRequested?.Invoke(card);
    }

    /// <summary>Kartın × düğmesi: kart kapanır, sahibi bunu "bu oturumda gösterme" diye kaydeder.</summary>
    public void RequestDismiss()
    {
        if (Type != OsdType.Card || Card is not { } card)
            return;

        Dismiss();
        DismissRequested?.Invoke(card);
    }

    // Simge gösterilecek boyutta çözülür ve dondurulur (AGENTS #9); çözülemeyen simge kartı engellemez.
    private static ImageSource? DecodeIcon(byte[]? data)
    {
        if (data is not { Length: > 0 })
            return null;

        try
        {
            using var stream = new MemoryStream(data);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 48;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return IsBlank(image) ? null : image;
        }
        catch
        {
            return null;
        }
    }

    // Tamamen saydam (ya da boyutsuz) resim "simge var" sayılmaz: boş daire yerine zil çıksın.
    private static bool IsBlank(BitmapSource image)
    {
        if (image.PixelWidth == 0 || image.PixelHeight == 0)
            return true;

        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return IconBlankCheck.IsBlank(pixels);
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
        VoiceInput = "";
        VoiceDetail = null;
        VoiceStatusText = "";
        VoiceState = VoiceState.Idle;
        Card = null;
        CardIcon = null;
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
