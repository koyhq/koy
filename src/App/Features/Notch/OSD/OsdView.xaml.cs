using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Koy.App.Features.Notch.OSD;

public partial class OsdView : UserControl
{
    public OsdView()
    {
        InitializeComponent();
    }

    /// <summary>Komut kutusuna klavye odağını verir (sesle yazma odaktaki kutuya yazar).</summary>
    public void FocusVoiceInput()
    {
        // Kutu görünür olduktan sonra (bağlama güncellendikten sonra) odaklanır.
        Dispatcher.BeginInvoke(() =>
        {
            VoiceInputBox.Focus();
            Keyboard.Focus(VoiceInputBox);
            VoiceInputBox.CaretIndex = VoiceInputBox.Text.Length;
        }, DispatcherPriority.Input);
    }
}
