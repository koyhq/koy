using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.QuickLaunch;
using Koy.Platform.Shell;

namespace Koy.App.Features.Home;

/// <summary>Hızlı başlat şeridindeki bir uygulama / dosya: simge (yoksa baş harfler) ve ad.</summary>
public sealed partial class QuickLaunchItemViewModel(string path, string name) : ObservableObject
{
    public string Path { get; } = path;

    public string Name { get; } = name;

    /// <summary>Simge yüklenemezse gösterilen iki harf.</summary>
    public string Initials { get; } = name.Length >= 2 ? name[..2] : name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _icon;

    public bool HasIcon => Icon is not null;
}

/// <summary>
/// Ana sayfadaki hızlı başlat şeridi: tıklayınca başlat, "+" ile dosya / uygulama seçerek ekle, sağ tıkla kaldır.
/// Liste ve kalıcılık <see cref="IQuickLaunchService"/> içinde (en çok 8). Simgeler kabuktan arka planda çözülür
/// (açılış animasyonunu bekletmesin), 32 px'e indirilip dondurulur ve bu modelde önbelleğe alınır.
/// </summary>
public sealed partial class QuickLaunchViewModel : ObservableObject, IDisposable
{
    private readonly IQuickLaunchService _service;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private bool _active;
    private bool _disposed;

    public QuickLaunchViewModel(IQuickLaunchService service, Dispatcher? dispatcher = null)
    {
        _service = service;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        PickPath = DefaultPick;
        PickFolderPath = DefaultPickFolder;
    }

    public ObservableCollection<QuickLaunchItemViewModel> Items { get; } = [];

    /// <summary>Liste dolu değil: "+" düğmesi görünür.</summary>
    [ObservableProperty]
    private bool _canAdd = true;

    /// <summary>Dosya seçici (kabuk iletişim kutusu); iptalde null. Testte ve araçta değiştirilebilir.</summary>
    public Func<string?> PickPath { get; set; }

    /// <summary>Klasör seçici (kabuk iletişim kutusu); iptalde null. Testte ve araçta değiştirilebilir.</summary>
    public Func<string?> PickFolderPath { get; set; }

    /// <summary>Seçici açılırken true, kapanınca false: çentik fare ayrılınca kapanmasın diye.</summary>
    public Action<bool>? HoldOpen { get; set; }

    /// <summary>
    /// Seçicinin sahibi olacak pencere (çentik penceresi). Sahipsiz diyalog Koy ön planda değilken arkada
    /// kalabilir (görev çubuğu gizli, kimse görmez); sahipli diyalog çentiğin üstünde açılır.
    /// Çağrı pencereyi diyalog için etkinleştirebilir. Null dönerse sahipsiz açılır.
    /// </summary>
    public Func<System.Windows.Window?>? ResolveDialogOwner { get; set; }

    public void SetActive(bool active)
    {
        if (_disposed || active == _active)
            return;

        _active = active;
        if (active)
        {
            _service.Changed += OnChanged;
            Reload();
        }
        else
        {
            _service.Changed -= OnChanged;
        }
    }

    private void OnChanged(object? sender, EventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            if (!_disposed && _active)
                Reload();
            return;
        }

        _dispatcher.InvokeAsync(() =>
        {
            if (!_disposed && _active)
                Reload();
        });
    }

    private void Reload()
    {
        Items.Clear();
        var missing = new List<QuickLaunchItemViewModel>();
        foreach (var path in _service.Items)
        {
            var item = new QuickLaunchItemViewModel(path, QuickLaunchService.DisplayName(path));
            if (_icons.TryGetValue(path, out var cached))
                item.Icon = cached;
            else
                missing.Add(item);
            Items.Add(item);
        }

        CanAdd = Items.Count < IQuickLaunchService.MaxItems;
        if (missing.Count > 0)
            _ = LoadIconsAsync(missing);
    }

    private async Task LoadIconsAsync(List<QuickLaunchItemViewModel> items)
    {
        foreach (var item in items)
        {
            var path = item.Path;
            byte[]? bytes = null;
            try
            {
                bytes = await Task.Run(() => IconHelper.GetIconBytes(path));
            }
            catch (Exception ex)
            {
                Koy.Core.Diagnostics.CrashLog.Write("Hızlı başlat simgesi", ex);
            }

            if (_disposed)
                return;

            var image = bytes is { Length: > 0 } ? Decode(bytes) : null;
            _icons[path] = image;
            item.Icon = image;
        }
    }

    private static ImageSource? Decode(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 32;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            Koy.Core.Diagnostics.CrashLog.Write("Hızlı başlat simgesi çözülemedi", ex);
            return null;
        }
    }

    [RelayCommand]
    private void Launch(QuickLaunchItemViewModel? item)
    {
        if (item is not null)
            _service.Launch(item.Path);
    }

    [RelayCommand]
    private void Remove(QuickLaunchItemViewModel? item)
    {
        if (item is null)
            return;

        _service.Remove(item.Path);
        _icons.Remove(item.Path);
    }

    [RelayCommand]
    private void Add()
    {
        HoldOpen?.Invoke(true);
        try
        {
            var path = PickPath();
            if (!string.IsNullOrWhiteSpace(path))
                _service.Add(path);
        }
        finally
        {
            HoldOpen?.Invoke(false);
        }
    }

    [RelayCommand]
    private void AddFolder()
    {
        HoldOpen?.Invoke(true);
        try
        {
            var path = PickFolderPath();
            if (!string.IsNullOrWhiteSpace(path))
                _service.Add(path);
        }
        finally
        {
            HoldOpen?.Invoke(false);
        }
    }

    private string? DefaultPick()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = Strings.Get("QuickLaunchPickTitle"),
            Filter = Strings.Get("QuickLaunchPickFilter"),
            CheckFileExists = true,
            DereferenceLinks = false
        };

        var owner = ResolveDialogOwner?.Invoke();
        var accepted = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return accepted == true ? dialog.FileName : null;
    }

    private string? DefaultPickFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = Strings.Get("QuickLaunchPickFolderTitle"),
            Multiselect = false
        };

        var owner = ResolveDialogOwner?.Invoke();
        var accepted = owner is not null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
        return accepted == true ? dialog.FolderName : null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        SetActive(false);
        _disposed = true;
    }
}
