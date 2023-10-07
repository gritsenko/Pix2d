using Pix2d.Common;
using Pix2d.Messages.Edit;
using Pix2d.Messages;
using SkiaNodes.Interactive;

namespace Pix2d.Views;

public class InfoPanelView : ComponentBase
{
    private void LabelStyle(TextBlock t) => t
        .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily)
        .VerticalAlignment(VerticalAlignment.Center)
        .FontSize(12);

    protected override object Build() =>
        new Grid()
            .Cols("28,70,24,*")
            .IsHitTestVisible(false)
            .Height(28)
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(
                new TextBlock()
                    .Text("\xE902")
                    .Padding(8, 0, 0, 0)
                    .With(LabelStyle),

                new TextBlock().Col(1)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Text(Bind(PointerInfo)),

                new TextBlock().Col(2)
                    .Text("\xE921")
                    .With(LabelStyle),

                new TextBlock().Col(3)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Margin(0, 0, 8, 0)
                    .Text(Bind(SizeInfo))
            );

    [Inject] IMessenger Messenger { get; set; } = null!;

    [Inject] AppState AppState { get; } = null!;
    public SelectionState SelectionState => AppState.SelectionState;

    public string PointerInfo { get; set; } = "0, 0";
    public string SizeInfo { get; set; } = "0×0";

    protected override void OnAfterInitialized()
    {
        SKInput.Current.PointerChanged += CurrentOnPointerChanged;
        Messenger.Register<EditedNodeChangedMessage>(this, msg=> UpdateSelectionInfo());
        Messenger.Register<StateChangedMessage>(this, msg =>
        {
            if (msg.PropertyName is nameof(SelectionState.UserSelectingFrameSize) or nameof(SelectionState.IsUserSelecting))
                UpdateSelectionInfo();
        });
    }

    private void CurrentOnPointerChanged(object sender, SKInputPointer pointer)
    {
        DeferredAction.Run(100, () =>
        {
            var pos = pointer.WorldPosition;
            PointerInfo = (int)pos.X + ", " + (int)pos.Y;
            OnPropertyChanged(nameof(PointerInfo));
        });
    }

    private void UpdateSelectionInfo()
    {
        var size = SelectionState.IsUserSelecting ? SelectionState.UserSelectingFrameSize : AppState.CurrentProject.SelectionSize;
        SizeInfo = size.Width + "×" + size.Height;
        OnPropertyChanged(nameof(SizeInfo));
    }
}