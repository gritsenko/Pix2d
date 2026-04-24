using Pix2d.UI.Resources;
using CommunityToolkit.Mvvm.ComponentModel;
using SkiaNodes;

namespace Pix2d.UI.Layers;

public partial class LayerEffectItemView(IEffectsService effectsService) : ViewBase<LayerEffectItemView.State>(new State(effectsService))
{
    private ISKNodeEffect? _model;

    protected override object Build(State state) =>
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
                            .Text(state, x => x.ModelName),

                        new Button()
                            .Col(1)
                            .OnClick(_ => OnEffectBake?.Invoke(Model))
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Content("\xE930")
                            .FontSize(14)
                            .ToolTip_Tip(L("Bake effect to layer")),

                        new Button()
                            .Col(2)
                            .OnClick(_ => OnEffectDelete?.Invoke(Model))
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .Content("\xE74D")
                            .FontSize(14)
                            .ToolTip_Tip(L("Delete effect")),

                        new ContentControl().Row(1).Col(0).ColSpan(3)
                            .Content(state, x => x.SettingsView)
                            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                            .Padding(5)
                    ));

    public ISKNodeEffect? Model
    {
        get => _model;
        set
        {
            _model = value;
            ViewModel?.SetModel(value);
        }
    }

    public Action<ISKNodeEffect?>? OnEffectDelete { get; set; }
    public Action<ISKNodeEffect?>? OnEffectBake { get; set; }

    public sealed partial class State : ObservableObject
    {
        private readonly IEffectsService _effectsService;

        [ObservableProperty]
        public partial string ModelName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial object? SettingsView { get; set; }

        public State(IEffectsService effectsService)
        {
            _effectsService = effectsService;
        }

        public void SetModel(ISKNodeEffect? model)
        {
            ModelName = L(model?.Name ?? string.Empty);
            SettingsView = model != null ? _effectsService.GetSettingsView(model) : null;
        }
    }
}