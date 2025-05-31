#nullable enable
using System.Collections;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace Pix2d.Common.Behaviors;

public class DragBehavior : Behavior<Control>
{
    private bool _enableDrag;
    private Point _start;
    private int _draggedIndex;
    private int _targetIndex;
    private ItemsControl? _itemsControl;
    private Control? _draggedContainer;
    
    public static readonly StyledProperty<Orientation> OrientationProperty = 
        AvaloniaProperty.Register<DragBehavior, Orientation>(nameof(Orientation));
    
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly StyledProperty<bool> IsReadonlyAffectedProperty =
        AvaloniaProperty.Register<DragBehavior, bool>(nameof(IsReadonlyAffected), true);

    public bool IsReadonlyAffected
    {
        get => GetValue(IsReadonlyAffectedProperty);
        set => SetValue(IsReadonlyAffectedProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        var strategies = AssociatedObject is ListBoxItem
            ? RoutingStrategies.Tunnel
            : RoutingStrategies.Direct | RoutingStrategies.Bubble;
        if (AssociatedObject is { })
        {
            AssociatedObject.AddHandler(InputElement.PointerPressedEvent, Pressed, strategies);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    protected override void OnDetaching()
    {
        base.OnDetaching();

        if (AssociatedObject?.Parent is Control parent)
        {
            AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, Pressed);
            AssociatedObject.RemoveHandler(InputElement.PointerReleasedEvent, Pressed);
            AssociatedObject.RemoveHandler(InputElement.PointerMovedEvent, Pressed);
            AssociatedObject.RemoveHandler(InputElement.PointerCaptureLostEvent, CaptureLost);
        }
    }

    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (AssociatedObject?.Parent is not ItemsControl itemsControl ||
            IsReadonlyAffected ||
            !e.GetCurrentPoint(AssociatedObject).Properties.IsLeftButtonPressed)
        {
            return;
        }
            
        var strategies = AssociatedObject is ListBoxItem
            ? RoutingStrategies.Tunnel
            : RoutingStrategies.Direct | RoutingStrategies.Bubble;
        AssociatedObject.AddHandler(InputElement.PointerReleasedEvent, Released, strategies);
        AssociatedObject.AddHandler(InputElement.PointerMovedEvent, Moved, strategies);
        AssociatedObject.AddHandler(InputElement.PointerCaptureLostEvent, CaptureLost, strategies);
        //Messenger.Default.Register<AppDeactivatedMessage>(this, AppDeactivated);

        _enableDrag = true;
        _start = e.GetPosition(itemsControl);
        _draggedIndex = -1;
        _targetIndex = -1;
        _itemsControl = itemsControl;
        _draggedContainer = AssociatedObject;
            
        AddTransforms(_itemsControl);
    }
        
    //private void AppDeactivated(AppDeactivatedMessage obj) => Released();
    private void Released(object? sender, PointerReleasedEventArgs e) => Released();
    private void CaptureLost(object? sender, PointerCaptureLostEventArgs e) => Released();

    private void Released()
    {
        if (AssociatedObject?.Parent is Control parent)
        {
            parent.PointerReleased -= Released;
            parent.PointerMoved -= Moved;
            AssociatedObject.RemoveHandler(InputElement.PointerCaptureLostEvent, CaptureLost);
        }
        //Messenger.Default.Unregister<AppDeactivatedMessage>(this, AppDeactivated);

        if (_enableDrag)
        {
            RemoveTransforms(_itemsControl);

            if (_draggedIndex >= 0 && _targetIndex >= 0 && _draggedIndex != _targetIndex)
            {
                Debug.WriteLine($"MoveItem {_draggedIndex} -> {_targetIndex}");
                MoveDraggedItem(_itemsControl, _draggedIndex, _targetIndex);
            }

            _draggedIndex = -1;
            _targetIndex = -1;
            _enableDrag = false;
            _itemsControl = null;
            _draggedContainer = null;
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
            var container = itemsControl.ContainerFromIndex(i);
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
            var container = itemsControl.ContainerFromIndex(i);
            if (container is not null)
            {
                container.RenderTransform = null;
            }
  
            i++;
        }  
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

    private void Moved(object? sender, PointerEventArgs e)
    {
        if (_itemsControl?.Items is null || _draggedContainer?.RenderTransform is null || !_enableDrag)
        {
            return;
        }

        var orientation = Orientation;
        var position = e.GetPosition(_itemsControl);
        var delta = orientation == Orientation.Horizontal ? position.X - _start.X : position.Y - _start.Y;

        if (orientation == Orientation.Horizontal)
        {
            ((TranslateTransform) _draggedContainer.RenderTransform).X = delta;
        }
        else
        {
            ((TranslateTransform) _draggedContainer.RenderTransform).Y = delta;
        }

        _draggedIndex = _itemsControl.IndexFromContainer(_draggedContainer);
        _targetIndex = -1;

        var draggedBounds = _draggedContainer.Bounds;

        var draggedStart = orientation == Orientation.Horizontal ? 
            draggedBounds.X : draggedBounds.Y;

        var draggedDeltaStart = orientation == Orientation.Horizontal ? 
            draggedBounds.X + delta : draggedBounds.Y + delta;

        var draggedDeltaEnd = orientation == Orientation.Horizontal ?
            draggedBounds.X + delta + draggedBounds.Width : draggedBounds.Y + delta + draggedBounds.Height;

        var i = 0;

        foreach (var _ in _itemsControl.Items)
        {
            var targetContainer = _itemsControl.ContainerFromIndex(i);
            if (targetContainer?.RenderTransform is null || ReferenceEquals(targetContainer, _draggedContainer))
            {
                i++;
                continue;
            }

            var targetBounds = targetContainer.Bounds;

            var targetStart = orientation == Orientation.Horizontal ? 
                targetBounds.X : targetBounds.Y;

            var targetMid = orientation == Orientation.Horizontal ? 
                targetBounds.X + targetBounds.Width / 2 : targetBounds.Y + targetBounds.Height / 2;

            var targetIndex = _itemsControl.IndexFromContainer(targetContainer);

            if (targetStart > draggedStart && draggedDeltaEnd >= targetMid)
            {
                if (orientation == Orientation.Horizontal)
                {
                    ((TranslateTransform) targetContainer.RenderTransform).X = -draggedBounds.Width;
                }
                else
                {
                    ((TranslateTransform) targetContainer.RenderTransform).Y = -draggedBounds.Height;
                }

                _targetIndex = _targetIndex == -1 ? 
                    targetIndex : 
                    targetIndex > _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Right {_draggedIndex} -> {_targetIndex}");
            }
            else if (targetStart < draggedStart && draggedDeltaStart <= targetMid)
            {
                if (orientation == Orientation.Horizontal)
                {
                    ((TranslateTransform) targetContainer.RenderTransform).X = draggedBounds.Width;
                }
                else
                {
                    ((TranslateTransform) targetContainer.RenderTransform).Y = draggedBounds.Height;
                }

                _targetIndex = _targetIndex == -1 ? 
                    targetIndex : 
                    targetIndex < _targetIndex ? targetIndex : _targetIndex;
                Debug.WriteLine($"Moved Left {_draggedIndex} -> {_targetIndex}");
            }
            else
            {
                if (orientation == Orientation.Horizontal)
                {
                    ((TranslateTransform) targetContainer.RenderTransform).X = 0;
                }
                else
                {
                    ((TranslateTransform) targetContainer.RenderTransform).Y = 0;
                }
            }

            i++;
        }

        Debug.WriteLine($"Moved {_draggedIndex} -> {_targetIndex}");
    }
}