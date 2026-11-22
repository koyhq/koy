using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Koy.App.Resources;
using Koy.Core.Undo;

namespace Koy.App.Features.Notch;

/// <summary>
/// Çentiğin "Geri al" şeridi: silme onaysız ve hemen olur, birkaç saniye geri alınabilir (mantık
/// <see cref="UndoBuffer"/>, Core). Bu sınıf yalnız metni ve süre zamanlayıcısını taşır. Zamanlayıcı yalnız şerit
/// görünürken çalışır; çentik kapanır ya da gizlenirse kayıt bırakılır (<see cref="SetActive"/>), boşta zamanlayıcı yoktur (AGENTS #5).
/// Panellerin ViewModel'leri silmeden sonra <see cref="Offer"/> çağırır.
/// </summary>
public sealed partial class UndoToastViewModel : ObservableObject, IDisposable
{
    private readonly UndoBuffer _buffer;
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _timer;
    private bool _active;
    private bool _disposed;

    public UndoToastViewModel(Dispatcher? dispatcher = null, UndoBuffer? buffer = null)
    {
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        _buffer = buffer ?? new UndoBuffer();
        _buffer.Changed += OnBufferChanged;
    }

    /// <summary>Şerit görünür mü.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>"Pano temizlendi" gibi.</summary>
    [ObservableProperty]
    private string _message = "";

    /// <summary>
    /// Silmeyi geri alınabilir yapar. Çentik görünür değilse (sayfa görünmezken silme olmaz, ama kayıt tutulmasın)
    /// şerit açılmaz ve kayıt düşer.
    /// </summary>
    public void Offer(UndoKind kind, int count, Action undo)
    {
        if (_disposed || !_active)
            return;

        _buffer.Push(kind, count, undo);
    }

    /// <summary>Çentik açık ve görünürken true; değilken kayıt bırakılır, zamanlayıcı durur.</summary>
    public void SetActive(bool active)
    {
        if (_active == active)
            return;

        _active = active;
        if (!active)
            _buffer.Clear();
    }

    [RelayCommand]
    private void Undo() => _buffer.TryUndo();

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        if (_buffer.Current is { } entry)
        {
            Message = Strings.Get(KeyFor(entry.Kind));
            IsVisible = true;
            StartTimer(_buffer.Remaining);
        }
        else
        {
            IsVisible = false;
            StopTimer();
        }
    }

    private void StartTimer(TimeSpan remaining)
    {
        _timer ??= CreateTimer();
        _timer.Stop();
        _timer.Interval = remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1);
        _timer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _buffer.ExpireIfDue();
        };
        return timer;
    }

    private void StopTimer() => _timer?.Stop();

    private static string KeyFor(UndoKind kind) => kind switch
    {
        UndoKind.ClipboardCleared => "UndoClipboardCleared",
        UndoKind.ClipboardItemRemoved => "UndoClipboardItemRemoved",
        UndoKind.ShelfCleared => "UndoShelfCleared",
        UndoKind.ShelfItemRemoved => "UndoShelfItemRemoved",
        UndoKind.EventDeleted => "UndoEventDeleted",
        UndoKind.TaskDeleted => "UndoTaskDeleted",
        _ => "UndoAction"
    };

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _buffer.Changed -= OnBufferChanged;
        StopTimer();
        _timer = null;
    }
}
