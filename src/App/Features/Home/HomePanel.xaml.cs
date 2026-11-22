using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Koy.App.Features.Music;
using Koy.App.Features.Notch;
using Koy.Core.Notch;

namespace Koy.App.Features.Home;

public partial class HomePanel : UserControl
{
    private readonly TrackTransitionPresenter _trackTransition;

    private readonly StaggeredEntrancePresenter _musicEntrance;

    /// <summary>Müzik yüzündeki parça değişimi geçişi (görsel kanıt aracı ara kareleri buradan çizer).</summary>
    public TrackTransitionPresenter TrackTransition => _trackTransition;

    /// <summary>Müzik yüzünün kademeli girişi (çentik açılınca ya da Müzik sayfasına geçilince).</summary>
    public StaggeredEntrancePresenter MusicEntrance => _musicEntrance;

    public HomePanel()
    {
        InitializeComponent();
        _trackTransition = new TrackTransitionPresenter(MusicFace, IncomingCover, OutgoingCover, TrackTitleBlock);
        // Tasarımdaki sıra (#182): kapak, başlık, dalga/sözler, ilerleme, denetimler.
        _musicEntrance = new StaggeredEntrancePresenter(
            (CoverFrame, 0), (VinylFrame, 0), (TitleRow, 1), (MusicWave, 2), (MusicLyrics, 2), (ProgressRow, 3), (ControlsRow, 4));
        MusicFace.IsVisibleChanged += (_, e) =>
        {
            if (e.NewValue is true)
                _musicEntrance.Play();
            else
                _musicEntrance.Finish();
        };
        Loaded += (_, _) =>
        {
            UpdateRightColumnOrder();
            _trackTransition.Attach((DataContext as HomeViewModel)?.Music);
        };
        // Sayfa şablondan yeniden kurulursa eski örnek müzik olaylarında asılı kalmasın.
        Unloaded += (_, _) => _trackTransition.Attach(null);
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is HomeViewModel oldVm)
            oldVm.PropertyChanged -= OnHomeViewModelPropertyChanged;

        if (e.NewValue is HomeViewModel newVm)
            newVm.PropertyChanged += OnHomeViewModelPropertyChanged;

        if (IsLoaded)
            _trackTransition.Attach((e.NewValue as HomeViewModel)?.Music);

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

    private static double FractionAt(FrameworkElement bar, MouseEventArgs e) =>
        bar.ActualWidth > 0 ? Math.Clamp(e.GetPosition(bar).X / bar.ActualWidth, 0, 1) : 0;

    // Basınca fare yakalanır ve sürükleme başlar; sarma yalnız bırakınca olur (tek tıklama = basıp bırakma, bugünkü davranış).
    // Atlama ya da sürükleme kartı büyütmesin, çentiği kapatmasın: olay işlenmiş sayılır.
    private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement bar || DataContext is not HomeViewModel vm)
            return;

        e.Handled = true;
        if (!vm.Music.BeginScrub(FractionAt(bar, e)))
            return;

        bar.CaptureMouse();
        bar.Focus();
        PlaceScrubVisuals(bar, vm.Music);
    }

    private void ProgressBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { IsMouseCaptured: true } bar && DataContext is HomeViewModel vm)
        {
            vm.Music.MoveScrub(FractionAt(bar, e));
            PlaceScrubVisuals(bar, vm.Music);
            e.Handled = true;
        }
    }

    private void ProgressBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { IsMouseCaptured: true } bar || DataContext is not HomeViewModel vm)
            return;

        // Bırakma konumu son taşımadan farklı olabilir (hızlı tık): önce son kesri işle, sonra bitir.
        vm.Music.MoveScrub(FractionAt(bar, e));
        e.Handled = true;
        _ = vm.Music.EndScrub();
        bar.ReleaseMouseCapture();
    }

    // Yakalama başka bir sebeple kaybolursa (Esc, pencere kaybı) sürükleme iptal; bırakmada EndScrub zaten bitirmiştir.
    private void ProgressBar_LostMouseCapture(object sender, MouseEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            vm.Music.CancelScrub();
    }

    private void ProgressBar_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not HomeViewModel vm)
            return;

        if (e.Key == Key.Escape && vm.Music.IsScrubbing)
        {
            vm.Music.CancelScrub();
            ((FrameworkElement)sender).ReleaseMouseCapture();
            e.Handled = true;
        }
        else if (e.Key is Key.Left or Key.Right && !vm.Music.IsLive)
        {
            _ = vm.Music.StepSeek(e.Key == Key.Left ? -1 : 1);
            e.Handled = true;
        }
    }

    /// <summary>Anlık görüntü aracı için: sürükleme anını (dolu kısım, nokta, süre balonu) fare olmadan kurar.</summary>
    public void ShowScrubForSnapshot(double fraction)
    {
        if (DataContext is not HomeViewModel vm || !vm.Music.BeginScrub(fraction))
            return;

        ScrubHit.UpdateLayout();
        PlaceScrubVisuals(ScrubHit, vm.Music);
    }

    private void PlaceScrubVisuals(FrameworkElement bar, MusicViewModel music)
    {
        var x = music.Progress * bar.ActualWidth;
        ScrubThumbShift.X = Math.Clamp(x - ScrubThumb.Width / 2, 0, Math.Max(0, bar.ActualWidth - ScrubThumb.Width));
        ScrubBalloon.UpdateLayout();
        var w = ScrubBalloon.ActualWidth;
        ScrubBalloonShift.X = Math.Clamp(x - w / 2, 0, Math.Max(0, bar.ActualWidth - w));
    }

    // Müzik kartının boş yerine tıklama işlenmiş sayılır: yoksa çentik penceresi tıklamayı "boşluğa tıklandı" sayıp
    // çentiği kapatırdı. Düğme ve ilerleme çubuğu kendi işini yapar.
    private void MusicCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, sender); node = ParentOf(node))
        {
            if (node is ButtonBase or RangeBase)
                return;
        }

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
