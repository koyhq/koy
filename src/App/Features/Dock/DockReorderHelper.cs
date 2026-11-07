using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Koy.Core.Dock;

namespace Koy.App.Features.Dock;

/// <summary>
/// Dock simgesini fareyle sürükleyip sıralamayı yöneten fare mantığı (yüzen kapsül ve tam genişlik görünümü paylaşır).
/// Sürükleme eşiği aşılmadan tıklama normal çalışır; eşik aşılınca fare yakalanır, bırakınca tıklama yutulur.
/// Hesaplar Core'daki <see cref="DockReorderMath"/>, kayma yayları <see cref="DockViewModel"/> içindedir.
/// Sürükleme sırasında başka bir uygulamanın üzerinde ~400 ms duraklama (Merge bölgesi) hedefi büyütür (1.1x)
/// ve parlatır; bırakınca klasör oluşturur veya klasöre ekler.
/// </summary>
internal sealed class DockReorderHelper
{
    private readonly Func<DockViewModel?> _viewModel;
    private readonly DockDropZoneTracker _dropZoneTracker = new();
    private readonly DispatcherTimer _mergeHoverTimer;

    private FrameworkElement? _element;
    private DockEntryViewModel? _item;
    private DockEntryViewModel? _glowingEntry;
    private Point _start;
    private bool _dragging;
    private int _originalIndex;
    private double _slotWidth;
    private double _lastOffset;

    public DockReorderHelper(Func<DockViewModel?> viewModel)
    {
        _viewModel = viewModel;
        _mergeHoverTimer = new DispatcherTimer
        {
            Interval = DockDropZones.MergeHoverDelay
        };
        _mergeHoverTimer.Tick += OnMergeHoverTimerTick;
    }

    public void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            sender is not FrameworkElement { DataContext: DockEntryViewModel item } element ||
            _viewModel() is not { } vm || !vm.CanStartReorder(item))
            return;

        _element = element;
        _item = item;
        _dragging = false;
        _start = Position(e);
    }

    public void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_item is null || _element is null || _viewModel() is not { } vm)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            Reset(vm, cancel: _dragging);
            return;
        }

        var position = Position(e);
        if (!_dragging)
        {
            if (!DockReorderMath.ExceedsThreshold(
                    position.X - _start.X, position.Y - _start.Y, SystemParameters.MinimumHorizontalDragDistance))
                return;

            _originalIndex = vm.Items.IndexOf(_item);
            if (_originalIndex < 0)
            {
                Reset(vm, cancel: false);
                return;
            }

            _slotWidth = _element.ActualWidth + _element.Margin.Left + _element.Margin.Right;
            _dragging = true;
            _element.CaptureMouse();
            vm.BeginReorder();
        }

        // İmleç yalnız sabitli yuvalar arasında gezer: sınırın ötesine kayma kırpılır.
        var count = vm.PinnedCount;
        var min = -_originalIndex * _slotWidth;
        var max = (count - 1 - _originalIndex) * _slotWidth;
        _lastOffset = Math.Clamp(position.X - _start.X, min, max);

        var targetIndex = DockReorderMath.TargetIndex(count, _originalIndex, _lastOffset, _slotWidth);

        // Yalnızca normal uygulamalar başka bir uygulama veya klasörle birleştirilebilir (klasörler iç içe geçmez).
        var canMerge = _item is DockItemViewModel &&
                       targetIndex != _originalIndex &&
                       targetIndex >= 0 &&
                       targetIndex < vm.Items.Count;

        if (canMerge)
        {
            var relativeX = (_slotWidth * 0.5) + (_lastOffset - (targetIndex - _originalIndex) * _slotWidth);
            var zone = DockDropZones.DetectZone(_slotWidth, relativeX, 0.5);

            if (zone == DockDropZone.Merge)
            {
                var now = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
                var armed = _dropZoneTracker.Update(targetIndex, zone, now);

                if (armed)
                {
                    _mergeHoverTimer.Stop();
                    ArmMerge(vm, targetIndex);
                    return;
                }

                if (!_mergeHoverTimer.IsEnabled)
                {
                    _mergeHoverTimer.Start();
                }
            }
            else
            {
                _mergeHoverTimer.Stop();
                _dropZoneTracker.Reset();
                ClearGlow();
            }
        }
        else
        {
            _mergeHoverTimer.Stop();
            _dropZoneTracker.Reset();
            ClearGlow();
        }

        if (!_dropZoneTracker.IsMergeArmed)
        {
            vm.UpdateReorder(_item, _originalIndex, _lastOffset, _slotWidth);
        }
    }

    private void OnMergeHoverTimerTick(object? sender, EventArgs e)
    {
        _mergeHoverTimer.Stop();
        if (!_dragging || _item is null || _viewModel() is not { } vm)
            return;

        var count = vm.PinnedCount;
        var targetIndex = DockReorderMath.TargetIndex(count, _originalIndex, _lastOffset, _slotWidth);
        if (targetIndex != _originalIndex && targetIndex >= 0 && targetIndex < vm.Items.Count)
        {
            var now = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            _dropZoneTracker.Update(targetIndex, DockDropZone.Merge, now);
            ArmMerge(vm, targetIndex);
        }
    }

    private void ArmMerge(DockViewModel vm, int targetIndex)
    {
        if (targetIndex < 0 || targetIndex >= vm.Items.Count)
            return;

        var targetEntry = vm.Items[targetIndex];
        if (_glowingEntry != targetEntry)
        {
            ClearGlow();
            _glowingEntry = targetEntry;
            _glowingEntry.IsTargetGlow = true;
            _glowingEntry.SetTargetScale(1.1);
        }

        // Birleştirme hedeflendiğinde diğer simgelerin yer değiştirmesini durdur, sürüklenen imleçte kalsın
        _item?.SnapOffset(_lastOffset);
        for (var i = 0; i < vm.PinnedCount && i < vm.Items.Count; i++)
        {
            if (i != _originalIndex)
                vm.Items[i].SetTargetOffset(0);
        }
    }

    /// <summary>Bırakma: sürükleme sürmüşse tamamlar ve true döner (çağıran tıklamayı işlemez).</summary>
    public bool TryFinish()
    {
        _mergeHoverTimer.Stop();

        if (_item is null)
            return false;

        var wasDragging = _dragging;
        var vm = _viewModel();
        if (wasDragging && vm is not null)
        {
            var item = _item;
            var element = _element;
            var armed = _dropZoneTracker.IsMergeArmed;
            var targetSlot = _dropZoneTracker.TargetSlot;

            ClearGlow();
            Clear();
            element?.ReleaseMouseCapture();

            if (armed && item is DockItemViewModel draggedApp &&
                targetSlot >= 0 && targetSlot < vm.Items.Count && targetSlot != _originalIndex)
            {
                var targetEntry = vm.Items[targetSlot];
                if (targetEntry is DockFolderViewModel targetFolder)
                {
                    vm.AddAppToFolder(draggedApp.Model.Id, targetFolder.Folder.Id);
                }
                else if (targetEntry is DockItemViewModel targetApp)
                {
                    vm.CreateFolderFromDrop(draggedApp.Model.Id, targetApp.Model.Id);
                }
                return true;
            }

            vm.CommitReorder(item, _originalIndex, _lastOffset, _slotWidth);
            return true;
        }

        ClearGlow();
        Clear();
        return false;
    }

    /// <summary>Fare yakalaması başka yere geçtiyse sürükleme iptal edilir.</summary>
    public void OnLostCapture()
    {
        if (_dragging && _viewModel() is { } vm)
            Reset(vm, cancel: true);
    }

    private void Reset(DockViewModel vm, bool cancel)
    {
        _mergeHoverTimer.Stop();
        var element = _element;
        ClearGlow();
        Clear();
        element?.ReleaseMouseCapture();
        if (cancel)
            vm.CancelReorder();
    }

    private void Clear()
    {
        _element = null;
        _item = null;
        _dragging = false;
        _dropZoneTracker.Reset();
    }

    private void ClearGlow()
    {
        if (_glowingEntry is not null)
        {
            _glowingEntry.IsTargetGlow = false;
            _glowingEntry.SetTargetScale(1.0);
            _glowingEntry = null;
        }
    }

    private Point Position(MouseEventArgs e)
        => e.GetPosition(_element is null ? null : Window.GetWindow(_element));
}
