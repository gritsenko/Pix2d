using System.Diagnostics;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace SkiaNodes.Interactive;

public class SKInput
{
    public Func<SKNode>? RootNodeProvider { get; set; }
    public Func<ViewPort>? ViewPortProvider { get; set; }


    private KeyModifier _keyModifiers;
    private bool _panMode;
    private SKInputPointer _pointer;
    private static SKInput? _instance;

    private SKCursorType _hoverCursor;

    public event EventHandler<KeyboardActionEventArgs>? KeyPressed;
    public event EventHandler<KeyboardActionEventArgs>? KeyReleased;
    public event EventHandler<RootNodeChangedEventArgs>? RootNodeChanged;
    public event EventHandler<SKInputPointer>? PointerChanged;

    /// <summary>Raised only when <see cref="HoverCursor"/> actually changes, so the host can set its cursor there.</summary>
    public event EventHandler? HoverCursorChanged;

    /// <summary>
    /// What the topmost interactive node under the pointer wants the cursor to be, recomputed on every
    /// pointer move. The host control (Pix2d's <c>SkiaCanvas</c>) maps it onto a real cursor — the scene
    /// graph has no other way to influence it, since the whole canvas is one Avalonia control.
    /// </summary>
    public SKCursorType HoverCursor
    {
        get => _hoverCursor;
        private set
        {
            if (_hoverCursor == value)
                return;

            _hoverCursor = value;
            HoverCursorChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static SKInput Current => _instance ??= new SKInput();

    public IInteractive? CapturedPointerBy { get; set; }
    private IInteractive? LastInteractiveUnderPointer { get; set; }

    public SKInputPointer Pointer
    {
        get => _pointer;
        private set
        {
            _pointer = value;
            PointerChanged?.Invoke(this, _pointer);
        }
    }

    public bool EnablePanWithSpace => true;

    public bool PanMode
    {
        get => _panMode;
        set
        {
            if (_panMode != value)
            {
                _panMode = value;

                OnPanModeChanged(value);
            }
        }
    }

    public bool IsInitialized => RootNodeProvider != null && ViewPortProvider != null;
    public bool EraserMode { get; set; }

    /// <summary>
    /// Normalized pressure [0..1] for the next pointer snapshot. Set by the host control from the
    /// platform pointer (real value for a stylus pen, <c>1</c> for mouse/touch) before each
    /// Set* call, mirroring how <see cref="EraserMode"/> is fed in.
    /// </summary>
    public float Pressure { get; set; } = 1f;

    private void OnPanModeChanged(bool value)
    {
        foreach (var interactive in GetInteractives(Pointer.WorldPosition))
        {
            if (CapturedPointerBy == null || CapturedPointerBy == interactive)
            {
                interactive.OnPanModeChanged(value);
            }
        }
    }

    public void SetPointerPressed(SKPoint pos, KeyModifier modifiers, bool isTouch, int clickCount = 1)
    {
        if (!IsInitialized)
            return;

        Pointer = new SKInputPointer(pos, GetViewport()!, true, EraserMode, isTouch, Pressure);
        var args = new PointerActionEventArgs(PointerActionType.Pressed, Pointer, modifiers);

        HandlePointerEventByInteractives((interactive) =>
        {
            interactive?.OnPointerPressed(args, clickCount);
        }, args);
    }

    private ViewPort? GetViewport() => ViewPortProvider?.Invoke();

    private void HandlePointerEventByInteractives(Action<IInteractive> handler, PointerActionEventArgs args)
    {
        var interactives = GetInteractives(Pointer.WorldPosition);

        foreach (var interactive in interactives)
        {
            if (CapturedPointerBy == null || CapturedPointerBy == interactive)
                handler.Invoke(interactive);

            if (args.Handled)
            {
                //                    Debug.WriteLine("Handled by :" + interactive.GetType().Name);
                break;
            }
        }
    }

    public void SetPointerReleased(SKPoint pos, KeyModifier modifiers, bool isTouch)
    {
        if (!IsInitialized)
            return;

        Pointer = new SKInputPointer(pos, GetViewport()!, false, EraserMode, isTouch, Pressure);
        var args = new PointerActionEventArgs(PointerActionType.Released, Pointer, modifiers);

        HandlePointerEventByInteractives((interactive) =>
        {
            interactive?.OnPointerReleasedCore(args);
        }, args);
    }

    public void SetPointerMoved(SKPoint pos, bool isPointerPressed, KeyModifier modifiers, bool isTouch)
    {
        if (!IsInitialized)
            return;

        Pointer = new SKInputPointer(pos, GetViewport()!, isPointerPressed, EraserMode, isTouch, Pressure);
        var worldPos = Pointer.WorldPosition;

        var args = new PointerActionEventArgs(PointerActionType.Moved, Pointer, modifiers);

        // Resolved from the same pass rather than from OnPointerEnter/Leave: the dispatch below walks the
        // whole chain under the pointer (topmost first) unless one of them handles the event, so a node
        // gets Enter and Leave within a single move as soon as anything interactive sits beneath it.
        var hoverCursor = SKCursorType.Default;

        HandlePointerEventByInteractives((interactive) =>
        {
            interactive?.OnPointerMoved(args);
            if (interactive != LastInteractiveUnderPointer)
            {
                LastInteractiveUnderPointer?.OnPointerLeave(worldPos);
                LastInteractiveUnderPointer = interactive;
                LastInteractiveUnderPointer?.OnPointerEnter(worldPos);
            }

            if (hoverCursor == SKCursorType.Default && interactive is SKNode node)
                hoverCursor = node.GetHoverCursor(worldPos);

        }, args);

        HoverCursor = hoverCursor;
    }

    public IEnumerable<IInteractive> GetInteractives(SKPoint pos)
    {
        var rootNode = RootNodeProvider?.Invoke();
        if (rootNode == null)
            return [];

        return rootNode
            .GetVisibleDescendants(x => (x.IsInteractive && x.ContainsPoint(pos)) || x == CapturedPointerBy, true, true)
            .Reverse();
    }

    public void CapturePointer(SKNode catchedBy)
    {
        CapturedPointerBy = catchedBy;
    }

    public void ReleasePointer(SKNode catchedBy)
    {
        if (CapturedPointerBy == catchedBy)
            CapturedPointerBy = null;
    }

    public bool SetKeyPressed(VirtualKeys key, KeyModifier keyModifiers)
    {
        var activeKeyModifier = key.ToModifier();
        if (activeKeyModifier == KeyModifier.None)
        {
            _keyModifiers = keyModifiers;
        }
        else
        {
            // The modifier key was released. CrossPlatformDesktop reports modifier to be still inactive but we need to set
            // is as currently active.
            _keyModifiers |= activeKeyModifier;
        }

        return OnKeyPressed(new KeyboardActionEventArgs(key, keyModifiers));
    }

    public bool SetKeyReleased(VirtualKeys key, KeyModifier keyModifiers)
    {
        var activeKeyModifier = key.ToModifier();
        if (activeKeyModifier == KeyModifier.None)
        {
            _keyModifiers = keyModifiers;
        }
        else
        {
            // The modifier key was released. CrossPlatformDesktop reports modifier to be still active but we need to set
            // is as currently inactive.
            _keyModifiers &= ~activeKeyModifier;
        }

        return OnKeyReleased(new KeyboardActionEventArgs(key, keyModifiers));
    }

    private bool OnKeyPressed(KeyboardActionEventArgs e)
    {
        if (KeyPressed == null)
            return false;

        var ds = KeyPressed.GetInvocationList();
        foreach (var d in ds.OfType<EventHandler<KeyboardActionEventArgs>>())
        {
            d.Invoke(this, e);
            if (e.Handled)
            {
                Debug.WriteLine($"Key pressed {e.Key} processed by " + d.Target?.GetType().Name);
                break;
            }
        }
        return e.Handled;
    }

    protected virtual bool OnKeyReleased(KeyboardActionEventArgs e)
    {
        if (KeyReleased == null)
            return false;

        var ds = KeyReleased.GetInvocationList();
        foreach (var d in ds.OfType<EventHandler<KeyboardActionEventArgs>>())
        {
            d.Invoke(this, e);
            if (e.Handled)
            {
                Debug.WriteLine($"Key released {e.Key} processed by " + d.Target?.GetType().Name);
                break;
            }
        }

        return e.Handled;
    }

    public KeyModifier GetModifiers()
    {
        return _keyModifiers;
    }

    protected virtual void OnRootNodeChanged(RootNodeChangedEventArgs e)
    {
        RootNodeChanged?.Invoke(this, e);
    }
}
