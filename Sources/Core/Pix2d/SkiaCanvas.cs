using System;
using System.Linq;
using Avalonia.Controls.Embedding;
using Avalonia.Input;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using CommonServiceLocator;
using Pix2d.Abstract;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using SkiaNodes;
using SkiaNodes.Interactive;
using SkiaSharp;

namespace Pix2d;

public class SkiaCanvas : Border
{
    public ViewPort? ViewPort { get; private set; }
    private RootNode? _rootNode;

    private bool _isInitialized;
    private ICustomDrawOperation _drawingOp;
    private Cursor _cursor;
    private bool _isPointerPressed;
    private Point _initialPos;
    private SKPoint _initialPan;

    //pinch gesture stuff
    bool _isPinching = false;
    private readonly PinchGestureRecognizer _pinchRecognizer = new PinchGestureRecognizer();
    private double _oldScale;
    private SKPoint _oldVpPos;

    public bool AllowTouchDraw { get; set; } = true;
    private static SKInput Input => SKInput.Current;

    public SkiaCanvas()
    {
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
        // PointerLeave += DrawingCanvas_PointerLeave;
        // PointerEnter += DrawingCanvas_PointerEnter;
    }

    private void SkiaCanvas_AttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var root = e.Root as Control;
        if (root != null)
        {
            root.KeyDown += OnKeyDown;
            root.KeyUp += OnKeyUp;
        }
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

        if (!Design.IsDesignMode)
        {
            InitCore();

            if (Math.Abs(finalRect.Size.Width - ViewPort.Size.Width) > 0 ||
                Math.Abs(finalRect.Size.Height - ViewPort.Size.Height) > 0)
            {
                OnSizeChanged();
            }
        }
    }

    private void OnSizeChanged()
    {
        InitializeCanvas();

        if (ViewPort != null && !Bounds.IsEmpty)
        {
            ViewPort.Size = GetViewPortSize();
            ViewPort.Refresh();
        }
    }

    private void InitCore()
    {
        if (_isInitialized || Bounds.IsEmpty)
            return;

        _isInitialized = true;
        InitializeCanvas();
        InitializeViewport();

        var scs = ServiceLocator.Current.GetInstance<ISceneService>();

        _rootNode = SKApp.SceneManager.GetRootNode() as RootNode;
        _rootNode.ShowGrid = true;
        Input.RootNodeProvider = () => _rootNode;
        Input.ViewPortProvider = () => ViewPort;
        Pix2DApp.Instance.ViewPort = ViewPort;

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
        //balzor hack
        if (VisualRoot is EmbeddableControlRoot topLevel)
        {
            var surface = topLevel.PlatformImpl.Surfaces.FirstOrDefault();
            var sType = surface?.GetType();
            if (sType?.Name == "BlazorSkiaSurface")
            {
                var prop = sType.GetProperty("Scaling");
                if (prop != null)
                {
                    var bScale = (double)prop.GetValue(surface);
                    if (bScale > 0)
                        return (float)bScale;
                }
            }
        }


        return (float)VisualRoot.RenderScaling;
    }

    private void OnViewportInitialized()
    {
        ServiceLocator.Current.GetInstance<IToolService>();
        ServiceLocator.Current.GetInstance<IEditService>();
        ServiceLocator.Current.GetInstance<IOperationService>();
        Pix2DApp.Instance.OnStartup();
        Pix2DApp.Instance.ShowAll();
        var ds = ServiceLocator.Current.GetInstance<IDrawingService>();
        ds.UpdateDrawingTarget();
    }

    private void InitializeCanvas()
    {
        var left = Bounds.Left;
        var top = Bounds.Top;

        //hack for 11 preview avalonia
        //if (left == 0)
        //    left = Parent.Bounds.Left;
        //if (top == 0)
        //    top = Parent.Bounds.Top;

        _drawingOp = new SkNodeDrawOp(new Rect(left, top, Bounds.Width, Bounds.Height), this);
    }

    private void OnPointerWheelChanged(object sender, PointerWheelEventArgs e)
    {
        if (ViewPort == null /*|| SKInput.Pointer == null*/)
            return;

        if (e.Delta.Y > 0)
            ViewPort.ZoomIn(Input.Pointer.ViewportPosition);
        else if (e.Delta.Y < 0)
            ViewPort.ZoomOut(Input.Pointer.ViewportPosition);

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

        if (Input.PanMode)
        {
            _initialPan = ViewPort.Pan;
            _initialPos = e.GetPosition(this);
            //Refresh();
            return;
        }

        Input.EraserMode = props.IsRightButtonPressed;

        Input.SetPointerPressed(ToSKPoint(e.GetPosition(this)), ToModifiers(e.KeyModifiers),
            e.Pointer.Type == PointerType.Touch);
        InvalidateVisual();
    }

    private void OnPointerMoved(object sender, PointerEventArgs e)
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

        Input.SetPointerMoved(ToSKPoint(e.GetPosition(this)), props.IsLeftButtonPressed, ToModifiers(e.KeyModifiers),
            pointerType == PointerType.Touch);
        InvalidateVisual();
    }

    private void OnPinch(object sender, PinchEventArgs e)
    {
        Input.SetPointerMoved(ToSKPoint(_pinchRecognizer.Offset), false, KeyModifier.None, true);

        if (!_isPinching)
        {
            _isPinching = true;
            _oldScale = e.Scale;
            _oldVpPos = Input.Pointer.ViewportPosition;
            Input.PanMode = true;
        }
        var deltaPan = _oldVpPos - Input.Pointer.ViewportPosition;
        _oldVpPos = Input.Pointer.ViewportPosition;

        ViewPort.ChangeZoom((float)(1.0f + e.Scale - _oldScale), Input.Pointer.ViewportPosition);
        ViewPort.ChangePan(deltaPan.X, deltaPan.Y);

        _oldScale = e.Scale;

        e.Handled = true;
    }

    private void OnPinchEnded(object sender, PinchEndedEventArgs e)
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

    private class SkNodeDrawOp : ICustomDrawOperation
    {
        private readonly SkiaCanvas _parent;
        public Rect Bounds { get; }
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation other) => false;
        private SKCanvas? _skCanvas;
        private Matrix _lastTransform;

        public SkNodeDrawOp(Rect bounds, SkiaCanvas parent)
        {
            _parent = parent;
            Bounds = bounds;
        }

        public void Dispose()
        {
            // No-op
        }

        public void Render(IDrawingContextImpl context)
        {
            var canvas = GetSkCanvas(context);
            try
            {

                canvas.Save();

                canvas.Clear(new SKColor(60, 60, 60));
                if (_parent._rootNode != null && _parent.ViewPort != null)
                {
                    if (_lastTransform != context.Transform)
                    {
                        _parent.ViewPort.PivotTransformMatrix = ToSKMatrix(context.Transform);
                        _parent.ViewPort.PivotTransformMatrix.TransX *= _parent.ViewPort.ScaleFactor;
                        _parent.ViewPort.PivotTransformMatrix.TransY *= _parent.ViewPort.ScaleFactor;
                        _lastTransform = context.Transform;
                    }

                    _parent._rootNode.Render(canvas, _parent.ViewPort);
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

        private SKCanvas GetSkCanvas(IDrawingContextImpl context)
        {
            if (_skCanvas?.Handle == IntPtr.Zero)
                _skCanvas = null;

            return _skCanvas ??= GetCanvasFromField();

            SKCanvas GetCanvasFromField()
            {
                var leaseFeature = context.GetFeature<ISkiaSharpApiLeaseFeature>();
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
