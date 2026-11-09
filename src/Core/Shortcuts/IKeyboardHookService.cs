namespace Koy.Core.Shortcuts;

/// <summary>
/// RegisterHotKey'in yapamadığı kısayollar için klavye kancası sözleşmesi: yalnız değiştiriciler (tek başına Win) ve
/// Windows'un kendi kısayolları. Kanca yalnız böyle bir kısayol atanmışken ya da yakalama sürerken kuruludur (AGENTS #5).
/// </summary>
public interface IKeyboardHookService : IDisposable
{
    /// <summary>Kancayla izlenen bir kısayola basıldı; değer <see cref="SetBindings"/>'e verilen kimliktir.</summary>
    event EventHandler<int>? HotkeyPressed;

    /// <summary>İzlenecek kısayolları değiştirir; boşsa kanca kaldırılır. Kanca kurulamazsa false döner.</summary>
    bool SetBindings(IReadOnlyDictionary<int, Hotkey> bindings);

    /// <summary>Bir sonraki kombinasyonu yakalar, tuşlar Windows'a gitmez. Kanca kurulamazsa false döner.</summary>
    bool BeginCapture(Action<Hotkey> onCaptured);

    void EndCapture();
}
