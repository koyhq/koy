using System.Windows;
using System.Windows.Controls;

namespace Koy.App.Features.Dictation;

public partial class DictationSettingsView : UserControl
{
    private readonly DictationSettingsViewModel _viewModel = new();

    public DictationSettingsView()
    {
        InitializeComponent();
        DataContext = _viewModel;
        KeyBox.Password = _viewModel.ApiKey;
    }

    // PasswordBox'a bağlanılamaz (güvenlik gereği); değişiklik elle aktarılır.
    private void KeyBox_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.ApiKey = KeyBox.Password;
}
