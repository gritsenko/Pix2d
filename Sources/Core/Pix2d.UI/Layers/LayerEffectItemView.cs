using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaNodes;

namespace Pix2d.UI.Layers;

public class LayerEffectItemView : LocalizedComponentBase
{
    private ISKNodeEffect? _model;

    protected override object Build() =>
        new Border()
            .BorderThickness(1)
            .BorderBrush(StaticResources.Brushes.InnerPanelBackgroundBrush)
            .Background(StaticResources.Brushes.InnerPanelBackgroundBrush)
            .Margin(new Thickness(0, 0, 0, 5))
            .Child(
                new Grid()
                    .Rows("Auto,*")
                    .Cols("*,Auto,Auto")
                    .Children(
                        new TextBlock()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Padding(new Thickness(5, 0))
                            .Text(() => L(Model?.Name ?? "")()),

                        new Button()
                            .Col(1)
                            .OnClick(_ => OnEffectBake?.Invoke(Model))
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Content("\xE930")
                            .FontSize(14)
                            .ToolTip(L("Bake effect to layer")()),

                        new Button()
                            .Col(2)
                            .OnClick(_ => OnEffectDelete?.Invoke(Model))
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Content("\xE74D")
                            .FontSize(14)
                            .ToolTip(L("Delete effect")()),

                        new ContentControl().Row(1).Col(0).ColSpan(3)
                            .Content(() => Model != null ? EffectsService.GetSettingsView(Model) : null)
                            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                            .Padding(5)
                    ));

    public ISKNodeEffect? Model
    {
        get => _model;
        set
        {
            _model = value;
            StateHasChanged();
        }
    }
    [Inject] private IEffectsService EffectsService { get; set; } = null!;

    public Action<ISKNodeEffect?>? OnEffectDelete { get; set; }
    public Action<ISKNodeEffect?>? OnEffectBake { get; set; }
}