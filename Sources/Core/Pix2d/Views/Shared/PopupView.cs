using System;
using System.Windows.Input;
using Avalonia.Controls.Shapes;

namespace Pix2d.Shared;

public class PopupView : ViewBase
{
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
            SetAndRaise(IsOpenProperty, ref _isOpen, value);
            IsVisible = value;
            if (value)
                _onShowAction?.Invoke();
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
                    .IsVisible(Bind(ShowHeaderProperty))
                    .Children(

                        new TextBlock() { IsHitTestVisible = false }
                            .Margin(8,0,0,0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(Bind(HeaderProperty, BindingMode.OneWay)),

                        new Button().Col(1) // pin button
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .IsVisible(Bind(ShowPinButtonProperty, BindingMode.OneWay))
                            .Content("\xE840"),

                        new Button().Col(2) //Close button
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Command(Bind(CloseButtonCommandProperty, BindingMode.OneWay))
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
                    .Content(Bind(ContentProperty, BindingMode.OneWay))
                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            );

    private ContentControl _contentControl;
    private Action _onShowAction;

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
}