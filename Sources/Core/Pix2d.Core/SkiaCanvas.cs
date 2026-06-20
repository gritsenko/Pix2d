#nullable enable
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Services;
using Pix2d.Plugins.Drawing.Tools;
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
    private ICustomDrawOperation _drawingOp = null!;
    private Cursor? _cursor;
    private bool _isPointerPressed;
    private Point _initialPos;
    private SKPoint _initialPan;
    private Point? _cachedOriginInTopLevel;

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

    // Single-finger-pan mode only: when a one-finger press lands on a different (inactive) artboard we
    // can't yet tell whether the user means to activate it (tap) or scroll the canvas (drag). We defer
    // the decision — pan once movement passes the threshold, otherwise treat the release as a tap and
    // route it into the normal pipeline so the click-to-activate resolver switches the active sprite.
    private bool _isPendingTouchPanDecision;
    private const double TouchPanActivationThresholdPx = 8;
    // TODO: 3-finger gesture doesn't work reliably on Android/Windows - need alternative approach
    //private readonly MultiFingerGestureRecognizer _redoGesture = new() { FingersCount = 3, TapCount = 2, RoutedEventToRaise = RedoGestureEvent };
    private double _oldScale;
    private SKPoint _oldVpPos;
    private readonly IViewPortService _viewPortService = null!;
    private readonly AppState _appState;
    private IEditService? _editService;
    private readonly IPenHapticsService? _penHaptics;

    public bool AllowTouchDraw { get; set; } = true;
    private static SKInput Input => SKInput.Current;

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
        DetachedFromVisualTree += SkiaCanvas_DetachedFromVisualTree;

        _viewPortService = serviceProvider.GetRequiredService<IViewPortService>();
        // Optional: the no-op default is registered in Core, the real one in the desktop head on Windows.
        _penHaptics = serviceProvider.GetService<IPenHapticsService>();

    }

    private void SkiaCanvas_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.ScalingChanged += SkiaCanvas_ScalingChanged;
            if (topLevel.InsetsManager is { } insetsManager)
                insetsManager.SafeAreaChanged += OnSafeAreaChanged;
        }

        // Bind pen haptics to the native window so a haptic-capable stylus can be resolved (Windows only;
        // a no-op everywhere else). Cheap to re-call — the service ignores a handle it's already bound to.
        _penHaptics?.Attach(topLevel?.TryGetPlatformHandle()?.Handle ?? 0);

        LayoutUpdated += OnLayoutUpdated;

        UpdateOriginInTopLevel();

        if (e.RootVisual is Control root)
        {
            root.KeyDown += OnKeyDown;
            root.KeyUp += OnKeyUp;
        }
    }

    private void SkiaCanvas_DetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _penHaptics?.Detach();

        LayoutUpdated -= OnLayoutUpdated;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            topLevel.ScalingChanged -= SkiaCanvas_ScalingChanged;
            if (topLevel.InsetsManager is { } insetsManager)
                insetsManager.SafeAreaChanged -= OnSafeAreaChanged;
        }
    }

    private void SkiaCanvas_ScalingChanged(object? sender, EventArgs e)
    {
        UpdateOriginInTopLevel();
        OnSizeChanged();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        RefreshOriginIfChanged();
    }

    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
    {
        RefreshOriginIfChanged();
    }

    private void RefreshOriginIfChanged()
    {
        var previous = _cachedOriginInTopLevel;
        UpdateOriginInTopLevel();
        if (_cachedOriginInTopLevel != previous)
            InvalidateVisual();
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

    /// <summary>
    /// True when the input should erase: the right mouse button (the classic erase shortcut) or a stylus
    /// used with its eraser end. Windows/Android report the flipped pen via <see cref="PointerPointProperties.IsEraser"/>
    /// and/or <see cref="PointerPointProperties.IsInverted"/> — different digitizers populate one or the other,
    /// so we accept either. Drives <see cref="SKInput.EraserMode"/>, which a brush/pencil tool reads to switch
    /// to <see cref="Pix2d.Primitives.Drawing.BrushDrawingMode.Erase"/> for the stroke (and the hover preview).
    /// </summary>
    private static bool IsEraserInput(PointerPointProperties props)
        => props.IsRightButtonPressed || props.IsEraser || props.IsInverted;

    /// <summary>
    /// Normalized pressure [0..1] for <see cref="SKInput.Pressure"/>. Only a stylus pen carries real
    /// pressure; mouse and touch report <c>1</c> so pressure-driven brushes behave as before for them.
    /// (Avalonia reports 0.5 for pressure-less devices, which would otherwise halve every stroke.)
    /// </summary>
    private static float GetPressure(PointerPointProperties props, PointerType pointerType)
        => pointerType == PointerType.Pen ? Math.Clamp(props.Pressure, 0f, 1f) : 1f;

    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);

        UpdateOriginInTopLevel();

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

    private void UpdateOriginInTopLevel()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        _cachedOriginInTopLevel = topLevel == null ? null : this.TranslatePoint(default, topLevel);
    }

    private void UpdatePivotTransform(Matrix currentTransform, Point? controlOriginInTopLevel)
    {
        if (ViewPort == null)
            return;

        var pivotTransform = ToSKMatrix(currentTransform);
        pivotTransform.TransX *= ViewPort.ScaleFactor;
        pivotTransform.TransY *= ViewPort.ScaleFactor;

        // On some platforms the custom draw op receives CurrentTransform without the control's
        // translation inside the TopLevel: on Android the safe-area inset is missing on the first
        // frames, and on Windows the compositor hands us an identity transform always. Without a
        // correction the scene renders at the window origin, which became visible once the project
        // tabs row pushed the canvas down. Fall back to the cached control origin (kept fresh via
        // ArrangeCore/LayoutUpdated/SafeAreaChanged) whenever the supplied translation is zero.
        if (controlOriginInTopLevel.HasValue)
        {
            var fallbackTransX = (float)(controlOriginInTopLevel.Value.X * ViewPort.ScaleFactor);
            var fallbackTransY = (float)(controlOriginInTopLevel.Value.Y * ViewPort.ScaleFactor);

            if (Math.Abs(pivotTransform.TransX) < 0.01f && Math.Abs(fallbackTransX) > 0.01f)
                pivotTransform.TransX = fallbackTransX;

            if (Math.Abs(pivotTransform.TransY) < 0.01f && Math.Abs(fallbackTransY) > 0.01f)
                pivotTransform.TransY = fallbackTransY;
        }

        ViewPort.PivotTransformMatrix = pivotTransform;
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

        // Stop pen haptics on any release (idempotent — no-op when no stroke is active). Done up front so
        // the various early returns below for touch/pan can't leave the inking waveform running.
        _penHaptics?.EndStroke();

        var shouldFinalizeTouchInput = pointerType == PointerType.Touch
                                       && _isPointerPressed
                                       && !_isPinching
                                       && !_isUndoGestureTracking
                                       && !Input.PanMode;
        var shouldIgnoreSingleTouch = ShouldIgnoreSingleTouch(pointerType);
        var shouldAllowTouchPan = ShouldAllowTouchPan(pointerType);

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

        if (_isPendingTouchPanDecision)
        {
            // Released without crossing the drag threshold → a tap on a different artboard. Replay it as a
            // press+release into the normal pipeline so the click-to-activate resolver switches the active
            // sprite (exactly what a stylus/mouse tap would do). No pan was started.
            _isPendingTouchPanDecision = false;
            _isPointerPressed = false;
            var modifiers = ToModifiers(e.KeyModifiers);
            Input.SetPointerPressed(ToSKPoint(_initialPos), modifiers, true, 1);
            Input.SetPointerReleased(ToSKPoint(e.GetPosition(this)), modifiers, true);
            InvalidateVisual();
            return;
        }

        if (shouldIgnoreSingleTouch)
        {
            _isPointerPressed = false;
            return;
        }

        if (!shouldFinalizeTouchInput && !shouldAllowTouchPan && ShouldBlockTouchDrawing(pointerType))
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
        // Losing capture mid-stroke (e.g. pen) must also stop the inking waveform. The touch-only logic
        // below early-returns for a pen, so do this before that gate.
        _penHaptics?.EndStroke();

        if (e.Pointer.Type != PointerType.Touch)
            return;

        if (_isPendingTouchPanDecision)
        {
            // The single-finger gesture was taken over (e.g. a second finger / pinch) before we resolved
            // tap-vs-pan. Drop it without activating anything or replaying it into the pipeline.
            _isPendingTouchPanDecision = false;
            _isPointerPressed = false;
            _activeTouchPointers.Remove(e.Pointer.Id);
            TryEndTouchSuppression();
            return;
        }

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
            if (ShouldResetStaleTouchStateOnPress(e.Pointer.Id))
            {
                CancelTouchOnlyGestureState();
            }

            _activeTouchPointers.Add(e.Pointer.Id);

            if (_activeTouchPointers.Count >= 2)
            {
                BeginTouchUndoSuppression();
            }
        }

        var shouldIgnoreSingleTouch = ShouldIgnoreSingleTouch(pointerType);
        var shouldAllowTouchPan = ShouldAllowTouchPan(pointerType);

        if (shouldAllowTouchPan && _undoGesture.IsTapSequenceInProgress)
        {
            _undoGesture.ResetTapSequence();
        }

        if (!shouldAllowTouchPan && ShouldBlockTouchDrawing(pointerType))
        {
            return;
        }

        _initialPos = e.GetPosition(this);
        var props = e.GetCurrentPoint(this).Properties;
        var isTouchPan = ShouldUseTouchPan(pointerType);

        // Single-finger-pan ("scroll with one finger") must yield to direct manipulation: scroll only when
        // the finger is on empty space or the active raster. If it lands on an interactive overlay
        // (transform/resize/rotate handles, artboard labels, object-edit handles) route the touch into the
        // normal pipeline so manipulation works as in the other input modes. If it lands on a different
        // artboard, defer the choice — a tap activates it, a drag pans (resolved in Moved/Released).
        if (isTouchPan)
        {
            var pressWorldPos = GetWorldPosition(_initialPos);
            if (pressWorldPos.HasValue)
            {
                if (IsOverInteractiveOverlay(pressWorldPos.Value))
                {
                    isTouchPan = false; // hand the press to the scene's interactive nodes
                }
                else if (EditService.GetInactiveArtboardAt(pressWorldPos.Value) != null)
                {
                    BeginPendingTouchPanDecision();
                    return;
                }
            }
        }

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

        Input.EraserMode = IsEraserInput(props);
        Input.Pressure = GetPressure(props, pointerType);

        Input.SetPointerPressed(ToSKPoint(position), ToModifiers(e.KeyModifiers),
            e.Pointer.Type == PointerType.Touch, e.ClickCount);

        // A pen tip went down to draw — start the continuous "pen on paper" haptic for the active tool
        // (no-op unless this is a haptic-capable stylus on Windows 11).
        if (pointerType == PointerType.Pen)
            _penHaptics?.BeginStroke(GetHapticTool());

        InvalidateVisual();
    }

    /// <summary>
    /// Maps the active drawing context to a haptic inking waveform. Only the freehand pixel tools get
    /// continuous feedback; <see cref="SKInput.EraserMode"/> covers both the Eraser tool and a flipped
    /// pen used over the Brush tool. Everything else returns <see cref="PenHapticTool.None"/> (silent).
    /// </summary>
    private PenHapticTool GetHapticTool()
    {
        if (!_appState.IsPenHapticsEnabled)
            return PenHapticTool.None;

        var toolKey = _appState.ToolsState.CurrentToolKey;
        if (Input.EraserMode || toolKey == nameof(EraserTool))
            return PenHapticTool.Eraser;
        return toolKey == nameof(BrushTool) ? PenHapticTool.Pen : PenHapticTool.None;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pointerType = e.Pointer.Type;
        var shouldAllowTouchPan = ShouldAllowTouchPan(pointerType);
        if (_isPinching || _isUndoGestureTracking || (!shouldAllowTouchPan && ShouldBlockTouchDrawing(pointerType)) || ShouldIgnoreSingleTouch(pointerType))
        {
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        var pos = e.GetPosition(this);

        if (_isPendingTouchPanDecision)
        {
            var dx = pos.X - _initialPos.X;
            var dy = pos.Y - _initialPos.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < TouchPanActivationThresholdPx)
                return; // not enough movement yet — still potentially a tap-to-activate

            // Movement crossed the threshold → this is a pan. Re-anchor to the current position so the
            // canvas doesn't jump by the threshold distance, then fall through to the pan path below.
            _isPendingTouchPanDecision = false;
            Input.PanMode = true;
            _initialPos = pos;
            if (ViewPort != null)
                _initialPan = ViewPort.Pan;
        }

        // Don't hijack into pan while an interactive overlay (transform/resize/rotate handle) has captured
        // the touch — single-finger-pan must yield to the manipulation the press already landed on.
        var shouldEnterTouchPan = ShouldUseTouchPan(pointerType) && Input.CapturedPointerBy == null;
        if ((!AllowTouchDraw && pointerType == PointerType.Touch) || props.IsMiddleButtonPressed || shouldEnterTouchPan)
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

        // Keep the eraser flag live for a stylus: the eraser end both hovers (preview) and draws, so the
        // flag must be current before the pointer snapshot is built below. For mouse/touch this matches
        // the press path (right button, otherwise false).
        Input.EraserMode = IsEraserInput(props);
        Input.Pressure = GetPressure(props, pointerType);

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
            // Once pinch has actually started, this interaction is no longer a candidate for
            // two-finger tap undo. Reset the tap recognizer before its capture-lost path can
            // misclassify the pinch handoff as a valid tap.
            _undoGesture.ResetTapSequence();
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

        // Multi-touch suppression is already active from the moment the second finger arrives.
        // After pinch ends, keep relying on real touch release/capture-loss events to hold that
        // suppression until every touch involved in the gesture is gone, then allow drawing
        // immediately instead of forcing an extra post-pinch cooldown.
        TryEndTouchSuppression();

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
               || _isPendingTouchPanDecision
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
        _isPendingTouchPanDecision = false;
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
        if (pointerType != PointerType.Touch)
            return false;

        TryEndTouchSuppression();
        return _isTouchDrawingSuppressed || IsUndoTapSequencePending() || IsSuppressionCooldownActive();
    }

    private bool ShouldUseTouchPan(PointerType pointerType)
    {
        return pointerType == PointerType.Touch
               && _appState.IsStylusModeEnabled
               && _appState.IsSingleFingerPanEnabled
               && _activeTouchPointers.Count == 1;
    }

    private bool ShouldAllowTouchPan(PointerType pointerType)
    {
        return pointerType == PointerType.Touch
               && _appState.IsStylusModeEnabled
               && _appState.IsSingleFingerPanEnabled
               && (_activeTouchPointers.Count == 1 || Input.PanMode);
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

    private bool ShouldResetStaleTouchStateOnPress(int pointerId)
    {
        return _activeTouchPointers.Count > 0
               && !_activeTouchPointers.Contains(pointerId)
               && !_isPointerPressed
               && !_isPinching
               && !_isUndoGestureTracking
               && !Input.PanMode;
    }

    private void BeginTouchUndoSuppression()
    {
        _isTouchDrawingSuppressed = true;
        _isPointerPressed = false;
        // A second finger / undo gesture supersedes a pending single-finger tap-vs-pan decision.
        _isPendingTouchPanDecision = false;
        _serviceProvider.GetRequiredService<IDrawingService>().CancelActiveDrawing();
        Input.CapturedPointerBy = null;
        InvalidateVisual();
    }

    private void TryEndTouchSuppression()
    {
        if (_isUndoGestureTracking)
            return;

        if (_activeTouchPointers.Count == 0 && !_isPinching && !IsSuppressionCooldownActive())
            _isTouchDrawingSuppressed = false;
    }

    private IEditService EditService => _editService ??= _serviceProvider.GetRequiredService<IEditService>();

    /// <summary>
    /// True when an interactive scene overlay (transform/resize/rotate handles, artboard name labels,
    /// object-edit handles/backdrop) sits under the given world position. Two interactive nodes report
    /// <c>ContainsPoint == true</c> everywhere and are NOT manipulation affordances — the always-present
    /// drawing layer (raster surface) and the scene <see cref="RootNode"/> — so both are excluded. What
    /// remains are the manipulation overlays a single-finger touch must be allowed to operate even while
    /// single-finger-pan is on.
    /// </summary>
    private static bool IsOverInteractiveOverlay(SKPoint worldPos)
        => Input.GetInteractives(worldPos).Any(n => n is not IDrawingLayer and not RootNode);

    private void BeginPendingTouchPanDecision()
    {
        _isPendingTouchPanDecision = true;
        _isPointerPressed = true;
        if (ViewPort != null)
            _initialPan = ViewPort.Pan;
        // _initialPos was already set to the press position by the caller.
    }


    private SKPoint ToSKPoint(Point p) => new(
        (float)((ViewPort?.ScaleFactor ?? 1f) * p.X),
        (float)((ViewPort?.ScaleFactor ?? 1f) * p.Y)
    );

    /// <summary>
    /// Converts a point in this control's coordinates (e.g. a drop position) to world coordinates,
    /// using the same scaling the pointer pipeline applies. Returns null before the viewport exists.
    /// </summary>
    public SKPoint? GetWorldPosition(Point positionInControl)
    {
        if (ViewPort == null)
            return null;

        return ViewPort.ViewportToWorld(ToSKPoint(positionInControl));
    }

    private static SKMatrix ToSKMatrix(Matrix m)
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
        private Point? _lastOrigin;

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
                    var controlOriginInTopLevel = parent._cachedOriginInTopLevel;

                    if (_lastTransform != context.CurrentTransform || _lastOrigin != controlOriginInTopLevel)
                    {
                        parent.UpdatePivotTransform(context.CurrentTransform, controlOriginInTopLevel);
                        _lastTransform = context.CurrentTransform;
                        _lastOrigin = controlOriginInTopLevel;
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

    }
}
