using Avalonia.Interactivity;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;

namespace Pix2d.UI.ToolBar;

public class ToolItemView : LocalizedComponentBase
{
    private ToolState _toolState;

    public ToolItemView(ToolState toolState)
    {
        _toolState = toolState;
        Initialize();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppState.CurrentProject.WatchFor(x => x.IsAnimationPlaying, StateHasChanged);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppState.CurrentProject.Unwatch(x => x.IsAnimationPlaying, StateHasChanged);
    }

    protected override object Build() =>
        new Button()
            .ToolTip(L(_toolState?.ToolTip)())
            .Classes("toolbar-button")
            .BindClass(IsSelected, "selected", bindingSource: this)
            .OnClick(OnButtonClicked)
            .IsEnabled(() => !AppState.CurrentProject.IsAnimationPlaying || ToolState.EnabledDuringAnimation)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch)
            .VerticalContentAlignment(VerticalAlignment.Stretch)
            .ClipToBounds(true)
            .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
            .Content(
                new Border()
                    .CornerRadius(StaticResources.Measures.ToolItemCornerRadius)
                    .ClipToBounds(true)
                    .Child(
                        new Grid()
                            .Children(
                                new ContentControl()
                                    .Name("tool-item-border")
                                    .DataTemplates(StaticResources.Templates.ToolIconTemplateSelector)
                                    .HorizontalContentAlignment(HorizontalAlignment.Stretch)
                                    .VerticalContentAlignment(VerticalAlignment.Stretch)
                                    .Content(() => ToolIconKey)
                                //new Rectangle()
                                //    .RadiusX(StaticResources.Measures.PipkaCornerRadius)
                                //    .RadiusY(StaticResources.Measures.PipkaCornerRadius)
                                //    .Fill(Colors.White.WithAlpha(0.3f).ToBrush())
                                //    .Width(8)
                                //    .Height(8)
                                //    .Stretch(Stretch.Fill)
                                //    .VerticalAlignment(VerticalAlignment.Bottom)
                                //    .HorizontalAlignment(HorizontalAlignment.Right)
                                //    .IsVisible(() => ShowProperties)
                            )
                    )
            );

    [Inject] IToolService ToolService { get; set; } = null!;
    [Inject] AppState AppState { get; set; } = null!;
    [Inject] private IMessenger Messenger { get; set; } = null!;
    public string ToolKey => ToolState?.Name ?? "";

    public string ToolIconKey => ToolState?.IconKey ?? "";
    public bool IsSelected => AppState.ToolsState.CurrentToolKey == ToolKey;

    public bool ShowProperties => ToolState?.HasToolProperties ?? false;
    public ToolState ToolState
    {
        get => _toolState;
        set
        {
            _toolState = value;
            StateHasChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Register<CurrentToolChangedMessage>(this, msg => StateHasChanged());
    }

    private void OnButtonClicked(RoutedEventArgs args)
    {
        AppState.UiState.ShowToolGroup = false;

        if (IsSelected)
        {
            if (ToolState.HasToolProperties)
                AppState.UiState.ShowToolProperties = !AppState.UiState.ShowToolProperties;
        }
        else
        {
            AppState.UiState.ShowToolProperties = false;
            ToolService.ActivateTool(this._toolState.Name);
        }

        this.StateHasChanged();
    }
}