using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using Koy.Core.Dictation;
using Koy.Platform.Dictation;

namespace Koy.App.Features.Dictation;

/// <summary>
/// Metni panoya koyar, Ctrl+V basar, eski panoyu geri koyar. Pano yalnız arayüz iş parçacığından kullanılır; başka bir program
/// panoyu kısa süre tutabilir, bu yüzden açma denemeleri birkaç kez tekrarlanır. Geri yükleme metin, resim ve dosya listesi içindir;
/// başka biçimdeki (biçimli içerik, özel biçimler) pano geri getirilemez, yalnız boşaltılmaz ve bu durumda yapıştırma sonrası
/// metin panoda kalır.
/// </summary>
public sealed class WpfPasteService : IPasteService
{
    private const int Attempts = 6;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(40);

    // Hedef program panoyu okur okumaz geri yüklersek eski içerik yapıştırılır; çoğu uygulama birkaç on ms'de okur.
    private static readonly TimeSpan ReadGrace = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ModifierWait = TimeSpan.FromSeconds(1);

    private sealed record Snapshot(string? Text, BitmapSource? Image, StringCollection? Files, bool Empty);

    public async Task PasteAsync(string text, bool restoreClipboard, CancellationToken cancellationToken)
    {
        var snapshot = restoreClipboard ? await OnUiAsync(TakeSnapshot, cancellationToken).ConfigureAwait(true) : null;

        await OnUiAsync(() => Clipboard.SetText(text), cancellationToken).ConfigureAwait(true);

        // Ctrl+Alt+H gibi kısayolun tuşları hâlâ basılıysa Ctrl+V yerine yanlış kombinasyon giderdi.
        var waited = TimeSpan.Zero;
        while (!PasteKeys.ModifiersReleased() && waited < ModifierWait)
        {
            await Task.Delay(20, cancellationToken).ConfigureAwait(true);
            waited += TimeSpan.FromMilliseconds(20);
        }

        PasteKeys.SendCtrlV();
        await Task.Delay(ReadGrace, cancellationToken).ConfigureAwait(true);

        if (snapshot is not null)
            await OnUiAsync(() => Restore(snapshot), cancellationToken).ConfigureAwait(true);
    }

    public Task CopyAsync(string text, CancellationToken cancellationToken) =>
        OnUiAsync(() => Clipboard.SetText(text), cancellationToken);

    private static Snapshot TakeSnapshot()
    {
        if (Clipboard.ContainsText())
            return new Snapshot(Clipboard.GetText(), null, null, false);
        if (Clipboard.ContainsImage())
            return new Snapshot(null, Clipboard.GetImage(), null, false);
        if (Clipboard.ContainsFileDropList())
            return new Snapshot(null, null, Clipboard.GetFileDropList(), false);

        var formats = Clipboard.GetDataObject()?.GetFormats();
        return new Snapshot(null, null, null, formats is null or { Length: 0 });
    }

    private static void Restore(Snapshot snapshot)
    {
        if (snapshot.Text is not null)
            Clipboard.SetText(snapshot.Text);
        else if (snapshot.Image is not null)
            Clipboard.SetImage(snapshot.Image);
        else if (snapshot.Files is not null)
            Clipboard.SetFileDropList(snapshot.Files);
        else if (snapshot.Empty)
            Clipboard.Clear();
    }

    private static async Task OnUiAsync(Action action, CancellationToken cancellationToken)
    {
        await OnUiAsync(() =>
        {
            action();
            return true;
        }, cancellationToken).ConfigureAwait(true);
    }

    private static async Task<T> OnUiAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        var dispatcher = Application.Current.Dispatcher;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await dispatcher.InvokeAsync(action);
            }
            catch (COMException) when (attempt < Attempts)
            {
                // CLIPBRD_E_CANT_OPEN: pano başka bir programda açık; kısa süre sonra tekrar.
                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(true);
            }
        }
    }
}
