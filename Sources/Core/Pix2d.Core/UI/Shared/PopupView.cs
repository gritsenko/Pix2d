using System.Windows.Input;
using Avalonia.Controls.Shapes;
using Avalonia.Reactive;
using Avalonia.Threading;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Styles;

namespace Pix2d.UI.Shared;

public class PopupView(AppState appState, IMessenger messenger) : ViewBase
{
    private static readonly TimeSpan AutoCloseTimeout = TimeSpan.FromMilliseconds(500);
    private readonly State _state = new(appState, messenger);

    public event EventHandler<EventArgs>? CloseButtonClicked;

    #region control properties

    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<PopupView, Control?> ContentProperty
        = AvaloniaProperty.RegisterDirect<PopupView, Control?>(nameof(Content), o => o.Content, (o, v) => o.Content = v);

    private Control? _content = default;

    public Control? Content
    {
        get => _content;
        set => SetAndRaise(ContentProperty, ref _content, value);
    }

    /// <summary>
    /// Is open
    /// </summary>
    public static readonly DirectProperty<PopupView, bool> IsOpenProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(IsOpen), o => o.IsOpen, (o, v) => o.IsOpen = v);

    private bool _isOpen = false;

    public bool IsOpen
    {
        get => _isOpen;
        set
        {
            SetAndRaise(IsOpenProperty, ref _isOpen, value);

            if (IsVisible != value)
            {
                IsVisible = value;
                if (value)
                {
                    _state.OnOpened(this, ShowPinButton, _onShowAction, ResetPositionForCurrentLayout);
                }
                else
                {
                    _state.OnClosed(this, ShowPinButton);
                    StopObservingParentSize();
                }
            }
        }
    }

    /// <summary>
    /// Show Header
    /// </summary>
    public static readonly DirectProperty<PopupView, bool> ShowHeaderProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(ShowHeader), o => o.ShowHeader,
            (o, v) => o.ShowHeader = v);

    private bool _showHeader = true;

    public bool ShowHeader
    {
        get => _showHeader;
        set => SetAndRaise(ShowHeaderProperty, ref _showHeader, value);
    }

    /// <summary>
    /// Header
    /// </summary>
    public static readonly DirectProperty<PopupView, string> HeaderProperty
        = AvaloniaProperty.RegisterDirect<PopupView, string>(nameof(Header), o => o.Header, (o, v) => o.Header = v);

    private string _header = "HEADER";

    public string Header
    {
        get => _header;
        // The design renders all captions uppercase (Figma textCase: UPPER); Avalonia has no
        // text-transform, so normalize here.
        set => SetAndRaise(HeaderProperty, ref _header, value?.ToUpperInvariant()!);
    }

    /// <summary>
    /// CloseButtonCommand
    /// </summary>
    public static readonly DirectProperty<PopupView, ICommand> CloseButtonCommandProperty
        = AvaloniaProperty.RegisterDirect<PopupView, ICommand>(nameof(CloseButtonCommand), o => o.CloseButtonCommand,
            (o, v) => o.CloseButtonCommand = v);

    private ICommand _closeButtonCommand = null!;

    public ICommand CloseButtonCommand
    {
        get => _closeButtonCommand;
        set => SetAndRaise(CloseButtonCommandProperty, ref _closeButtonCommand, value);
    }

    /// <summary>
    /// Show pin button
    /// </summary>
    public static readonly DirectProperty<PopupView, bool> ShowPinButtonProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(ShowPinButton), o => o.ShowPinButton,
            (o, v) => o.ShowPinButton = v);

    private bool _showPinButton = false;

    public bool ShowPinButton
    {
        get => _showPinButton;
        set => SetAndRaise(ShowPinButtonProperty, ref _showPinButton, value);
    }

    public static readonly DirectProperty<PopupView, bool> IsPinnedProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(IsPinned), o => o.IsPinned, (o, v) => o.IsPinned = v);

    private bool _isPinned = false;

    public bool IsPinned
    {
        get => _isPinned;
        set => SetAndRaise(IsPinnedProperty, ref _isPinned, value);
    }

    public static readonly DirectProperty<PopupView, bool> CenterOnNarrowScreenProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(CenterOnNarrowScreen), o => o.CenterOnNarrowScreen,
            (o, v) => o.CenterOnNarrowScreen = v);

    private bool _centerOnNarrowScreen;

    public bool CenterOnNarrowScreen
    {
        get => _centerOnNarrowScreen;
        set => SetAndRaise(CenterOnNarrowScreenProperty, ref _centerOnNarrowScreen, value);
    }

    #endregion

    public IControlTemplate ThumbTemplate =
        new FuncControlTemplate((ns, c) => new Rectangle().Fill(Colors.Transparent.ToBrush()));

    protected override object Build() =>
        BuildPopup(null);

    protected object BuildPopup(Func<Control>? contentFunc)
    {
        if (contentFunc != null) Content = contentFunc();

        return new BlurPanel()
            .Ref(out _popupRoot)
            .DisableBlur(false)
            .BackgroundBrush(StaticResources.Brushes.PopupBackgroundBrush)
            .ClipToBounds(true)
            .Content(
                new Grid()
                    .Rows("44, *")
                    .Children(
                        new Grid().Cols("*, Auto, Auto")
                            .IsVisible(this, x => x.ShowHeader, BindingMode.OneWay)
                            .Children(
                                new TextBlock() { IsHitTestVisible = false }
                                    .Classes("body11")
                                    .Margin(8, 0, 0, 0)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Text(this, x => x.Header, BindingMode.OneWay),
                                new ToggleButton().Col(1) // pin button
                                    .Classes("small-button")
                                    .Width(StaticResources.Measures.SmallButtonSize)
                                    .Height(StaticResources.Measures.SmallButtonSize)
                                    .CornerRadius(new CornerRadius(StaticResources.Measures.SmallButtonCornerRadius))
                                    .Margin(4)
                                    .IsVisible(this, x => x.ShowPinButton, BindingMode.OneWay)
                                    .IsChecked(this, x => x.IsPinned, BindingMode.TwoWay)
                                    .Content(this, x => x.IsPinned, BindingMode.OneWay)
                                    .ContentTemplate(new FuncDataTemplate<bool>((v, _) =>
                                        new TextBlock()
                                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                            .FontSize(14)
                                            .Text(v ? "\xE840" : "\xE141"))),
                                new Button().Col(2) //Close button
                                    .Classes("small-button")
                                    .FontSize(14)
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .Command(this, x => x.CloseButtonCommand, BindingMode.OneWay)
                                    .OnClick(_ => CloseButtonClicked?.Invoke(this, EventArgs.Empty))
                                    .Content("\xE894"),
                                new Thumb()
                                    .Template(ThumbTemplate)
                                    .With(t => t.DragDelta += (s, e) =>
                                    {
                                        var pos = GetCurrentPos();
                                        UpdatePosition(new Point(pos.X + e.Vector.X, pos.Y + e.Vector.Y));
                                    })
                            ),
                        new ScrollViewer().Row(1)
                            .Ref(out _contentScrollViewer)
                            .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                            .HorizontalScrollBarVisibility(ScrollBarVisibility.Disabled)
                            .Content(
                                new ContentControl()
                                    .Ref(out _contentControl)
                                    .Content(this, x => x.Content, BindingMode.OneWay)
                                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                            )
                    )
            );
    }

    private BlurPanel _popupRoot = null!;
    private ContentControl _contentControl = null!;
    private ScrollViewer _contentScrollViewer = null!;
    private Action _onShowAction = null!;
    private DateTime _autoCloseTime;

    // Author-set MinWidth floor, captured on first constraint pass so the narrow-screen clamp can be
    // undone when the viewport grows back. NaN = not captured yet.
    private double _baseMinWidth = double.NaN;
    // Live subscription to the positioning parent's size while the popup is open, so an already-open
    // dialog re-fits when the window is resized or the device is rotated.
    private IDisposable? _parentBoundsSub;
    // Set BEFORE subscribing: Avalonia's GetObservable pushes the current value synchronously on
    // Subscribe, and that first callback runs before _parentBoundsSub is assigned — this flag stops it
    // from re-entering EnsureParentSizeSubscription and subscribing again (infinite recursion).
    private bool _parentSizeSubscribed;

    private Point GetCurrentPos()
    {
        var x = Canvas.GetLeft(this);
        var y = Canvas.GetTop(this);

        if (double.IsNaN(x)) x = 0;
        if (double.IsNaN(y)) y = 0;
        return new Point(x, y);
    }

    public void UpdatePosition(Point pos)
    {
        var top = Math.Max(0, pos.Y);
        var left = Math.Max(0, pos.X);

        var parent = GetPositioningParent();

        if (parent != null)
        {
            var bounds = parent.Bounds;
            left = Math.Min(Math.Max(0, bounds.Width - Bounds.Width), left);
            top = Math.Min(Math.Max(0, bounds.Height - Bounds.Height), top);
        }

        Canvas.SetTop(this, top);
        Canvas.SetLeft(this, left);
    }

    public PopupView UseCenteredPositionOnNarrowScreen(bool value)
    {
        CenterOnNarrowScreen = value;
        return this;
    }

    public void ResetPositionForCurrentLayout()
    {
        Dispatcher.UIThread.Post(ApplyPositionForCurrentLayout, DispatcherPriority.Loaded);
    }

    // Re-fit the popup whenever the viewport it sits in changes size (window resize / device rotation)
    // so the width clamp in UpdateSizeConstraints keeps it inside a now-narrower screen. Bound lazily
    // from ApplyPositionForCurrentLayout (posted at Loaded priority) rather than the IsOpen setter,
    // because a dialog shown during startup may not be attached to the visual tree yet when it opens —
    // at that point GetPositioningParent() is still null and there would be nothing to observe.
    private void EnsureParentSizeSubscription()
    {
        if (_parentSizeSubscribed)
            return;
        if (GetPositioningParent() is { } parent)
        {
            _parentSizeSubscribed = true;
            _parentBoundsSub = parent.GetObservable(BoundsProperty)
                .Subscribe(new AnonymousObserver<Rect>(_ =>
                {
                    if (IsOpen) ApplyPositionForCurrentLayout();
                }));
        }
    }

    private void StopObservingParentSize()
    {
        _parentBoundsSub?.Dispose();
        _parentBoundsSub = null;
        _parentSizeSubscribed = false;
    }

    private void ApplyPositionForCurrentLayout()
    {
        UpdateSizeConstraints();
        EnsureParentSizeSubscription();

        if (!IsOpen || !_state.ShouldCenterOnNarrowScreen(CenterOnNarrowScreen))
            return;

        var parent = GetPositioningParent();
        if (parent == null || Bounds.Width <= 0 || Bounds.Height <= 0 || parent.Bounds.Width <= 0 || parent.Bounds.Height <= 0)
            return;

        UpdatePosition(new Point((parent.Bounds.Width - Bounds.Width) / 2, (parent.Bounds.Height - Bounds.Height) / 2));
    }

    private void UpdateSizeConstraints()
    {
        var parent = GetPositioningParent();
        if (parent == null || parent.Bounds.Height <= 0)
            return;

        var availableHeight = Math.Max(44, parent.Bounds.Height - StaticResources.Measures.PanelMargin * 2);
        var headerHeight = ShowHeader ? 44d : 0d;

        _popupRoot.MaxHeight = availableHeight;
        _contentScrollViewer.MaxHeight = Math.Max(0, availableHeight - headerHeight);

        // Cap the width to the viewport too (not just the height): on a narrow phone-portrait window a
        // dialog/popup whose content asks for a fixed width wider than the screen would otherwise spill
        // past the edges (the content scroll viewer disables horizontal scrolling, so it clips instead).
        if (parent.Bounds.Width > 0)
        {
            var availableWidth = Math.Max(44, parent.Bounds.Width - StaticResources.Measures.PanelMargin * 2);
            _popupRoot.MaxWidth = availableWidth;

            // A fixed MinWidth floor (e.g. the dialog host's 300px minimum) must not out-vote the
            // viewport on a very narrow phone screen — MinWidth wins over MaxWidth in Avalonia's
            // measure, so without this the popup would stay its floor width and clip. Clamp it to the
            // viewport, remembering the author-set floor so it restores when the window grows again.
            if (double.IsNaN(_baseMinWidth))
                _baseMinWidth = MinWidth;
            MinWidth = Math.Min(_baseMinWidth, availableWidth);
        }
    }

    private Visual? GetPositioningParent() => Parent as Visual;

    protected override void OnBeforeReload()
    {
        _contentControl.Content = null;
    }

    public Control OnShow(Action action)
    {
        _onShowAction = action;
        return this;
    }

    private void OnWindowClicked(WindowClickedMessage message)
    {
        if (!IsPinned
            && IsOpen
            && message.Target is StyledElement styledElement
            && !IsInside(styledElement))
        {
            _autoCloseTime = DateTime.Now;
            IsOpen = false;
        }
    }

    private bool IsInside(StyledElement element)
    {
        return ReferenceEquals(element, this) || element.Parent != null && IsInside(element.Parent);
    }

    public void CloseUnpinned(CloseUnpinnedPopups closeUnpinnedPopups)
    {
        if (!IsPinned)
        {
            IsOpen = false;
        }
    }

    private sealed class State(AppState appState, IMessenger messenger)
    {
        private readonly AppState _appState = appState;
        private readonly IMessenger _messenger = messenger;

        public void OnOpened(PopupView popupView, bool showPinButton, Action? onShowAction, Action resetPositionAction)
        {
            if (showPinButton)
            {
                _messenger.Register<WindowClickedMessage>(popupView, popupView.OnWindowClicked);
                _messenger.Register<CloseUnpinnedPopups>(popupView, popupView.CloseUnpinned);
            }

            onShowAction?.Invoke();
            resetPositionAction();
        }

        public void OnClosed(PopupView popupView, bool showPinButton)
        {
            if (!showPinButton)
                return;

            _messenger.Unregister<WindowClickedMessage>(popupView, popupView.OnWindowClicked);
            _messenger.Unregister<CloseUnpinnedPopups>(popupView, popupView.CloseUnpinned);
        }

        public bool ShouldCenterOnNarrowScreen(bool centerOnNarrowScreen)
        {
            return centerOnNarrowScreen && _appState.UiState.VisualState == nameof(VisualStates.Narrow);
        }
    }
}