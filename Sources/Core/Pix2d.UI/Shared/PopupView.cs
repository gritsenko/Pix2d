using System.Windows.Input;
using Avalonia.Controls.Shapes;
using CommonServiceLocator;
using Pix2d.Messages;
using Pix2d.UI.Resources;

namespace Pix2d.UI.Shared;

public class PopupView : ViewBase
{
    private static readonly TimeSpan AutoCloseTimeout = TimeSpan.FromMilliseconds(500);
    
    #region control properties

    /// <summary>
    /// Content Property
    /// </summary>
    public static readonly DirectProperty<PopupView, Control> ContentProperty
        = AvaloniaProperty.RegisterDirect<PopupView, Control>(nameof(Content), o => o.Content, (o, v) => o.Content = v);
    private Control _content = default;
    public Control Content
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
            // if (value && (DateTime.Now - _autoCloseTime < AutoCloseTimeout))
            // {
            //     SetAndRaise(IsOpenProperty, ref _isOpen, false);
            //     return;
            // }
            
            SetAndRaise(IsOpenProperty, ref _isOpen, value);
            IsVisible = value;
            if (value)
            {
                if (ShowPinButton)
                {
                    ServiceLocator.Current.GetInstance<IMessenger>().Register<WindowClickedMessage>(this, OnWindowClicked);
                }
                _onShowAction?.Invoke();
            }
            else
            {
                if (ShowPinButton)
                {
                    ServiceLocator.Current.GetInstance<IMessenger>().Unregister<WindowClickedMessage>(this, OnWindowClicked);
                }
            }
        }
    }

    /// <summary>
    /// Show Header
    /// </summary>
    public static readonly DirectProperty<PopupView, bool> ShowHeaderProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(ShowHeader), o => o.ShowHeader, (o, v) => o.ShowHeader = v);
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
    private string _header = "Header";
    public string Header
    {
        get => _header;
        set => SetAndRaise(HeaderProperty, ref _header, value);
    }

    /// <summary>
    /// CloseButtonCommand
    /// </summary>
    public static readonly DirectProperty<PopupView, ICommand> CloseButtonCommandProperty
        = AvaloniaProperty.RegisterDirect<PopupView, ICommand>(nameof(CloseButtonCommand), o => o.CloseButtonCommand, (o, v) => o.CloseButtonCommand = v);
    private ICommand _closeButtonCommand = default;
    public ICommand CloseButtonCommand
    {
        get => _closeButtonCommand;
        set => SetAndRaise(CloseButtonCommandProperty, ref _closeButtonCommand, value);
    }

    /// <summary>
    /// Show pin button
    /// </summary>
    public static readonly DirectProperty<PopupView, bool> ShowPinButtonProperty
        = AvaloniaProperty.RegisterDirect<PopupView, bool>(nameof(ShowPinButton), o => o.ShowPinButton, (o, v) => o.ShowPinButton = v);
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

    #endregion

    public IControlTemplate ThumbTemplate =
        new FuncControlTemplate((ns, c) => new Rectangle().Fill(Colors.Transparent.ToBrush()));

    public IControlTemplate ContentTemplate => 
        new FuncControlTemplate((ns, c) => this.Content);

    protected override object Build() =>
        new Grid()
            .Rows("Auto, *")
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(

                new Grid().Cols("*, Auto, Auto")
                    .IsVisible(@ShowHeaderProperty)
                    .Children(

                        new TextBlock() {IsHitTestVisible = false}
                            .Margin(8, 0, 0, 0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(@HeaderProperty, BindingMode.OneWay),

                        new ToggleButton().Col(1) // pin button
                            .IsVisible(@ShowPinButtonProperty, BindingMode.OneWay)
                            .IsChecked(IsPinned, BindingMode.TwoWay, bindingSource: this)
                            .Content(IsPinned, BindingMode.OneWay, bindingSource: this)
                            .ContentTemplate(new FuncDataTemplate<bool>((v, _) => 
                                new TextBlock()
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .Text(v ? "\xE840" : "\xE141"))),

                        new Button().Col(2) //Close button
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Command(@CloseButtonCommandProperty, BindingMode.OneWay)
                            .Content("\xE894"),

                        new Thumb()
                            .Template(ThumbTemplate)
                            .With(t => t.DragDelta += (s, e) =>
                            {
                                var pos = GetCurrentPos();
                                UpdatePosition(new Point(pos.X + e.Vector.X, pos.Y + e.Vector.Y));
                            })
                    ),

                new ContentControl().Row(1)
                    .Ref(out _contentControl)
                    .Content(@ContentProperty, BindingMode.OneWay)
                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            );

    private ContentControl _contentControl;
    private Action _onShowAction;
    private DateTime _autoCloseTime;

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

        var parent = TemplatedParent as Visual;

        if (parent != null)
        {
            var bounds = parent.Bounds;
            left = Math.Min(bounds.Width - Bounds.Width, left);
            top = Math.Min(bounds.Height - Bounds.Height, top);
        }

        Canvas.SetTop(this, top);
        Canvas.SetLeft(this, left);
    }

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
}