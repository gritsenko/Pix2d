#nullable enable
using System.Collections;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;

namespace Pix2d.Common.Behaviors;

public class ItemsListContextDragBehavior : Behavior<Control>
{
    private Point _dragStartPoint;

    private ItemsControl? _itemsControl;

    private double _thresholdDelta = 3;

    private int _draggedIndex;
    private int _targetIndex;
    private Control _draggedContainer = null!;

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ItemsListContextDragBehavior, Orientation>(nameof(Orientation));

    private int _oldItemZIndex;
    private bool _readyToDrag = false;

    private Timer? _startDragTimer;
    private Point _latestPointerDownPoint;
    private Point _latestMovePoint;
    private bool _waitingForDragStart;

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        _startDragTimer = new Timer(OnStartDragTimerCallback, this, -1, -1);

        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        _startDragTimer!.Dispose();
        _startDragTimer = null;

        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed);
        AssociatedObject?.RemoveHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased);
        AssociatedObject?.RemoveHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved);
    }

    private void AssociatedObject_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        //e.PreventGestureRecognition();
        if (AssociatedObject?.Parent?.Parent is not ItemsControl itemsControl)
        {
            return;
        }

        _itemsControl = itemsControl;
        var p = e.GetCurrentPoint(AssociatedObject);
        var properties = p.Properties;
        if (properties.IsLeftButtonPressed)
        {
            _latestPointerDownPoint = e.GetPosition(null);
            _latestMovePoint = _latestPointerDownPoint;
            //if (p.Pointer.Type == PointerType.Mouse)
            //    StartDrag(e.GetPosition(null));
            //else
            {
                _waitingForDragStart = true;
                _startDragTimer!.Change(300, -1);
            }
        }
    }

    private void OnStartDragTimerCallback(object? state)
    {
        if (!_waitingForDragStart)
            return;

         var diff = _latestPointerDownPoint - _latestMovePoint;

        if (Math.Abs(diff.X) <= _thresholdDelta && Math.Abs(diff.Y) <= _thresholdDelta)
        {
            Dispatcher.UIThread.Invoke(() => { StartDrag(_latestMovePoint); });
        }
    }

    private void StartDrag(Point pos)
    {
        _dragStartPoint = pos;
        _draggedContainer = AssociatedObject.Parent as Control;
        _oldItemZIndex = _draggedContainer!.ZIndex;
        _draggedContainer.ZIndex = 100;
        AddTransforms();

        ((TranslateTransform)_draggedContainer.RenderTransform!).Y -= 10;

        _draggedContainer.Opacity = 0.7;
        _readyToDrag = true;
    }

    private void AssociatedObject_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.PreventGestureRecognition();
        
        _waitingForDragStart = false;
        
        //if (!Equals(e.Pointer.Captured, AssociatedObject)) return;
        if (_readyToDrag) //если дошли до _readyToDrag, значит уже схватились за контрол и нужно в любом случае откатить прозрачность и трансформации
        {
            if (_draggedContainer != null)
            {
                _draggedContainer.ZIndex = _oldItemZIndex;
                _draggedContainer.Opacity = 1;
            }
            RemoveTransforms();

            if (_draggedIndex >= 0 && _targetIndex >= 0 && _draggedIndex != _targetIndex)
            {
                Debug.WriteLine($"MoveItem {_draggedIndex} -> {_targetIndex}");
                MoveDraggedItem(_itemsControl, _draggedIndex, _targetIndex);
            }

            e.Pointer.Capture(null);

            _readyToDrag = false;
            _itemsControl = null;
            _draggedContainer = null!;
        }
    }

    private void AddTransforms()
    {
        if (_itemsControl?.Items is not null)
            for (var i = 0; i < _itemsControl.Items.Count; i++)
                SetItemRenderTransform(i, new TranslateTransform());
    }

    private void RemoveTransforms()
    {
        if (_itemsControl?.Items is not null)
            for (var i = 0; i < _itemsControl.Items.Count; i++)
                SetItemRenderTransform(i, null);
    }

    private void SetItemRenderTransform(int i, Transform? t)
    {
        var container = _itemsControl!.ContainerFromIndex(i);
        if (container is not null) container.RenderTransform = t;
    }
    private void MoveDraggedItem(ItemsControl? itemsControl, int draggedIndex, int targetIndex)
    {
        if (itemsControl?.ItemsSource is not IList items)
        {
            return;
        }

        var draggedItem = items[draggedIndex];
        items.RemoveAt(draggedIndex);
        items.Insert(targetIndex, draggedItem);

        if (itemsControl is SelectingItemsControl selectingItemsControl)
        {
            selectingItemsControl.SelectedIndex = targetIndex;
        }
    }

    private void AssociatedObject_PointerMoved(object? sender, PointerEventArgs e)
    {
        if(_readyToDrag)
			e.PreventGestureRecognition();

        _latestMovePoint = e.GetPosition(null);

        var point = e.GetPosition(null);
        var diff = _dragStartPoint - point;

        var isCaptured = Equals(e.Pointer.Captured, AssociatedObject);
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;

        if (!isCaptured && _readyToDrag && properties.IsLeftButtonPressed)
        {
            if (Math.Abs(diff.X) > _thresholdDelta || Math.Abs(diff.Y) > _thresholdDelta)
            {
                e.Pointer.Capture(AssociatedObject);
                isCaptured = true;
            }
        }

        if (isCaptured) 
            DragItem(diff);
    }

    private void DragItem(Point diff)
    {
        if (Orientation == Orientation.Horizontal)
        {
            DragItemX(-diff.X);
        }
        else
        {
            DragItemY(-diff.Y);
        }
    }

    private void DragItemX(double delta)
    {
        _draggedIndex = _itemsControl!.IndexFromContainer(_draggedContainer!);
        _targetIndex = -1;

        ((TranslateTransform)_draggedContainer.RenderTransform!).X = delta;

        var draggedBounds = _draggedContainer.Bounds;
        var draggedStart = draggedBounds.X;
        var draggedDeltaStart = draggedBounds.X + delta;
        var draggedDeltaEnd = draggedBounds.X + delta + draggedBounds.Width;

        for (var i = 0; i < _itemsControl.Items.Count; i++)
        {
            var targetContainer = _itemsControl.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
                continue;

            var targetBounds = targetContainer.Bounds;
            var targetStart = targetBounds.X;
            var targetMid = targetBounds.X + targetBounds.Width / 2;
            var targetIndex = _itemsControl.IndexFromContainer(targetContainer);

            if (targetStart > draggedStart && draggedDeltaEnd >= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).X = -draggedBounds.Width;

                _targetIndex = _targetIndex == -1 ? targetIndex : targetIndex > _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Right {_draggedIndex} -> {_targetIndex}");
            }
            else if (targetStart < draggedStart && draggedDeltaStart <= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).X = draggedBounds.Width;

                _targetIndex = _targetIndex == -1 ? targetIndex : targetIndex < _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Left {_draggedIndex} -> {_targetIndex}");
            }
            else
            {
                ((TranslateTransform)targetContainer.RenderTransform).X = 0;
            }
        }

        Debug.WriteLine($"Moved {_draggedIndex} -> {_targetIndex}");
    }

    private void DragItemY(double delta)
    {
        _draggedIndex = _itemsControl.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        ((TranslateTransform)_draggedContainer.RenderTransform).Y = delta - 10;

        var draggedBounds = _draggedContainer.Bounds;
        var draggedStart = draggedBounds.Y;
        var draggedDeltaStart = draggedBounds.Y + delta;
        var draggedDeltaEnd = draggedBounds.Y + delta + draggedBounds.Height;

        for (var i = 0; i < _itemsControl.Items.Count; i++)
        {
            var targetContainer = _itemsControl.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
                continue;

            var targetBounds = targetContainer.Bounds;
            var targetStart = targetBounds.Y;
            var targetMid = targetBounds.Y + targetBounds.Height / 2;
            var targetIndex = _itemsControl.IndexFromContainer(targetContainer);

            if (targetStart > draggedStart && draggedDeltaEnd >= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = -draggedBounds.Height;

                _targetIndex = _targetIndex == -1 ? targetIndex : targetIndex > _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Right {_draggedIndex} -> {_targetIndex}");
            }
            else if (targetStart < draggedStart && draggedDeltaStart <= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = draggedBounds.Height;

                _targetIndex = _targetIndex == -1 ? targetIndex : targetIndex < _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Left {_draggedIndex} -> {_targetIndex}");
            }
            else
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = 0;
            }
        }

        Debug.WriteLine($"Moved {_draggedIndex} -> {_targetIndex}");
    }
}