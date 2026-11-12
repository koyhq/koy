using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Koy.App.Features.Settings.Pages;

public partial class GelismisPage : UserControl
{
    public GelismisPage()
    {
        InitializeComponent();
    }

    // Metin değeri yazarken her tuşta değil, odak kaybında ya da Enter'da doğrulanıp yazılır.
    private void ValueBox_LostFocus(object sender, RoutedEventArgs e) => Commit(sender);

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Commit(sender);
            e.Handled = true;
        }
    }

    private static void Commit(object sender)
    {
        if ((sender as FrameworkElement)?.DataContext is AdvancedSettingRow row)
            row.CommitCommand.Execute(null);
    }
}
