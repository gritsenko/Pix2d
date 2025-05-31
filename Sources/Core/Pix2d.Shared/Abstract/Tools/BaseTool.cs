#nullable enable
using System.Diagnostics;
using SkiaNodes;
using SkiaNodes.Interactive;

namespace Pix2d.Abstract.Tools;

public abstract class BaseTool : ITool
{
    protected SKNode? RootNode => SKInput.Current.RootNodeProvider?.Invoke();
    public bool IsActive { get; private set; }

    public virtual Task Activate()
    {
        Debug.WriteLine($"{GetType().Name} Tool activated");
        IsActive = true;

        if (RootNode == null) return Task.CompletedTask;

        RootNode.PointerPressed += OnPointerPressed;
        RootNode.PointerReleased += OnPointerReleased;
        RootNode.PointerMoved += OnPointerMoved;
        RootNode.DoubleClicked += OnPointerDoubleClicked;

        return Task.CompletedTask;
    }

    public virtual void Deactivate()
    {
        IsActive = false;
        Debug.WriteLine($"{GetType().Name} Tool deactivated");

        if (RootNode == null) return;
        
        RootNode.PointerPressed -= OnPointerPressed;
        RootNode.PointerReleased -= OnPointerReleased;
        RootNode.PointerMoved -= OnPointerMoved;
        RootNode.DoubleClicked -= OnPointerDoubleClicked;
    }

    protected virtual void OnPointerMoved(object? sender, PointerActionEventArgs e)
    {
    }

    protected virtual void OnPointerReleased(object? sender, PointerActionEventArgs e)
    {
    }

    protected virtual void OnPointerPressed(object? sender, PointerActionEventArgs e)
    {
    }

    protected virtual void OnPointerDoubleClicked(object? sender, PointerActionEventArgs e)
    {
    }
}