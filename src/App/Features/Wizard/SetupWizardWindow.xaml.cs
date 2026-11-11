using System.Windows;
using System.Windows.Input;

namespace Koy.App.Features.Wizard;

public partial class SetupWizardWindow : Window
{
    public SetupWizardViewModel ViewModel => (SetupWizardViewModel)DataContext;

    public SetupWizardWindow(SetupWizardViewModel? viewModel = null)
    {
        InitializeComponent();
        // Gerçek bulanıklık, koyu çerçeve, gölge ve köşeler DWM'den (Koy penceresiyle aynı).
        Koy.App.Resources.WindowFrame.Attach(this);
        DataContext = viewModel ?? new SetupWizardViewModel();

        ViewModel.Completed += () =>
        {
            DialogResult = true;
            Close();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Card0_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.SelectedLayoutIndex = 0;
    }

    private void Card1_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.SelectedLayoutIndex = 1;
    }

    private void Card2_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.SelectedLayoutIndex = 2;
    }

    private void LocationYes_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.UseLocation = true;
    }

    private void LocationNo_Click(object sender, MouseButtonEventArgs e)
    {
        ViewModel.UseLocation = false;
    }
}
