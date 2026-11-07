using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Koy.App.Resources;
using Koy.Core.Animation;
using Koy.Platform.Shell;

namespace Koy.App.Features.Dock.Folder;

/// <summary>
/// Dock klasörünün içine tıklandığında açılan kayan cam panel.
/// Yay fiziğiyle açılır/kapanır; dışarı tıklamada veya Esc tuşunda kapanır.
/// Başlık metnine tıklanarak doğrudan isim değiştirilebilir.
/// </summary>
public partial class FolderPanel : UserControl
{
    private const double ShiftDip = 8.0;
    private const double ScaleFrom = 0.96;

    public static readonly DependencyProperty PlacementTargetProperty =
        DependencyProperty.Register(
            nameof(PlacementTarget),
            typeof(UIElement),
            typeof(FolderPanel),
            new PropertyMetadata(null));

    public UIElement? PlacementTarget
    {
        get => (UIElement?)GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    /// <summary>UiSnapshots için görsel kutu gövdesi erişimi.</summary>
    public Border PanelBody => PopupBody;
    public ItemsControl AppsList => AppsItemsControl;

    /// <summary>UiSnapshots için bağımsız görsel gövde üretir (Popup'tan ayırır).</summary>
    public Border DetachBodyForSnapshot()
    {
        FolderPopup.Child = null;
        if (ViewModel is not null)
            AppsItemsControl.ItemsSource = ViewModel.Items;
        return PopupBody;
    }

    private DockFolderViewModel? _subscribed;
    private readonly Spring _spring = new(0) { Stiffness = 520, Damping = 38 };
    private bool _renderHooked;
    private TimeSpan _lastRender = TimeSpan.MinValue;
    private bool _closing;
    private bool _isEditing;

    private Point _appDragStart;
    private DockItemViewModel? _draggedItem;

    public FolderPanel()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Subscribe();
        IsVisibleChanged += (_, _) => SyncPopup();
        FolderPopup.Closed += OnPopupClosed;
        Unloaded += (_, _) => StopAll();
    }

    private DockFolderViewModel? ViewModel => DataContext as DockFolderViewModel;

    private void Subscribe()
    {
        if (_subscribed is not null)
            _subscribed.PropertyChanged -= OnViewModelPropertyChanged;

        _subscribed = ViewModel;
        if (_subscribed is not null)
            _subscribed.PropertyChanged += OnViewModelPropertyChanged;

        SyncPopup();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockFolderViewModel.IsFolderPanelOpen))
        {
            SyncPopup();
        }
        else if (e.PropertyName == nameof(DockFolderViewModel.IsEditingTitle))
        {
            if (ViewModel?.IsEditingTitle == true && !_isEditing)
                StartEdit();
            else if (ViewModel?.IsEditingTitle == false && _isEditing)
                FinishEdit(save: false);
        }
    }

    private void SyncPopup()
    {
        var wantOpen = IsVisible && ViewModel?.IsFolderPanelOpen == true;
        if (wantOpen)
            Open();
        else
            Close();
    }

    private bool AnimationsEnabled => Motion.Enabled && ViewModel?.OwnerViewModel?.ReduceMotion != true;

    private void Open()
    {
        _closing = false;
        if (!FolderPopup.IsOpen)
        {
            ApplyVisual(AnimationsEnabled ? 0 : 1);
            _spring.SnapTo(AnimationsEnabled ? 0 : 1);
            FolderPopup.IsOpen = true;

            Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(PopupBody, OnOutsideClick);
            Window.GetWindow(this)?.AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnGlobalKeyDown), true);
        }

        if (AnimationsEnabled)
        {
            _spring.Target = 1;
            StartRendering();
        }
        else
        {
            _spring.SnapTo(1);
            ApplyVisual(1);
        }

        if (ViewModel?.IsEditingTitle == true)
        {
            StartEdit();
        }
    }

    private void Close()
    {
        if (!FolderPopup.IsOpen || _closing)
            return;

        if (_isEditing)
            FinishEdit(save: true);

        if (!AnimationsEnabled || !IsVisible)
        {
            FinishClose();
            return;
        }

        _closing = true;
        _spring.Target = 0;
        StartRendering();
    }

    private void FinishClose()
    {
        _closing = false;
        StopRendering();
        Mouse.RemovePreviewMouseDownOutsideCapturedElementHandler(PopupBody, OnOutsideClick);
        Window.GetWindow(this)?.RemoveHandler(PreviewKeyDownEvent, new KeyEventHandler(OnGlobalKeyDown));
        FolderPopup.IsOpen = false;
    }

    private void StopAll()
    {
        StopRendering();
        _closing = false;
        if (_isEditing)
            FinishEdit(save: false);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (ViewModel is { IsFolderPanelOpen: true } vm)
            vm.CloseFolderPanel();
    }

    private void OnOutsideClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.CloseFolderPanel();
        }
        else
        {
            Close();
        }
    }

    private void OnGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isEditing)
            {
                FinishEdit(save: false);
            }
            else
            {
                ViewModel?.CloseFolderPanel();
            }
            e.Handled = true;
        }
    }

    private void StartRendering()
    {
        if (_renderHooked) return;
        _renderHooked = true;
        _lastRender = TimeSpan.MinValue;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopRendering()
    {
        if (!_renderHooked) return;
        _renderHooked = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = (e as RenderingEventArgs)?.RenderingTime ?? TimeSpan.Zero;
        var dt = _lastRender == TimeSpan.MinValue ? 1.0 / 60.0 : Math.Clamp((now - _lastRender).TotalSeconds, 0.001, 0.05);
        _lastRender = now;

        _spring.Step(dt);
        ApplyVisual(_spring.Value);

        if (_spring.IsAtRest)
        {
            StopRendering();
            if (_closing)
                FinishClose();
        }
    }

    private void ApplyVisual(double progress)
    {
        var clamped = Math.Clamp(progress, 0.0, 1.0);
        PopupBody.Opacity = clamped;

        var scale = ScaleFrom + (1.0 - ScaleFrom) * clamped;
        PopupScale.ScaleX = scale;
        PopupScale.ScaleY = scale;

        PopupShift.Y = ShiftDip * (1.0 - clamped);
    }

    #region Başlık Düzenleme (Inline Rename)

    private void OnTitleClick(object sender, MouseButtonEventArgs e)
    {
        StartEdit();
        e.Handled = true;
    }

    private void OnRenameButtonClick(object sender, RoutedEventArgs e)
    {
        StartEdit();
    }

    private void StartEdit()
    {
        if (_isEditing) return;
        _isEditing = true;

        var hwnd = GetWindowHandle();
        if (hwnd != 0)
            WindowStyles.SetNoActivate(hwnd, false);

        TitleTextBlock.Visibility = Visibility.Collapsed;
        TitleTextBox.Visibility = Visibility.Visible;
        TitleTextBox.Text = ViewModel?.Title ?? string.Empty;
        TitleTextBox.Focus();
        TitleTextBox.SelectAll();
    }

    private void OnTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            FinishEdit(save: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            FinishEdit(save: false);
            e.Handled = true;
        }
    }

    private void OnTitleLostFocus(object sender, RoutedEventArgs e)
    {
        if (_isEditing)
            FinishEdit(save: true);
    }

    private void FinishEdit(bool save)
    {
        if (!_isEditing) return;
        _isEditing = false;

        if (save && ViewModel is not null)
        {
            var newName = TitleTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                ViewModel.OwnerViewModel?.RenameFolder(ViewModel, newName);
            }
        }

        TitleTextBox.Visibility = Visibility.Collapsed;
        TitleTextBlock.Visibility = Visibility.Visible;

        if (ViewModel is not null)
            ViewModel.IsEditingTitle = false;

        var hwnd = GetWindowHandle();
        if (hwnd != 0)
            WindowStyles.SetNoActivate(hwnd, true);
    }

    private nint GetWindowHandle()
    {
        var win = Window.GetWindow(this);
        return win is not null ? new WindowInteropHelper(win).Handle : nint.Zero;
    }

    #endregion

    #region Uygulama Tıklama ve Dışarı Sürükleme

    private void OnAppItemLeftDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not Border { DataContext: DockItemViewModel })
        {
            if (source == PopupBody) break;
            source = VisualTreeHelper.GetParent(source);
        }

        if (source is FrameworkElement { DataContext: DockItemViewModel item })
        {
            _draggedItem = item;
            _appDragStart = e.GetPosition(PopupBody);
        }
    }

    private void OnAppItemMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedItem is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var current = e.GetPosition(PopupBody);
        var diff = current - _appDragStart;

        // Panel dışına sürüklendi mi denetle
        if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
        {
            var isOutside = current.X < 0 || current.X > PopupBody.ActualWidth ||
                            current.Y < 0 || current.Y > PopupBody.ActualHeight;

            if (isOutside && ViewModel is not null)
            {
                var item = _draggedItem;
                _draggedItem = null;

                // Klasörden çıkar ve dock'a yerleştir
                ViewModel.OwnerViewModel?.RemoveAppFromFolder(ViewModel.Folder.Id, item.Id, -1);
                ViewModel.CloseFolderPanel();
            }
        }
    }

    private void OnAppItemLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedItem is not null)
        {
            var item = _draggedItem;
            _draggedItem = null;
            item.ClickCommand.Execute(null);
            ViewModel?.CloseFolderPanel();
            e.Handled = true;
        }
    }

    #endregion
}
