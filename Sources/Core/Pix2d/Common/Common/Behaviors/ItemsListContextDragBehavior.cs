using System.Collections;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace Pix2d.Common.Common.Behaviors;

public class ItemsListContextDragBehavior : Behavior<Control>
{
    private Point _dragStartPoint;

    private ItemsControl _itemsControl;

    private double _thresholdDelta = 3;

    private int _draggedIndex;
    private int _targetIndex;
    private Control _draggedContainer;

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<ItemsListContextDragBehavior, Orientation>(nameof(Orientation));

    private int _oldItemZIndex;
    private bool _readyToDrag = false;

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree()
    {
        AssociatedObject?.AddHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AssociatedObject?.AddHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree()
    {
        AssociatedObject?.RemoveHandler(InputElement.PointerPressedEvent, AssociatedObject_PointerPressed);
        AssociatedObject?.RemoveHandler(InputElement.PointerReleasedEvent, AssociatedObject_PointerReleased);
        AssociatedObject?.RemoveHandler(InputElement.PointerMovedEvent, AssociatedObject_PointerMoved);
    }

    private void AssociatedObject_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse) return;
        
        e.PreventGestureRecognition();
        if (AssociatedObject?.Parent?.Parent is not ItemsControl itemsControl)
        {
            return;
        }

        _itemsControl = itemsControl; 
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedContainer = AssociatedObject.Parent as Control;
            _oldItemZIndex = _draggedContainer.ZIndex;
            _draggedContainer.ZIndex = 100;
            AddTransforms();

            _readyToDrag = true;
            // e.Pointer.Capture(AssociatedObject);
        }
    }

    private void AssociatedObject_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse) return;
        
        e.PreventGestureRecognition();
        if (!Equals(e.Pointer.Captured, AssociatedObject)) return;

        if (_draggedContainer != null)
            _draggedContainer.ZIndex = _oldItemZIndex;
        RemoveTransforms();

        if (_draggedIndex >= 0 && _targetIndex >= 0 && _draggedIndex != _targetIndex)
        {
            Debug.WriteLine($"MoveItem {_draggedIndex} -> {_targetIndex}");
            MoveDraggedItem(_itemsControl, _draggedIndex, _targetIndex);
        }

        e.Pointer.Capture(null);

        _readyToDrag = false;
        _itemsControl = null;
        _draggedContainer = null;
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

    private void SetItemRenderTransform(int i, Transform t)
    {
        var container = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
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

    private void AssociatedObject_PointerMoved(object sender, PointerEventArgs e)
    {
        e.PreventGestureRecognition();

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
        _draggedIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        ((TranslateTransform)_draggedContainer.RenderTransform).X = delta;

        var draggedBounds = _draggedContainer.Bounds;
        var draggedStart = draggedBounds.X;
        var draggedDeltaStart = draggedBounds.X + delta;
        var draggedDeltaEnd = draggedBounds.X + delta + draggedBounds.Width;

        for (var i = 0; i < _itemsControl.Items.Count; i++)
        {
            var targetContainer = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
                continue;

            var targetBounds = targetContainer.Bounds;
            var targetStart = targetBounds.X;
            var targetMid = targetBounds.X + targetBounds.Width / 2;
            var targetIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(targetContainer);

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
        _draggedIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        ((TranslateTransform)_draggedContainer.RenderTransform).Y = delta;

        var draggedBounds = _draggedContainer.Bounds;
        var draggedStart = draggedBounds.Y;
        var draggedDeltaStart = draggedBounds.Y + delta;
        var draggedDeltaEnd = draggedBounds.Y + delta + draggedBounds.Height;

        for (var i = 0; i < _itemsControl.Items.Count; i++)
        {
            var targetContainer = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
                continue;

            var targetBounds = targetContainer.Bounds;
            var targetStart = targetBounds.Y;
            var targetMid = targetBounds.Y + targetBounds.Height / 2;
            var targetIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(targetContainer);

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