using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Avalonia.Xaml.Interactivity;

namespace Pix2d.Common.Common.Behaviors;

public class ItemsListContextDragBehavior : Behavior<Control>
{
    private Point _dragStartPoint;
    private PointerEventArgs _triggerEvent;
    private bool _lock;

    private ItemsControl _itemsControl;

    public static readonly StyledProperty<object> ContextProperty =
        AvaloniaProperty.Register<ContextDragBehavior, object>(nameof(Context));

    public static readonly StyledProperty<IDragHandler> HandlerProperty =
        AvaloniaProperty.Register<ContextDragBehavior, IDragHandler>(nameof(Handler));

    public static readonly StyledProperty<double> HorizontalDragThresholdProperty =
        AvaloniaProperty.Register<ContextDragBehavior, double>(nameof(HorizontalDragThreshold), 3);

    public static readonly StyledProperty<double> VerticalDragThresholdProperty =
        AvaloniaProperty.Register<ContextDragBehavior, double>(nameof(VerticalDragThreshold), 3);

    private int _draggedIndex;
    private int _targetIndex;
    private Control _draggedContainer;

    public object Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    public IDragHandler Handler
    {
        get => GetValue(HandlerProperty);
        set => SetValue(HandlerProperty, value);
    }

    public double HorizontalDragThreshold
    {
        get => GetValue(HorizontalDragThresholdProperty);
        set => SetValue(HorizontalDragThresholdProperty, value);
    }

    public double VerticalDragThreshold
    {
        get => GetValue(VerticalDragThresholdProperty);
        set => SetValue(VerticalDragThresholdProperty, value);
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

    private async Task DoDragDrop(PointerEventArgs triggerEvent, object value)
    {
        var data = new DataObject();
        data.Set(ContextDropBehavior.DataFormat, value!);

        var effect = DragDropEffects.None;

        if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            effect |= DragDropEffects.Link;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            effect |= DragDropEffects.Move;
        }
        else if (triggerEvent.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            effect |= DragDropEffects.Copy;
        }
        else
        {
            effect |= DragDropEffects.Move;
        }

        await DragDrop.DoDragDrop(triggerEvent, data, effect);
    }

    private void AssociatedObject_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject?.Parent?.Parent is not ItemsControl itemsControl)
        {
            return;
        }

        _itemsControl = itemsControl;

        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            if (e.Source is Control control
                && AssociatedObject?.DataContext == control.DataContext)
            {
                _dragStartPoint = e.GetPosition(null);
                _triggerEvent = e;
                _lock = true;

                _draggedContainer = AssociatedObject.Parent as Control;

                e.Pointer.Capture(AssociatedObject);

                AddTransforms(_itemsControl);

            }
        }
    }

    private void AddTransforms(ItemsControl? itemsControl)
    {
        if (itemsControl?.Items is null)
        {
            return;
        }

        var i = 0;

        foreach (var _ in itemsControl.Items)
        {
            var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (container is not null)
            {
                container.RenderTransform = new TranslateTransform();
            }

            i++;
        }
    }

    private void RemoveTransforms(ItemsControl? itemsControl)
    {
        if (itemsControl?.Items is null)
        {
            return;
        }

        var i = 0;

        foreach (var _ in itemsControl.Items)
        {
            var container = itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (container is not null)
            {
                container.RenderTransform = null;
            }

            i++;
        }
    }


    private async void AssociatedObject_PointerReleased(object sender, PointerReleasedEventArgs e)
    {
        if (Equals(e.Pointer.Captured, AssociatedObject))
        {
            if (_triggerEvent != null)
            {
                var context = Context ?? AssociatedObject?.DataContext;

                Handler?.BeforeDragDrop(sender, _triggerEvent, context);

                await DoDragDrop(_triggerEvent, context);

                Handler?.AfterDragDrop(sender, _triggerEvent, context);
            }

            _triggerEvent = null;
            if (e.InitialPressMouseButton == MouseButton.Left && _triggerEvent is { })
            {
                _triggerEvent = null;
                _lock = false;
            }

            e.Pointer.Capture(null);
            RemoveTransforms(_itemsControl);

            _itemsControl = null;
            _draggedContainer = null;
        }
    }

    private async void AssociatedObject_PointerMoved(object sender, PointerEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        var isCaptured = Equals(e.Pointer.Captured, AssociatedObject);
        if (properties.IsLeftButtonPressed &&
            _triggerEvent is { })
        {
            var point = e.GetPosition(null);
            var diff = _dragStartPoint - point;
            var horizontalDragThreshold = HorizontalDragThreshold;
            var verticalDragThreshold = VerticalDragThreshold;

            DoMagic(diff);
            return;
            if (Math.Abs(diff.X) > horizontalDragThreshold || Math.Abs(diff.Y) > verticalDragThreshold)
            {
                if (_lock)
                {
                    _lock = false;
                }
                else
                {
                    return;
                }

                if (!isCaptured)
                    e.Pointer.Capture(AssociatedObject);

                var context = Context ?? AssociatedObject?.DataContext;

                Handler?.BeforeDragDrop(sender, _triggerEvent, context);

                await DoDragDrop(_triggerEvent, context);

                Handler?.AfterDragDrop(sender, _triggerEvent, context);

                _triggerEvent = null;
            }
        }
    }

    private void DoMagic(Point diff)
    {

        _draggedIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        //var delta = orientation == Orientation.Horizontal ? position.X - _start.X : position.Y - _start.Y;
        var delta = -diff.Y;

        Debug.WriteLine(_draggedContainer.Bounds);

        ((TranslateTransform)_draggedContainer.RenderTransform).Y = delta;

        var draggedBounds = _draggedContainer.Bounds;

        var draggedStart = draggedBounds.Y;

        var draggedDeltaStart = draggedBounds.Y + delta;

        var draggedDeltaEnd = draggedBounds.Y + delta + draggedBounds.Height;

        var i = 0;

        foreach (var _ in _itemsControl.Items)
        {
            var targetContainer = _itemsControl.ItemContainerGenerator.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
            {
                i++;
                continue;
            }

            var targetBounds = targetContainer.Bounds;

            var targetStart = targetBounds.Y;

            var targetMid = targetBounds.Y + targetBounds.Height / 2;

            var targetIndex = _itemsControl.ItemContainerGenerator.IndexFromContainer(targetContainer);

            if (targetStart > draggedStart && draggedDeltaEnd >= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = -draggedBounds.Height;

                _targetIndex = _targetIndex == -1 ?
                    targetIndex :
                    targetIndex > _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Right {_draggedIndex} -> {_targetIndex}");
            }
            else if (targetStart < draggedStart && draggedDeltaStart <= targetMid)
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = draggedBounds.Height;

                _targetIndex = _targetIndex == -1 ?
                    targetIndex :
                    targetIndex < _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Left {_draggedIndex} -> {_targetIndex}");
            }
            else
            {
                ((TranslateTransform)targetContainer.RenderTransform).Y = 0;
            }

            i++;
        }

        Debug.WriteLine($"Moved {_draggedIndex} -> {_targetIndex}");

    }
}