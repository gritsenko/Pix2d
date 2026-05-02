#nullable enable
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Services;
using Pix2d.UI.Resources;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaNodes.Interactive;
using SkiaNodes.Render;
using SkiaSharp;

namespace Pix2d;

public class SkiaCanvas : Control
{
    private const int UndoGestureTouchCooldownMs = 220;
    private const int UndoTapPinchGuardMs = 140;
    private readonly IServiceProvider _serviceProvider;
    private ViewPort? ViewPort { get; set; }
    private RootNode? _rootNode;

    private bool _isInitialized;
    private DateTime _initTime;
    private ICustomDrawOperation _drawingOp = null!;
    private Cursor? _cursor;
    private bool _isPointerPressed;
    private Point _initialPos;
    private SKPoint _initialPan;

    //pinch gesture stuff
    bool _isPinching = false;
    private readonly ZoomPanGestureRecognizer _pinchRecognizer = new();
    private float _gestureStartZoom;
    private SKPoint _gestureStartPan;

    public static readonly RoutedEvent<RoutedEventArgs> UndoGestureEvent = RoutedEvent.Register<SkiaCanvas, RoutedEventArgs>("UndoGesture", RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> RedoGestureEvent = RoutedEvent.Register<SkiaCanvas, RoutedEventArgs>("RedoGesture", RoutingStrategies.Bubble);

    private readonly MultiFingerGestureRecognizer _undoGesture = new() { FingersCount = 2, TapCount = 2, RoutedEventToRaise = UndoGestureEvent };
    private bool _isUndoGestureTracking = false;
    private bool _isTouchDrawingSuppressed = false;
    private readonly HashSet<int> _activeTouchPointers = [];
    private long _touchSuppressionUntilMs;
    // TODO: 3-finger gesture doesn't work reliably on Android/Windows - need alternative approach
    //private readonly MultiFingerGestureRecognizer _redoGesture = new() { FingersCount = 3, TapCount = 2, RoutedEventToRaise = RedoGestureEvent };
    private double _oldScale;
    private SKPoint _oldVpPos;
    private readonly IViewPortService _viewPortService = null!;
    private readonly AppState _appState;

    public bool AllowTouchDraw { get; set; } = true;
    private static SKInput Input => SKInput.Current;

    // Safe area insets (e.g., notch, status bar)
    private Thickness _safeAreaInsets = new Thickness(0);
    public Thickness SafeAreaInsets
    {
        get => _safeAreaInsets;
        set
        {
            if (_safeAreaInsets != value)
            {
                _safeAreaInsets = value;
                // Apply margin to the SkiaCanvas itself
                Margin = value;
                OnSizeChanged();
            }
        }
    }

    public SkiaCanvas(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _appState = serviceProvider.GetRequiredService<AppState>();
        ApplyUndoGestureSettings();

        _appState.WatchFor(x => x.IsTwoFingerDoubleTapUndoEnabled, ApplyUndoGestureSettings);
        _appState.WatchFor(x => x.TwoFingerDoubleTapTimeoutMs, ApplyUndoGestureSettings);
        _appState.WatchFor(x => x.IsStylusModeEnabled, OnTouchInputModeChanged);
        _appState.WatchFor(x => x.IsSingleFingerPanEnabled, OnTouchInputModeChanged);

        ClipToBounds = true;
        if (Design.IsDesignMode)
            return;

        Focusable = true;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;

        GestureRecognizers.Add(_undoGesture);
        //GestureRecognizers.Add(_redoGesture); // TODO: 3-finger gesture doesn't work reliably
        GestureRecognizers.Add(_pinchRecognizer);

        AddHandler(UndoGestureEvent, OnUndoGesture);
        //AddHandler(RedoGestureEvent, OnRedoGesture); // TODO: 3-finger gesture doesn't work reliably

        AddHandler(InputElement.PinchEvent, OnPinch);
        AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);
        AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, OnPointerTouchPadGestureMagnify);

        _undoGesture.TrackingStarted += OnUndoGestureTrackingStarted;
        _undoGesture.TrackingEnded += OnUndoGestureTrackingEnded;

        AttachedToVisualTree += SkiaCanvas_AttachedToVisualTree;

        _viewPortService = serviceProvider.GetRequiredService<IViewPortService>();

    }

    private void SkiaCanvas_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
            topLevel.ScalingChanged += SkiaCanvas_ScalingChanged;

        if (e.RootVisual is Control root)
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
            ViewPort.UpdateViewportMetrics(GetViewPortSize(), GetScale());
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
        if (_rootNode != null)
            _rootNode.ShowGrid = true;
        Input.RootNodeProvider = () => _rootNode!;
        Input.ViewPortProvider = () => ViewPort!;
        _viewPortService.Initialize(ViewPort!);

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
        return (float)(TopLevel.GetTopLevel(this)?.RenderScaling ?? 1f);
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

        if (e.Pointer.Type != PointerType.Touch)
        {
            CancelTouchOnlyGestureState();
        }

        var scroll = e.Delta * 30f * ViewPort.ScaleFactor;
        var zoomOrigin = ToSKPoint(e.GetPosition(this));

        bool shouldZoom;
        if (_appState.MouseWheelBehavior == Pix2d.Primitives.ViewPort.MouseWheelBehavior.Zoom)
        {
            shouldZoom = (e.KeyModifiers & KeyModifiers.Control) == 0;
        }
        else
        {
            shouldZoom = (e.KeyModifiers & KeyModifiers.Control) > 0;
        }

        if (shouldZoom)
        {
            if (e.Delta.Y > 0)
                ViewPort.ZoomIn(zoomOrigin);
            else if (e.Delta.Y < 0)
                ViewPort.ZoomOut(zoomOrigin);
        }
        else if ((e.KeyModifiers & KeyModifiers.Shift) > 0 && scroll.Y != 0 && scroll.X == 0)
        {
            ViewPort.ChangePan(-(float)scroll.Y, 0);
        }
        else
        {
            ViewPort.ChangePan(-(float)scroll.X, -(float)scroll.Y);
        }

        InvalidateVisual();
    }

    private void OnPointerTouchPadGestureMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (ViewPort == null)
        {
            e.Handled = true;
            return;
        }

        CancelTouchOnlyGestureState();

        var magnification = (float)e.Delta.Y;
        if (Math.Abs(magnification) < 0.0001f)
        {
            e.Handled = true;
            return;
        }

        var zoomFactor = Math.Max(0.01f, 1f + magnification);
        var zoomOrigin = ToSKPoint(e.GetPosition(this));

        ViewPort.ChangeZoom(zoomFactor, zoomOrigin);
        InvalidateVisual();

        e.Handled = true;
    }


    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var pointerType = e.Pointer.Type;
        var shouldFinalizeTouchInput = pointerType == PointerType.Touch
                                       && _isPointerPressed
                                       && !_isPinching
                                       && !_isUndoGestureTracking
                                       && !Input.PanMode;
        var shouldIgnoreSingleTouch = ShouldIgnoreSingleTouch(pointerType);

        if (pointerType == PointerType.Touch)
        {
            _activeTouchPointers.Remove(e.Pointer.Id);
            TryEndTouchSuppression();
        }

        if (_isPinching)
        {
            _isPointerPressed = false;
            return;
        }

        if (shouldIgnoreSingleTouch)
        {
            _isPointerPressed = false;
            return;
        }

        if (!shouldFinalizeTouchInput && ShouldBlockTouchDrawing(pointerType))
        {
            _isPointerPressed = false;
            TryEndTouchSuppression();
            return;
        }

        _isPointerPressed = false;

        if (Input.PanMode)
        {
            if (!_isUndoGestureTracking)
            {
                Input.PanMode = false;
                UpdateCursor();
            }
            return;
        }

        Input.SetPointerReleased(ToSKPoint(e.GetPosition(this)), ToModifiers(e.KeyModifiers), e.Pointer.Type == PointerType.Touch);
        InvalidateVisual();
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Touch)
            return;

        var shouldFinalizeTouchInput = _isPointerPressed
                                       && !_isPinching
                                       && !_isUndoGestureTracking
                                       && !Input.PanMode
                                       && Input.Pointer.ViewportPosition != default;

        if (shouldFinalizeTouchInput)
        {
            var releasePosition = Input.Pointer.ViewportPosition;
            Input.SetPointerReleased(releasePosition, Input.GetModifiers(), true);
            InvalidateVisual();
        }

        _activeTouchPointers.Remove(e.Pointer.Id);
        _isPointerPressed = false;
        TryEndTouchSuppression();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var pointerType = e.Pointer.Type;
        if (pointerType != PointerType.Touch)
        {
            CancelTouchOnlyGestureState();
        }
        else
        {
            if (_isTouchDrawingSuppressed && !_isUndoGestureTracking && !_isPinching && _activeTouchPointers.Count < 2)
            {
                _isTouchDrawingSuppressed = false;
            }

            _activeTouchPointers.Add(e.Pointer.Id);

            if (_activeTouchPointers.Count >= 2)
            {
                BeginTouchUndoSuppression();
            }
        }

        var shouldIgnoreSingleTouch = ShouldIgnoreSingleTouch(pointerType);
        if (ShouldBlockTouchDrawing(pointerType))
        {
            return;
        }

        _initialPos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        var isTouchPan = ShouldUseTouchPan(pointerType);

        if (shouldIgnoreSingleTouch)
        {
            _isPointerPressed = false;
            return;
        }

        _isPointerPressed = true;

        if ((!AllowTouchDraw && pointerType == PointerType.Touch) || props.IsMiddleButtonPressed || isTouchPan)
        {
            Input.PanMode = true;
        }

        var position = e.GetPosition(this);

        if (Input.PanMode || _isUndoGestureTracking)
        {
            if (ViewPort != null)
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
        var pointerType = e.Pointer.Type;
        if (_isPinching || _isUndoGestureTracking || ShouldBlockTouchDrawing(pointerType) || ShouldIgnoreSingleTouch(pointerType))
        {
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);

        if ((!AllowTouchDraw && pointerType == PointerType.Touch) || props.IsMiddleButtonPressed || ShouldUseTouchPan(pointerType))
        {
            Input.PanMode = true;
        }

        if (Input.PanMode && _isPointerPressed)
        {
            var offsetX = pos.X - _initialPos.X;
            var offsetY = pos.Y - _initialPos.Y;

            if (ViewPort != null)
                ViewPort.SetPan((float)(_initialPan.X - offsetX * ViewPort.ScaleFactor), (float)(_initialPan.Y - offsetY * ViewPort.ScaleFactor));
            InvalidateVisual();
            return;
        }

        var isPointerPressed = pointerType == PointerType.Touch ? _isPointerPressed : props.IsLeftButtonPressed;
        Input.SetPointerMoved(ToSKPoint(pos), isPointerPressed, ToModifiers(e.KeyModifiers),
            pointerType == PointerType.Touch);
    }

    private void OnUndoGesture(object? sender, RoutedEventArgs e)
    {
        if (!_appState.IsTwoFingerDoubleTapUndoEnabled)
        {
            e.Handled = true;
            return;
        }

        BeginTouchUndoSuppression();
        ExtendTouchSuppressionCooldown(UndoGestureTouchCooldownMs);

        // Откат камеры
        if (ViewPort != null)
        {
            ViewPort.SetZoom(_gestureStartZoom);
            ViewPort.SetPan(_gestureStartPan.X, _gestureStartPan.Y); 
        }

        if (_isPinching)
        {
            _isPinching = false;
            Input.PanMode = false;        }

        _serviceProvider.GetRequiredService<IOperationService>().Undo();
        e.Handled = true;
    }

    private void OnUndoGestureTrackingStarted(object? sender, EventArgs e)
    {
        _isUndoGestureTracking = true;
        BeginTouchUndoSuppression();

        if (ViewPort != null)
        {
            _gestureStartZoom = ViewPort.Zoom;
            _gestureStartPan = ViewPort.Pan;
        }
    }

    private void OnUndoGestureTrackingEnded(object? sender, EventArgs e)
    {
        _isUndoGestureTracking = false;
        TryEndTouchSuppression();
    }

    private void OnRedoGesture(object? sender, RoutedEventArgs e)
    {
        _serviceProvider.GetRequiredService<IOperationService>().Redo();
        e.Handled = true;
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (ViewPort == null)
        {
            e.Handled = true;
            return;
        }

        var origin = ToSKPoint(e.ScaleOrigin);

        if (!_isPinching)
        {
            _isPinching = true;
            _oldScale = e.Scale;
            _oldVpPos = origin;
            Input.PanMode = true;
            e.Handled = true;
            return;
        }

        var deltaPan = _oldVpPos - origin;
        ViewPort.ChangePan(deltaPan.X, deltaPan.Y);
        ViewPort.ChangeZoom((float)(e.Scale / _oldScale), origin);

        _oldVpPos = origin;
        _oldScale = e.Scale;

        InvalidateVisual();
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
    {
        Input.PanMode = false;
        _isPinching = false;
        _activeTouchPointers.Clear();

        // Keep suppression active briefly to prevent touch release from applying/resetting selection
        _isTouchDrawingSuppressed = true;
        ExtendTouchSuppressionCooldown(UndoTapPinchGuardMs);

        e.Handled = true;
    }

    private void ApplyUndoGestureSettings()
    {
        _undoGesture.IsGestureEnabled = _appState.IsTwoFingerDoubleTapUndoEnabled;
        _undoGesture.DoubleTapIntervalMs = Math.Max(100, Math.Min(1500, _appState.TwoFingerDoubleTapTimeoutMs));

        if (!_undoGesture.IsGestureEnabled)
        {
            CancelTouchOnlyGestureState();
        }
    }

    private void OnTouchInputModeChanged()
    {
        if (_activeTouchPointers.Count == 0 && !_isPinching && !_isUndoGestureTracking)
        {
            Input.PanMode = false;
            UpdateCursor();
            return;
        }

        CancelTouchOnlyGestureState();
    }

    private bool HasTouchOnlyGestureState()
    {
        return _isPinching
               || _isUndoGestureTracking
               || _isTouchDrawingSuppressed
               || _activeTouchPointers.Count > 0
               || _undoGesture.IsTapSequenceInProgress;
    }

    private void CancelTouchOnlyGestureState()
    {
        if (!HasTouchOnlyGestureState())
            return;

        var wasPinching = _isPinching;

        _pinchRecognizer.Reset();
        _undoGesture.ResetTapSequence();

        _activeTouchPointers.Clear();
        _touchSuppressionUntilMs = 0;
        _isTouchDrawingSuppressed = false;
        _isUndoGestureTracking = false;
        _isPointerPressed = false;
        _isPinching = false;
        _oldScale = 0;
        _oldVpPos = default;

        if (wasPinching)
        {
            Input.PanMode = false;
            UpdateCursor();
        }

        _serviceProvider.GetRequiredService<IDrawingService>().CancelActiveDrawing();
        Input.CapturedPointerBy = null;
        InvalidateVisual();
    }

    private bool ShouldBlockTouchDrawing(PointerType pointerType)
    {
        return pointerType == PointerType.Touch && (_isTouchDrawingSuppressed || IsUndoTapSequencePending() || IsSuppressionCooldownActive());
    }

    private bool ShouldUseTouchPan(PointerType pointerType)
    {
        return pointerType == PointerType.Touch
               && _appState.IsStylusModeEnabled
               && _appState.IsSingleFingerPanEnabled
               && _activeTouchPointers.Count == 1;
    }

    private bool ShouldIgnoreSingleTouch(PointerType pointerType)
    {
        return pointerType == PointerType.Touch
               && _appState.IsStylusModeEnabled
               && !_appState.IsSingleFingerPanEnabled
               && _activeTouchPointers.Count == 1;
    }

    private bool IsSuppressionCooldownActive()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < _touchSuppressionUntilMs;
    }

    private void ExtendTouchSuppressionCooldown(int durationMs)
    {
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var candidate = nowMs + durationMs;
        if (candidate > _touchSuppressionUntilMs)
            _touchSuppressionUntilMs = candidate;
    }

    private bool IsUndoTapSequencePending()
    {
        return _appState.IsTwoFingerDoubleTapUndoEnabled && _undoGesture.IsGestureEnabled && _undoGesture.IsTapSequenceInProgress;
    }

    private void BeginTouchUndoSuppression()
    {
        _isTouchDrawingSuppressed = true;
        ExtendTouchSuppressionCooldown(UndoGestureTouchCooldownMs);
        _isPointerPressed = false;
        _serviceProvider.GetRequiredService<IDrawingService>().CancelActiveDrawing();
        Input.CapturedPointerBy = null;
        InvalidateVisual();
    }

    private void TryEndTouchSuppression()
    {
        if (_isUndoGestureTracking)
            return;

        if (_activeTouchPointers.Count == 0 && !IsSuppressionCooldownActive())
            _isTouchDrawingSuppressed = false;
    }


    private SKPoint ToSKPoint(Point p) => new(
        (float)((ViewPort?.ScaleFactor ?? 1f) * p.X),
        (float)((ViewPort?.ScaleFactor ?? 1f) * p.Y)
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
        var scale = GetScale();
        if (Math.Abs(ViewPort.Size.Width - size.Width) > 0.01f
            || Math.Abs(ViewPort.Size.Height - size.Height) > 0.01f
            || Math.Abs(ViewPort.ScaleFactor - scale) > 0.0001f)
        {
            ViewPort.UpdateViewportMetrics(size, scale);
        }

        if (Design.IsDesignMode)
        {
            base.Render(context);
            return;
        }

        context.Custom(_drawingOp);
        //Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }


    private void ViewPortOnRefreshRequested(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
        //InvalidateVisual();
    }

    private SKSize GetViewPortSize()
    {
        // Insets are now applied via Margin, so we use full bounds
        var w = (int)Bounds.Width;
        var h = (int)Bounds.Height;
        return new SKSize(w, h);
    }

    private class SkNodeDrawOp(Rect bounds, SkiaCanvas parent) : ICustomDrawOperation
    {
        private static readonly SKColor _bgColor = Pix2d.Common.Extensions.ColorExtensions.ToSKColor(StaticResources.Colors.SceneBackgroundColor);

        public Rect Bounds { get; } = bounds;
        public bool HitTest(Point p) => true;
        public bool Equals(ICustomDrawOperation? other) => false;
        private SKCanvas? _skCanvas;
        private Matrix _lastTransform;

        public void Dispose()
        {
            // No-op
        }

        public void Render(ImmediateDrawingContext context)
        {
            var canvas = GetSkCanvas(context);
            if (canvas == null)
                return;
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
            catch (ObjectDisposedException)
            {
                //ignore this. nothing we can do actually
            }
            //else
            //    context.DrawText(Brushes.Black, new Point(), NoSkiaText.PlatformImpl);
        }

        private SKCanvas? GetSkCanvas(ImmediateDrawingContext context)
        {
            if (_skCanvas?.Handle == IntPtr.Zero)
                _skCanvas = null;

            return _skCanvas ??= GetCanvasFromField();

            SKCanvas? GetCanvasFromField()
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
