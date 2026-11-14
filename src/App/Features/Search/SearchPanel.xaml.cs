using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Koy.Core.Search;

namespace Koy.App.Features.Search;

public partial class SearchPanel : UserControl
{
    /// <summary>Yardım düğmesinin simgesi.</summary>
    public static SearchGlyph HelpGlyph => SearchGlyph.Help;

    private SearchViewModel? _viewModel;
    private bool _allowCompletion;
    private bool _completing;
    private Point _dragStart;
    private SearchRowViewModel? _dragRow;

    public SearchPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>Yazı kutusuna odaklanır; geri gelen son arama seçili olur, yazmaya başlayınca silinir.</summary>
    public void FocusBox()
    {
        Box.Focus();
        Keyboard.Focus(Box);
        Box.SelectAll();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CaretRequested -= OnCaretRequested;
            _viewModel.SelectionChanged -= OnSelectionChanged;
        }

        _viewModel = e.NewValue as SearchViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CaretRequested += OnCaretRequested;
            _viewModel.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnCaretRequested(int caret) => Dispatcher.BeginInvoke(() => Box.CaretIndex = Math.Min(caret, Box.Text.Length));

    private void OnSelectionChanged(int index) => Dispatcher.BeginInvoke(() =>
    {
        if (ResultList.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
            container.BringIntoView();
    }, System.Windows.Threading.DispatcherPriority.Loaded);

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_completing || _viewModel is not { HistoryMode: SearchHistoryMode.ImmediateCompletion } viewModel || !_allowCompletion)
            return;

        // Yazılanı geçmişten tamamlar; eklenen kısım seçili kalır, yazmaya devam edince silinir.
        var typed = Box.Text;
        var suggestion = viewModel.Suggestion;
        if (typed.Length == 0 || suggestion.Length <= typed.Length)
            return;

        _completing = true;
        Box.Text = typed + suggestion[typed.Length..];
        Box.Select(typed.Length, Box.Text.Length - typed.Length);
        _completing = false;
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm)
            return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var atEnd = Box.CaretIndex == Box.Text.Length && Box.SelectionLength == 0;
        var atStart = Box.CaretIndex == 0 && Box.SelectionLength == 0;
        _allowCompletion = e.Key is not (Key.Back or Key.Delete);

        switch (e.Key)
        {
            case Key.Escape:
                vm.RequestCloseCommand.Execute(null);
                break;
            case Key.Enter:
                vm.Activate(firstAction: shift);
                break;
            case Key.Up when ctrl:
            case Key.PageUp:
                vm.MoveCategory(-1);
                break;
            case Key.Down when ctrl:
            case Key.PageDown:
                vm.MoveCategory(1);
                break;
            case Key.Up:
            case Key.K when ctrl:
                vm.MoveSelection(-1);
                break;
            case Key.Down:
            case Key.J when ctrl:
                vm.ShowHistoryOrMoveDown();
                break;
            case Key.Home when atStart:
                vm.SelectFirst();
                break;
            case Key.End when atEnd:
                vm.SelectLast();
                break;
            case Key.Right when atEnd && vm.HistoryMode == SearchHistoryMode.Suggestion && vm.AcceptSuggestion():
                break;
            case Key.Tab:
                HandleTab(vm, shift);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    private static void HandleTab(SearchViewModel vm, bool shift)
    {
        if (!shift && vm.HistoryMode == SearchHistoryMode.Suggestion && vm.AcceptSuggestion())
            return;

        var direction = shift ? -1 : 1;
        if (vm.CycleAction(direction))
            return;

        vm.MoveSelection(direction);
        if (shift)
            vm.FocusLastAction();
    }

    private void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && IsInsideButton(source))
        {
            _dragRow = null;
            return;
        }

        if (sender is FrameworkElement { DataContext: SearchRowViewModel row })
        {
            _dragStart = e.GetPosition(null);
            _dragRow = row;
        }
    }

    private void Row_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRow is not { } row || row.Result.Drag is not { Kind: not SearchDragKind.None } drag)
            return;

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _dragRow = null;
        if (sender is not DependencyObject source)
            return;

        var data = BuildDragData(drag);
        var effects = drag.Kind == SearchDragKind.File
            ? DragDropEffects.Copy | DragDropEffects.Move
            : DragDropEffects.Copy | DragDropEffects.Link;
        DragDrop.DoDragDrop(source, data, effects);
    }

    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragRow = null;
        if (_viewModel is not null && sender is FrameworkElement { DataContext: SearchRowViewModel row })
        {
            _viewModel.ActivateRow(row);
            e.Handled = true;
        }
    }

    private static DataObject BuildDragData(SearchDragData drag)
    {
        var data = new DataObject();
        switch (drag.Kind)
        {
            case SearchDragKind.File:
                data.SetData(DataFormats.FileDrop, new[] { drag.Value });
                break;
            case SearchDragKind.Url:
            case SearchDragKind.Text:
                data.SetData(DataFormats.UnicodeText, drag.Value);
                break;
        }

        return data;
    }

    private static bool IsInsideButton(DependencyObject start)
    {
        for (var current = start; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is Button)
                return true;
        }

        return false;
    }

    private void RowAction_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement { DataContext: SearchActionViewModel action } button)
            return;

        var row = FindRow(button);
        if (row is not null)
            _viewModel.RunRowAction(row, action);
        e.Handled = true;
    }

    private static SearchRowViewModel? FindRow(DependencyObject start)
    {
        for (var current = start; current is not null; current = System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { DataContext: SearchRowViewModel row })
                return row;
        }

        return null;
    }
}
