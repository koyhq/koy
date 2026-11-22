using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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
    private DockDragPlan _lastPlan;
    private IReadOnlyList<double> _widths = [];

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
            _viewModel() is not { } vm)
            return;

        // Basma geri bildirimi sıralanabilir olmasa da (tek sabitli, sabitsiz çalışan uygulama) çalışır.
        ReleasePress();
        _pressedEntry = item;
        vm.SetEntryPressed(item, true);

        if (!vm.CanStartReorder(item))
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
        var isVertical = vm.IsVertical;
        var dragDelta = isVertical ? (position.Y - _start.Y) : (position.X - _start.X);
        var minDragDistance = isVertical ? SystemParameters.MinimumVerticalDragDistance : SystemParameters.MinimumHorizontalDragDistance;

        if (!_dragging)
        {
            if (!DockReorderMath.ExceedsThreshold(
                    position.X - _start.X, position.Y - _start.Y, minDragDistance))
                return;

            _originalIndex = vm.Items.IndexOf(_item);
            if (_originalIndex < 0)
            {
                Reset(vm, cancel: false);
                return;
            }

            _slotWidth = isVertical
                ? (_element.ActualHeight + _element.Margin.Top + _element.Margin.Bottom)
                : (_element.ActualWidth + _element.Margin.Left + _element.Margin.Right);
            _widths = MeasureSlots(vm, _element, isVertical, _slotWidth);
            _dragging = true;
            // Sürükleme başladı: basma geri bildirimi iptal (öğe imleci izlerken içe çökük kalmaz).
            ReleasePress();
            _element.CaptureMouse();
            _lastPlan = new DockDragPlan(_originalIndex, DockDropZone.Merge, false, _originalIndex);
            vm.BeginReorder();
            _item.DragZ = 10;
        }

        // İmleç yalnız sürükleme yuvaları (sabitliler ve ek öğeler) arasında gezer: sınırın ötesine kayma kırpılır.
        var (min, max) = vm.DragOffsetRange(_originalIndex, _widths);
        _lastOffset = Math.Clamp(dragDelta, min, max);

        // Karar Core'da: hedef yuva, bölge ve komşuların kayacağı hedef. Orta bantta kayma donar, hedef kaçmaz.
        var plan = vm.EvaluateDrag(_item, _originalIndex, _lastOffset, _widths);
        _lastPlan = plan;

        if (plan.CanMerge)
        {
            var now = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            var armed = _dropZoneTracker.Update(plan.HoveredIndex, plan.Zone, now);

            if (armed)
            {
                _mergeHoverTimer.Stop();
                ArmMerge(vm, plan.HoveredIndex);
            }
            else if (!_mergeHoverTimer.IsEnabled)
            {
                _mergeHoverTimer.Start();
            }

            // Hedef değiştiyse eskisinin parıltısı kalmaz; yenisi bekleme sonunda büyür.
            if (!_dropZoneTracker.IsMergeArmed)
                ClearGlow();
        }
        else
        {
            _mergeHoverTimer.Stop();
            _dropZoneTracker.Reset();
            ClearGlow();
        }

        // Kayma her harekette güncellenir: bantta Core zaten komşuyu yerinde tutar, kollu hedef de değişmez.
        vm.UpdateReorder(_item, _originalIndex, _lastOffset, plan.ShiftTarget, _slotWidth);
    }

    /// <summary>
    /// Şeritteki her yuvanın sürükleme yönündeki genişliği (kenar boşlukları dahil). Ek öğeler (saat, pil, ayırıcı) simgelerden
    /// farklı genişlikte olduğu için yuvalar eşit varsayılmaz; ölçülemeyen yuva sürüklenenin genişliğini alır.
    /// </summary>
    private static List<double> MeasureSlots(DockViewModel vm, FrameworkElement element, bool vertical, double fallback)
    {
        var widths = new List<double>(vm.Items.Count);
        DependencyObject? parent = element;
        ItemsControl? list = null;
        while (parent is not null && list is null)
        {
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
            list = parent as ItemsControl;
        }

        for (var i = 0; i < vm.Items.Count; i++)
        {
            var container = list?.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
            var size = container is null ? 0 : vertical ? container.ActualHeight : container.ActualWidth;
            widths.Add(size > 0 ? size : fallback);
        }
        return widths;
    }

    private void OnMergeHoverTimerTick(object? sender, EventArgs e)
    {
        _mergeHoverTimer.Stop();
        if (!_dragging || _item is null || _viewModel() is not { } vm)
            return;

        // Bekleme bitti: imleç hâlâ aynı yuvanın orta bandındaysa hedef silahlanır.
        var plan = vm.EvaluateDrag(_item, _originalIndex, _lastOffset, _widths);
        if (plan.CanMerge)
        {
            var now = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            _dropZoneTracker.Update(plan.HoveredIndex, DockDropZone.Merge, now);
            ArmMerge(vm, plan.HoveredIndex);
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

        // Kayma bantta zaten donuk (DockReorderMath.Evaluate); sürüklenen imleçte kalır, hedef büyüyüp parlar.
        _item?.SnapOffset(_lastOffset);
    }

    /// <summary>Bırakma: sürükleme sürmüşse tamamlar ve true döner (çağıran tıklamayı işlemez).</summary>
    public bool TryFinish()
    {
        _mergeHoverTimer.Stop();
        ReleasePress();

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

            vm.CommitReorder(item, _originalIndex, _lastPlan.ShiftTarget, _widths);
            return true;
        }

        ClearGlow();
        Clear();
        return false;
    }

    /// <summary>Fare yakalaması başka yere geçtiyse sürükleme iptal edilir.</summary>
    public void OnLostCapture()
    {
        ReleasePress();
        if (_dragging && _viewModel() is { } vm)
            Reset(vm, cancel: true);
    }

    /// <summary>Fare öğeden ayrıldı (sürükleme yokken): basılı kalmış görünüm bırakılır, tıklama da zaten gerçekleşmez.</summary>
    public void OnMouseLeave()
    {
        if (!_dragging)
            ReleasePress();
    }

    private DockEntryViewModel? _pressedEntry;

    private void ReleasePress()
    {
        if (_pressedEntry is null)
            return;

        var entry = _pressedEntry;
        _pressedEntry = null;
        _viewModel()?.SetEntryPressed(entry, false);
    }

    private void Reset(DockViewModel vm, bool cancel)
    {
        _mergeHoverTimer.Stop();
        ReleasePress();
        var element = _element;
        ClearGlow();
        Clear();
        element?.ReleaseMouseCapture();
        if (cancel)
            vm.CancelReorder();
    }

    private void Clear()
    {
        if (_item is not null)
            _item.DragZ = 0;
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
