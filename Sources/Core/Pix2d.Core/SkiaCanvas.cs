#nullable enable
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.UI.Resources;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d;

public class SkiaCanvas : Control
{
    private readonly IServiceProvider _serviceProvider;
    private ViewPort? ViewPort { get; set; }
    private RootNode? _rootNode;

    private bool _isInitialized;
    private DateTime _initTime;
    private ICustomDrawOperation _drawingOp;
    private Cursor? _cursor;
    private bool _isPointerPressed;
    private Point _initialPos;
    private SKPoint _initialPan;

    //pinch gesture stuff
    bool _isPinching = false;
    private readonly ZoomPanGestureRecognizer _pinchRecognizer = new();
    private double _oldScale;
    private SKPoint _oldVpPos;
    private readonly IViewPortService _viewPortService;

    public bool AllowTouchDraw { get; set; } = true;
    private static SKInput Input => SKInput.Current;

    public SkiaCanvas(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        ClipToBounds = true;
        if (Design.IsDesignMode)
            return;

        Focusable = true;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerWheelChanged += OnPointerWheelChanged;

        GestureRecognizers.Add(_pinchRecognizer);
        AddHandler(Gestures.PinchEvent, OnPinch);
        AddHandler(Gestures.PinchEndedEvent, OnPinchEnded);

        AttachedToVisualTree += SkiaCanvas_AttachedToVisualTree;

        _viewPortService = serviceProvider.GetRequiredService<IViewPortService>();
    }

    private void SkiaCanvas_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
            topLevel.ScalingChanged += SkiaCanvas_ScalingChanged;

        if (e.Root is Control root)
        {
            root.KeyDown += OnKeyDown;
            root.KeyUp += OnKeyUp;
        }
    }

    private void SkiaCanvas_ScalingChanged(object? sender, EventArgs e)
    {
        OnSizeChanged();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var key = ToVirtualKeys(e.Key);
        if (Input.EnablePanWithSpace && key == VirtualKeys.Space)
        {
            Input.PanMode = true;
            UpdateCursor();
        }

        Input.SetKeyPressed(key, ToModifiers(e.KeyModifiers));
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        var key = ToVirtualKeys(e.Key);
        if (Input.EnablePanWithSpace && key == VirtualKeys.Space)
        {
            Input.PanMode = false;
            UpdateCursor();
        }
        Input.SetKeyReleased(key, ToModifiers(e.KeyModifiers));
    }

    private void UpdateCursor()
    {
        if (Input.PanMode)
        {
            _cursor ??= new Cursor(StandardCursorType.Hand);
            Cursor = _cursor;
        }
        else
        {
            Cursor = Cursor.Default;
        }
    }

    private static KeyModifier ToModifiers(KeyModifiers keyModifiers) => (KeyModifier)keyModifiers;
    private static VirtualKeys ToVirtualKeys(Key key) => (VirtualKeys)KeyInterop.VirtualKeyFromKey(key);

    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);

        if (Design.IsDesignMode) return;

        InitCore();

        if (ViewPort == null)
        {
            return;
        }
        
        if (Math.Abs(finalRect.Size.Width - ViewPort.Size.Width) > 0 ||
            Math.Abs(finalRect.Size.Height - ViewPort.Size.Height) > 0)
        {
            OnSizeChanged();

            // This is a hack. On some platforms (for example, Android) application starts in the background with small
            // viewport size, and is then resized to the normal size. I could not find any reliable way to detect
            // real startup moment, so we consider rearrange of UI in less then 2 seconds after first init as being done
            // by the system, and retry showing the whole canvas.
            if (DateTime.Now - _initTime < TimeSpan.FromMilliseconds(2000))
            {
                _viewPortService.ShowAll();
            }
        }
    }

    private void OnSizeChanged()
    {
        InitializeCanvas();

        if (ViewPort != null && !IsBoundsEmpty())
        {
            ViewPort.ScaleFactor = (float) VisualRoot!.RenderScaling;
            ViewPort.Size = GetViewPortSize();
            ViewPort.Refresh();
        }
    }

    private bool IsBoundsEmpty() => Bounds.Size.Width < 1 || Bounds.Size.Height < 1;

    private void InitCore()
    {
        if (_isInitialized || IsBoundsEmpty())
            return;

        _isInitialized = true;
        _initTime = DateTime.Now;
        InitializeCanvas();
        InitializeViewport();
        
        _rootNode = SKApp.SceneManager.GetRootNode() as RootNode;
        _rootNode.ShowGrid = true;
        Input.RootNodeProvider = () => _rootNode;
        Input.ViewPortProvider = () => ViewPort;
        _viewPortService.Initialize(ViewPort);

        OnViewportInitialized();
    }

    public void InitializeViewport()
    {
        var scale = GetScale();
        var size = GetViewPortSize();
        ViewPort = new ViewPort((int)size.Width, (int)size.Height);
        ViewPort.ScaleFactor = scale;
        ViewPort.RefreshRequested += ViewPortOnRefreshRequested;
        ViewPort.SetZoom(1);
        ViewPort.SetPan(0, 0);
        ViewPort.Refresh();
    }

    private float GetScale()
    {
        return (float)(VisualRoot?.RenderScaling ?? 1f);
    }

    private void OnViewportInitialized()
    {
        //ServiceLocator.Current.GetInstance<IToolService>();
        //ServiceLocator.Current.GetInstance<IEditService>();
        //ServiceLocator.Current.GetInstance<IOperationService>();
        _viewPortService.ShowAll();
        var ds = _serviceProvider.GetRequiredService<IDrawingService>();
        ds.UpdateDrawingTarget();
    }

    private void InitializeCanvas()
    {
        var left = Bounds.Left;
        var top = Bounds.Top;

        _drawingOp = new SkNodeDrawOp(new Rect(left, top, Bounds.Width, Bounds.Height), this);
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (ViewPort == null /*|| SKInput.Pointer == null*/)
            return;

        var scroll = e.Delta * 30f * ViewPort.ScaleFactor;

        if ((e.KeyModifiers & KeyModifiers.Control) > 0)
        {
            //mouse wheel with control
            if (e.Delta.Y > 0)
                ViewPort.ZoomIn(Input.Pointer.ViewportPosition);
            else if (e.Delta.Y < 0)
                ViewPort.ZoomOut(Input.Pointer.ViewportPosition);
        }
        else if ((e.KeyModifiers & KeyModifiers.Shift) > 0 && scroll.Y != 0 && scroll.X == 0)
        {
            //mouse wheel with shift
            ViewPort.ChangePan(-(float)scroll.Y, 0);
        }
        else
        {
            //touch pad
            ViewPort.ChangePan(-(float)scroll.X, -(float)scroll.Y);
        }

        InvalidateVisual();
    }


    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPinching)
            return;

        _isPointerPressed = false;
        var props = e.GetCurrentPoint(this).Properties;
        var pointerType = e.Pointer.Type;

        var isTouch = pointerType == PointerType.Touch;

        if (Input.PanMode)
        {
            Input.PanMode = false;
            UpdateCursor();
            return;
        }

        Input.SetPointerReleased(ToSKPoint(e.GetPosition(this)), ToModifiers(e.KeyModifiers), e.Pointer.Type == PointerType.Touch);
        InvalidateVisual();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isPointerPressed = true;
        _initialPos = e.GetPosition(this);
        var pointerType = e.Pointer.Type;
        var props = e.GetCurrentPoint(this).Properties;

        if ((!AllowTouchDraw && pointerType == PointerType.Touch) || props.IsMiddleButtonPressed)
        {
            Input.PanMode = true;
        }

        var position = e.GetPosition(this);

        if (Input.PanMode)
        {
            _initialPan = ViewPort.Pan;
            _initialPos = position;
            //Refresh();
            return;
        }

        Input.EraserMode = props.IsRightButtonPressed;

        Input.SetPointerPressed(ToSKPoint(position), ToModifiers(e.KeyModifiers),
            e.Pointer.Type == PointerType.Touch);
        InvalidateVisual();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPinching)
        {
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        var pointerType = e.Pointer.Type;
        var pos = e.GetPosition(this);

        if ((!AllowTouchDraw && pointerType == PointerType.Touch) || props.IsMiddleButtonPressed)
        {
            Input.PanMode = true;
        }

        if (Input.PanMode && _isPointerPressed)
        {
            var offsetX = pos.X - _initialPos.X;
            var offsetY = pos.Y - _initialPos.Y;

            ViewPort.SetPan((float)(_initialPan.X - offsetX * ViewPort.ScaleFactor), (float)(_initialPan.Y - offsetY * ViewPort.ScaleFactor));
            //ViewPort.ChangePan(-(float)translationDeltaX, -(float)translationDeltaY);
            //Refresh();
            return;
        }

        Input.SetPointerMoved(ToSKPoint(pos), props.IsLeftButtonPressed, ToModifiers(e.KeyModifiers),
            pointerType == PointerType.Touch);
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!_isPinching)
        {
            _isPinching = true;
            _oldScale = e.Scale;
            _oldVpPos = e.ScaleOrigin.ToSKPoint();
            Input.PanMode = true;
        }

        var deltaPan = _oldVpPos - e.ScaleOrigin.ToSKPoint();
        ViewPort.ChangePan(deltaPan.X * ViewPort.ScaleFactor, deltaPan.Y * ViewPort.ScaleFactor);
        ViewPort.ChangeZoom((float)(e.Scale / _oldScale), e.ScaleOrigin.ToSKPoint().Multiply(ViewPort.ScaleFactor));

        _oldVpPos = e.ScaleOrigin.ToSKPoint();
        _oldScale = e.Scale;
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        Input.PanMode = false;
        _isPinching = false;
        e.Handled = true;
    }


    private SKPoint ToSKPoint(Point p) => new(
        (float)(ViewPort.ScaleFactor * p.X),
        (float)(ViewPort.ScaleFactor * p.Y)
    );


    public override void Render(DrawingContext context)
    {
        if (ViewPort == null)
        {
            return;
        }
        
        // Sometimes, particularly on load, UI scale factor can change without triggering size change events. So wee need
        // to check that the size is not changed here to prevent broken UI on load.
        var size = GetViewPortSize();
        if (ViewPort?.Size != size)
        {
            ViewPort.Size = size;
        }

        if (Design.IsDesignMode)
        {
            base.Render(context);
            return;
        }

        context.Custom(_drawingOp);
        //Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }


    private void ViewPortOnRefreshRequested(object sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        //InvalidateVisual();
    }

    private SKSize GetViewPortSize()
    {
        var w = (int)(Bounds.Width); // * (SystemScaleFactor / ViewPortScaleFactor));
        var h = (int)(Bounds.Height); // * (SystemScaleFactor / ViewPortScaleFactor));
        return new SKSize(w, h);
    }

    private class SkNodeDrawOp(Rect bounds, SkiaCanvas parent) : ICustomDrawOperation
    {
        private static readonly SKColor _bgColor = StaticResources.Colors.SceneBackgroundColor.ToSKColor();

        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation other) => false;
        private SKCanvas? _skCanvas;
        private Matrix _lastTransform;

        public void Dispose()
        {
            // No-op
        }

        public void Render(ImmediateDrawingContext context)
        {
            var canvas = GetSkCanvas(context);
            try
            {

                canvas.Save();

                canvas.Clear(_bgColor);
                if (parent is { _rootNode: not null, ViewPort: not null })
                {
                    if (_lastTransform != context.CurrentTransform)
                    {
                        parent.ViewPort.PivotTransformMatrix = ToSKMatrix(context.CurrentTransform);
                        parent.ViewPort.PivotTransformMatrix.TransX *= parent.ViewPort.ScaleFactor;
                        parent.ViewPort.PivotTransformMatrix.TransY *= parent.ViewPort.ScaleFactor;
                        _lastTransform = context.CurrentTransform;
                    }

                    SKNodeRenderer.Render(parent._rootNode, new RenderContext(canvas, parent.ViewPort));
                    //_parent._rootNode.Render(canvas, _parent.ViewPort);
                }
                canvas.Restore();
            }
            catch (ObjectDisposedException _)
            {
                //ignore this. nothing we can do actually
            }
            //else
            //    context.DrawText(Brushes.Black, new Point(), NoSkiaText.PlatformImpl);
        }

        private SKCanvas GetSkCanvas(ImmediateDrawingContext context)
        {
            if (_skCanvas?.Handle == IntPtr.Zero)
                _skCanvas = null;

            return _skCanvas ??= GetCanvasFromField();

            SKCanvas GetCanvasFromField()
            {
                var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
                if (leaseFeature == null)
                    return null;
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                return canvas;
            }
        }

        static SKMatrix ToSKMatrix(Matrix m)
        {
            var sm = new SKMatrix
            {
                ScaleX = (float)m.M11,
                SkewX = (float)m.M21,
                TransX = (float)m.M31,
                SkewY = (float)m.M12,
                ScaleY = (float)m.M22,
                TransY = (float)m.M32,
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            };

            return sm;
        }

    }
}
