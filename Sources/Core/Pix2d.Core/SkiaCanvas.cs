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
using Pix2d.Primitives.ViewPort;
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
    private Cursor? _pickerCursor;
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
    // Alt keys currently held down; picker mode (#184) stays on until all are released.
    private readonly HashSet<Key> _heldAltKeys = [];
    private long _touchSuppressionUntilMs;

    // Single-finger-pan mode only: when a one-finger press lands on a different (inactive) artboard we
    // can't yet tell whether the user means to activate it (tap) or scroll the canvas (drag). We defer
    // the decision — pan once movement passes the threshold, otherwise treat the release as a tap and
    // route it into the normal pipeline so the click-to-activate resolver switches the active sprite.
    private bool _isPendingTouchPanDecision;
    private const double TouchPanActivationThresholdPx = 8;
    // TODO: 3-finger gesture doesn't work reliably on Android/Windows - need alternative approach
    //private readonly MultiFingerGestureRecognizer _redoGesture = new() { FingersCount = 3, TapCount = 2, RoutedEventToRaise = RedoGestureEvent };

    // Tells a trackpad's two-finger scroll apart from a mouse wheel — see PrecisionScrollDetector for why
    // this has to be inferred. Drives which of the two wheel semantics OnPointerWheelChanged applies.
    private readonly PrecisionScrollDetector _precisionScroll = new();

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
        // Switching tools clears the transient Alt color-pick mode so its cursor/highlight can't stick (#184).
        _appState.ToolsState.WatchFor(x => x.CurrentToolKey, () => SetColorPickerMode(false));

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

        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _heldAltKeys.Add(e.Key);
            SetColorPickerMode(IsBrushFamilyToolActive());
        }

        Input.SetKeyPressed(key, ToKeyboardModifiers(e.KeyModifiers));
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        var key = ToVirtualKeys(e.Key);
        if (Input.EnablePanWithSpace && key == VirtualKeys.Space)
        {
            Input.PanMode = false;
            UpdateCursor();
        }

        // Only leave color-picker mode once *every* held Alt key is up — releasing one Alt while the other
        // is still down (or LeftAlt/RightAlt reported separately) must not clear the mode prematurely.
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            _heldAltKeys.Remove(e.Key);
            if (_heldAltKeys.Count == 0)
                SetColorPickerMode(false);
        }

        Input.SetKeyReleased(key, ToKeyboardModifiers(e.KeyModifiers));
    }

    // Holding Alt over a brush-family tool (Brush/Eraser/pixel Shape) makes it pick a color instead of
    // draw — see PixelBrushToolBase. Reflect that transient mode so the cursor and the toolbar show it (#184).
    private bool IsBrushFamilyToolActive()
        => _appState.ToolsState.CurrentTool?.ToolInstance is PixelBrushToolBase;

    private void SetColorPickerMode(bool active)
    {
        if (_appState.ToolsState.IsColorPickerModeActive == active)
            return;

        _appState.ToolsState.IsColorPickerModeActive = active;
        UpdateCursor();
    }

    private void OnHoverCursorChanged(object? sender, EventArgs e) => UpdateCursor();

    private void UpdateCursor()
    {
        // Order matters: pan and the Alt color-picker are modes the user explicitly asked for, so they
        // outrank a node's hover request (SKInput.HoverCursor), which is only a hint from the scene graph.
        if (Input.PanMode)
        {
            _cursor ??= new Cursor(StandardCursorType.Hand);
            Cursor = _cursor;
        }
        else if (_appState.ToolsState.IsColorPickerModeActive)
        {
            _pickerCursor ??= new Cursor(StandardCursorType.Cross);
            Cursor = _pickerCursor;
        }
        else if (Input.HoverCursor == SKCursorType.Hand)
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

    /// <summary>
    /// Modifiers for the keyboard path. Every shortcut in the app is declared with <see cref="KeyModifier.Ctrl"/>,
    /// but on macOS the platform's shortcut modifier is Cmd — which Avalonia reports as
    /// <see cref="KeyModifiers.Meta"/> and the plain cast turns into <see cref="KeyModifier.Win"/>, matching no
    /// declaration. Folding Cmd into Ctrl there makes Cmd+D / Cmd+S / Cmd+Z work without duplicating every
    /// binding (Ctrl keeps working too). Pointer modifiers stay unmapped on purpose: there Ctrl means
    /// aspect-lock / invert-the-wheel, not "the menu key".
    /// </summary>
    private static KeyModifier ToKeyboardModifiers(KeyModifiers keyModifiers)
    {
        var modifiers = ToModifiers(keyModifiers);

        if (OperatingSystem.IsMacOS() && (modifiers & KeyModifier.Win) != 0)
            modifiers = (modifiers & ~KeyModifier.Win) | KeyModifier.Ctrl;

        return modifiers;
    }

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
        // -= first: SKInput is a process-wide singleton while this control is not (hot reload rebuilds it,
        // and a second window would bring its own), so a plain += would leak a handler per canvas.
        Input.HoverCursorChanged -= OnHoverCursorChanged;
        Input.HoverCursorChanged += OnHoverCursorChanged;
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

    /// <summary>
    /// Builds the pivot matrix for <em>this</em> render pass: the transform that takes the scene, which is
    /// laid out in <b>physical pixels</b> (<see cref="ViewPort.TransformMatrix"/> bakes in
    /// <see cref="ViewPort.ScaleFactor"/>, matching <see cref="ToSKPoint"/> on the input side), into the
    /// coordinate space of whatever surface is being drawn.
    /// <para>
    /// <paramref name="deviceTransform"/> is the leased canvas' own matrix at entry — the DIP→target mapping
    /// Skia is already set up for. Dividing it by <see cref="ViewPort.ScaleFactor"/> converts it into a
    /// physical→target mapping, which is what the scene needs. On the window surface (target = physical
    /// pixels) that leaves the translation only, i.e. exactly the historical behaviour; in an offscreen
    /// <c>RenderTargetBitmap</c> pass, whose space is DIP, it also scales the scene down so screenshots
    /// stop drawing the canvas contents at <c>ScaleFactor</c>× their position and size on a HiDPI display.
    /// </para>
    /// </summary>
    private void UpdatePivotTransform(Matrix currentTransform, SKMatrix deviceTransform, Point? controlOriginInTopLevel)
    {
        if (ViewPort == null)
            return;

        var scale = ViewPort.ScaleFactor <= 0 ? 1f : ViewPort.ScaleFactor;

        // Degenerate device matrix (never seen in practice, but a zero scale would collapse the scene):
        // fall back to the transform Avalonia reports for the pass, which is what this used to read.
        var deviceScaleX = Math.Abs(deviceTransform.ScaleX) < 0.0001f ? 1f : deviceTransform.ScaleX;
        if (Math.Abs(deviceTransform.ScaleX) < 0.0001f || Math.Abs(deviceTransform.ScaleY) < 0.0001f)
        {
            deviceTransform = ToSKMatrix(currentTransform);
            deviceScaleX = Math.Abs(deviceTransform.ScaleX) < 0.0001f ? 1f : deviceTransform.ScaleX;
        }

        var pivotTransform = deviceTransform.PreConcat(SKMatrix.CreateScale(1f / scale, 1f / scale));

        // On some platforms the custom draw op is handed a transform without the control's translation
        // inside the TopLevel: on Android the safe-area inset is missing on the first frames, and on
        // Windows the compositor hands us an identity transform always. Without a correction the scene
        // renders at the window origin, which became visible once the project tabs row pushed the canvas
        // down. Fall back to the cached control origin (kept fresh via ArrangeCore / LayoutUpdated /
        // SafeAreaChanged) whenever the supplied translation is zero.
        //
        // Only for a pass drawing into the window surface, though — recognised by its scale matching the
        // viewport's, since that surface is the one measured in physical pixels. A pass rendering the
        // canvas *in isolation* (`screenshot_control`) legitimately has no translation: its target's origin
        // IS the control, so adding the control's window origin there is what used to push the scene down
        // by the height of the tab strip + top bar.
        //
        // Known limitation: at render scaling 1 the two are indistinguishable by scale alone (an isolated
        // DIP pass and a Windows on-screen pass that reports no translation both look like scale 1), so
        // there the isolated capture keeps that offset. Erring that way is deliberate — the alternative
        // (classifying by the canvas' device clip) risks the *on-screen* scene, since a partial repaint or
        // Android's first frames legitimately clip from the origin.
        if (controlOriginInTopLevel.HasValue && Math.Abs(deviceScaleX - scale) < 0.01f)
        {
            var fallbackTransX = (float)(controlOriginInTopLevel.Value.X * deviceScaleX);
            var fallbackTransY = (float)(controlOriginInTopLevel.Value.Y * deviceScaleX);

            if (Math.Abs(pivotTransform.TransX) < 0.01f && Math.Abs(fallbackTransX) > 0.01f)
                pivotTransform.TransX = fallbackTransX;

            if (Math.Abs(pivotTransform.TransY) < 0.01f && Math.Abs(fallbackTransY) > 0.01f)
                pivotTransform.TransY = fallbackTransY;
        }

        ViewPort.PivotTransformMatrix = pivotTransform;

#if DEBUG
        LogPivotOnChange(currentTransform, deviceTransform, pivotTransform, scale, controlOriginInTopLevel);
#endif
    }

#if DEBUG
    private static string? _lastLoggedPivot;

    /// <summary>
    /// On a HiDPI display the on-screen and offscreen passes must end up with *different* pivots (the
    /// window surface is physical pixels, a <c>RenderTargetBitmap</c>'s is DIP). Logging each distinct
    /// combination — deduplicated, so a 60 fps repaint prints nothing — is how that can be checked against
    /// a running build via the inspector's <c>get_logs</c>, which is the only honest verification available
    /// when the thing under test is the screenshot path itself.
    /// </summary>
    private static void LogPivotOnChange(Matrix currentTransform, SKMatrix deviceTransform, SKMatrix pivot,
        float scale, Point? origin)
    {
        var line = $"[Pix2d] canvas pivot: device=[{deviceTransform.ScaleX:0.###} {deviceTransform.ScaleY:0.###} " +
                   $"{deviceTransform.TransX:0.##} {deviceTransform.TransY:0.##}] " +
                   $"avalonia=[{currentTransform.M11:0.###} {currentTransform.M22:0.###} " +
                   $"{currentTransform.M31:0.##} {currentTransform.M32:0.##}] " +
                   $"vpScale={scale:0.###} origin={(origin.HasValue ? $"{origin.Value.X:0.##},{origin.Value.Y:0.##}" : "-")} " +
                   $"→ pivot=[{pivot.ScaleX:0.###} {pivot.ScaleY:0.###} {pivot.TransX:0.##} {pivot.TransY:0.##}]";

        if (line == _lastLoggedPivot)
            return;

        _lastLoggedPivot = line;
        Console.WriteLine(line);
    }
#endif

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

        var isCtrlDown = (e.KeyModifiers & KeyModifiers.Control) != 0;
        var isShiftDown = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        // A precision device (trackpad) always scrolls, whatever the setting says: two-finger scroll pans,
        // and zooming is the pinch gesture (OnPointerTouchPadGestureMagnify) or Ctrl+scroll — the same
        // contract every other app offers. The "mouse wheel behavior" setting therefore governs a notched
        // wheel only, which is the device the user was thinking of when they set it.
        var isPrecisionScrolling = _precisionScroll.Observe(e.Delta.X, e.Delta.Y, e.Timestamp);
        var isZoomMode = !isPrecisionScrolling
            && _appState.MouseWheelBehavior == MouseWheelBehavior.Zoom;

        // Zoom mode: either modifier temporarily turns the wheel back into scrolling, so the image can be
        // panned without leaving the mode — Ctrl and plain Shift scroll vertically, Ctrl+Shift horizontally.
        // Scroll mode is unchanged: Ctrl is the zoom modifier and Shift the horizontal-scroll one.
        var shouldZoom = isZoomMode ? !isCtrlDown && !isShiftDown : isCtrlDown;
        var shouldScrollHorizontally = isShiftDown && (!isZoomMode || isCtrlDown);

        if (shouldZoom)
        {
            if (e.Delta.Y > 0)
                ViewPort.ZoomIn(zoomOrigin);
            else if (e.Delta.Y < 0)
                ViewPort.ZoomOut(zoomOrigin);
        }
        else if (shouldScrollHorizontally && scroll.Y != 0 && scroll.X == 0)
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

        // Only a touchpad produces this event, so it also settles the wheel-vs-trackpad question for the
        // scroll events around it (a pinch is usually the first thing a trackpad user does on a canvas).
        _precisionScroll.NotifyTouchPadGesture();

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

        public void Dispose()
        {
            // No-op
        }

        /// <summary>
        /// Draws the scene into the canvas leased for <em>this</em> render pass.
        /// </summary>
        /// <remarks>
        /// The lease must be taken per call and held for the whole draw: the <see cref="SKCanvas"/> it
        /// hands out belongs to the render session that created it, and is not valid outside it. An
        /// earlier version cached the canvas across calls, which worked by accident for the on-screen
        /// window (same surface every frame) but broke every other render target: a pass driven by
        /// <c>RenderTargetBitmap.Render</c> — which is how screenshots are taken, including the
        /// AgentTools inspector's — drew into the stale surface instead, so the captured image had a
        /// blank hole where the canvas should be.
        /// </remarks>
        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            try
            {
                using var lease = leaseFeature.Lease();
#if DEBUG
                LogRenderBackendOnce(lease);
#endif
                var canvas = lease.SkCanvas;
                var restorePoint = canvas.Save();
                try
                {
                    canvas.Clear(_bgColor);

                    if (parent is { _rootNode: not null, ViewPort: not null })
                    {
                        var controlOriginInTopLevel = parent._cachedOriginInTopLevel;

                        // The pivot describes THIS pass (see UpdatePivotTransform): the window surface is
                        // physical pixels while an offscreen RenderTargetBitmap's is DIP, so it must be
                        // recomputed per pass from the canvas' own matrix — and it lives on the shared
                        // ViewPort, so restore the previous one afterwards or a screenshot leaves the
                        // on-screen frames drawing with an offscreen pivot.
                        var previousPivot = parent.ViewPort.PivotTransformMatrix;
                        parent.UpdatePivotTransform(context.CurrentTransform, canvas.TotalMatrix, controlOriginInTopLevel);

                        try
                        {
                            SKNodeRenderer.Render(parent._rootNode, new RenderContext(canvas, parent.ViewPort));
                        }
                        finally
                        {
                            if (parent.ViewPort != null)
                                parent.ViewPort.PivotTransformMatrix = previousPivot;
                        }
                    }
                }
                finally
                {
                    // Restore even if the scene throws: the canvas is shared with the rest of the frame,
                    // so an unbalanced Save() would leak our clip/matrix into everything drawn after us.
                    canvas.RestoreToCount(restorePoint);
                }
            }
            catch (ObjectDisposedException)
            {
                //ignore this. nothing we can do actually
            }
        }

#if DEBUG
        private static bool _renderBackendLogged;

        /// <summary>
        /// Reports, once per session, whether the canvas is drawn by the GPU and through which Skia
        /// backend. A null <see cref="ISkiaSharpApiLease.GrContext"/> means Avalonia fell all the way
        /// back to CPU rasterization; OpenGl here on Windows-on-ARM means the ANGLE path won, which
        /// is the one Avalonia's Adreno blocklist redirects to the software adapter (see
        /// Pix2d.Desktop's ConfigureWindowsRendering).
        /// </summary>
        private static void LogRenderBackendOnce(ISkiaSharpApiLease lease)
        {
            if (_renderBackendLogged)
                return;

            _renderBackendLogged = true;
            var grContext = lease.GrContext;
            Console.WriteLine(grContext == null
                ? "[Pix2d] Canvas render backend: CPU (no GrContext)"
                : $"[Pix2d] Canvas render backend: GPU / {grContext.Backend}");
        }
#endif
    }
}
