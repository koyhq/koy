using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Settings.Pages.SubPages;

public partial class CentikBehaviorSubPage : UserControl
{
    public CentikBehaviorSubPage()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(EdgeLineThicknessBox, OnEdgeLineThicknessPaste);
    }

    // Kalınlık kutusu yalnız rakam alır; değer odak gidince ya da Enter'la uygulanır (her tuşta çentik yeniden kurulmasın).
    private void EdgeLineThicknessBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private static void OnEdgeLineThicknessPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is not string text || !text.All(char.IsAsciiDigit))
            e.CancelCommand();
    }

    private void EdgeLineThicknessBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box)
            return;

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        RestoreIfInvalid(box);
        box.SelectAll();
        e.Handled = true;
    }

    // Boş bırakılan kutu ayara yazılamaz; bağlama hatası kalırsa kutu geçerli son değeri yeniden gösterir.
    private void EdgeLineThicknessBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
            Dispatcher.BeginInvoke(() => RestoreIfInvalid(box));
    }

    private static void RestoreIfInvalid(TextBox box)
    {
        if (Validation.GetHasError(box))
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
    }
}
