using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Koy.Core.Notch;

namespace Koy.App.Features.Home;

public partial class HomePanel : UserControl
{
    public HomePanel()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateRightColumnOrder();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is HomeViewModel oldVm)
            oldVm.PropertyChanged -= OnHomeViewModelPropertyChanged;

        if (e.NewValue is HomeViewModel newVm)
            newVm.PropertyChanged += OnHomeViewModelPropertyChanged;

        UpdateRightColumnOrder();
    }

    private void OnHomeViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.Cards))
            UpdateRightColumnOrder();
    }

    private void UpdateRightColumnOrder()
    {
        if (DataContext is not HomeViewModel vm || RightColumnGrid is null ||
            HeadphoneCardBorder is null || SystemCardGrid is null || QuickLaunchCardBorder is null)
            return;

        var items = new List<(FrameworkElement Element, bool IsStar, int Order)>
        {
            (HeadphoneCardBorder, false, GetCardOrder(vm.Cards, HomeCard.HeadphoneBattery)),
            (SystemCardGrid, true, GetCardOrder(vm.Cards, HomeCard.SystemNetwork)),
            (QuickLaunchCardBorder, false, GetCardOrder(vm.Cards, HomeCard.QuickLaunch))
        };

        items.Sort((a, b) => a.Order.CompareTo(b.Order));

        RightColumnGrid.RowDefinitions.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            RightColumnGrid.RowDefinitions.Add(new RowDefinition
            {
                Height = item.IsStar ? new GridLength(1, GridUnitType.Star) : GridLength.Auto
            });
            Grid.SetRow(item.Element, i);

            var isLast = i == items.Count - 1;
            item.Element.Margin = new Thickness(0, 0, 0, isLast ? 0 : 8);
        }
    }

    private static int GetCardOrder(IReadOnlyList<HomeCard> cards, HomeCard target)
    {
        for (var i = 0; i < cards.Count; i++)
        {
            if (cards[i] == target)
                return i;
        }
        return 999;
    }

    private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ProgressBar bar && DataContext is HomeViewModel vm)
        {
            var fraction = bar.ActualWidth > 0 ? e.GetPosition(bar).X / bar.ActualWidth : 0;
            vm.Music.SeekCommand.Execute(fraction);
            // Atlama tıklaması karta ulaşıp kartı büyütmesin, çentiğe ulaşıp onu kapatmasın.
            e.Handled = true;
        }
    }

    // Müzik kartına tıklanınca büyür / eski boyutuna döner. Düğme ve ilerleme çubuğu kendi işini yapar.
    // Olay işlenmiş sayılır: yoksa çentik penceresi tıklamayı "boşluğa tıklandı" sayıp çentiği kapatırdı.
    private void MusicCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not HomeViewModel vm || !vm.ShowMusic)
            return;

        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, sender); node = ParentOf(node))
        {
            if (node is ButtonBase or RangeBase)
                return;
        }

        vm.ToggleMusicExpandedCommand.Execute(null);
        e.Handled = true;
    }

    private bool _isAddMenuHeld;

    private void AddButton_ContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (_isAddMenuHeld)
            return;

        if (DataContext is HomeViewModel vm)
        {
            _isAddMenuHeld = true;
            vm.QuickLaunch.HoldOpen?.Invoke(true);
        }
    }

    private void AddButton_ContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (!_isAddMenuHeld)
            return;

        _isAddMenuHeld = false;
        if (DataContext is HomeViewModel vm)
        {
            vm.QuickLaunch.HoldOpen?.Invoke(false);
        }
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
