using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaNodes;
using SkiaSharp;
using System.Collections.ObjectModel;
using Pix2d.Operations.Effects;
using Pix2d.Plugins.Sprite.Operations.Effects;
using static Pix2d.Abstract.Services.IEffectsService;

namespace Pix2d.UI.Layers;

public partial class LayerOptionsView(IEffectsService effectsService, IMessenger messenger, IViewPortRefreshService viewPortRefreshService, AppState appState, IDialogService dialogService)
    : ViewBase<LayerOptionsView.State>(new State(effectsService, messenger, viewPortRefreshService, appState, dialogService))
{
    protected override object Build(State state) =>
        new ScrollViewer()
            .MaxHeight(600)
            .Content(
                new Grid()
                    .Rows("Auto, *, Auto, Auto")
                    .Children(
                        new WrapPanel()
                            .Margin(4)
                            .Children(
                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Delete"))
                                    .Command(SpritePlugin.EditCommands.DeleteLayer)
                                    .Content(
                                        new PathIcon().With(IconStyle).Data(Geometry.Parse(
                                            "M 6.496094 1 C 5.675781 1 5 1.675781 5 2.496094 L 5 3 L 2 3 L 2 4 L 3 4 L 3 12.5 C 3 13.324219 3.675781 14 4.5 14 L 10.5 14 C 11.324219 14 12 13.324219 12 12.5 L 12 4 L 13 4 L 13 3 L 10 3 L 10 2.496094 C 10 1.675781 9.324219 1 8.503906 1 Z M 6.496094 2 L 8.503906 2 C 8.785156 2 9 2.214844 9 2.496094 L 9 3 L 6 3 L 6 2.496094 C 6 2.214844 6.214844 2 6.496094 2 Z M 4 4 L 11 4 L 11 12.5 C 11 12.78125 10.78125 13 10.5 13 L 4.5 13 C 4.21875 13 4 12.78125 4 12.5 Z M 5 5 L 5 12 L 6 12 L 6 5 Z M 7 5 L 7 12 L 8 12 L 8 5 Z M 9 5 L 9 12 L 10 12 L 10 5 Z "))
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Clear"))
                                    .Command(SpritePlugin.EditCommands.Clear)
                                    .Content(
                                        new PathIcon().Data(Geometry.Parse(
                                            "M 4.4746094 2 C 3.652078 2 2.9746094 2.6774686 2.9746094 3.5 L 2.9746094 12.5 C 2.9746094 13.322531 3.652078 14 4.4746094 14 L 9 14 L 9 13 L 4.4746094 13 C 4.1931408 13 3.9746094 12.781469 3.9746094 12.5 L 3.9746094 3.5 C 3.9746094 3.2185314 4.1931408 3 4.4746094 3 L 8.9746094 3 L 8.9746094 6 L 11.974609 6 L 11.974609 9 L 12.974609 9 L 12.974609 5.2929688 L 9.6816406 2 L 4.4746094 2 z M 9.9746094 3.7070312 L 11.267578 5 L 9.9746094 5 L 9.9746094 3.7070312 z M 10.728516 10.021484 L 10.021484 10.728516 L 12.292969 13 L 10.021484 15.271484 L 10.728516 15.978516 L 13 13.707031 L 15.271484 15.978516 L 15.978516 15.271484 L 13.707031 13 L 15.978516 10.728516 L 15.271484 10.021484 L 13 12.292969 L 10.728516 10.021484 z"))
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Clone"))
                                    .Command(SpritePlugin.EditCommands.DuplicateLayer)
                                    .Content(
                                        new PathIcon().Data(Geometry.Parse(
                                            "M 2.5 1 C 1.675781 1 1 1.675781 1 2.5 L 1 10.5 C 1 11.324219 1.675781 12 2.5 12 L 4 12 L 4 12.5 C 4 13.324219 4.675781 14 5.5 14 L 13.5 14 C 14.324219 14 15 13.324219 15 12.5 L 15 4.5 C 15 3.675781 14.324219 3 13.5 3 L 12 3 L 12 2.5 C 12 1.675781 11.324219 1 10.5 1 Z M 2.5 2 L 10.5 2 C 10.78125 2 11 2.21875 11 2.5 L 11 10.5 C 11 10.78125 10.78125 11 10.5 11 L 2.5 11 C 2.21875 11 2 10.78125 2 10.5 L 2 2.5 C 2 2.21875 2.21875 2 2.5 2 Z M 12 4 L 13.5 4 C 13.78125 4 14 4.21875 14 4.5 L 14 12.5 C 14 12.78125 13.78125 13 13.5 13 L 5.5 13 C 5.21875 13 5 12.78125 5 12.5 L 5 12 L 10.5 12 C 11.324219 12 12 11.324219 12 10.5 Z "))
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Merge"))
                                    .Command(SpritePlugin.EditCommands.MergeLayer)
                                    .Content(
                                        new PathIcon().Data(Geometry.Parse(
                                            "M 2.5 1 C 1.6774686 1 1 1.6774686 1 2.5 L 1 8.5 C 1 9.3225314 1.6774686 10 2.5 10 L 5 10 L 5 12.5 C 5 13.322531 5.6774686 14 6.5 14 L 12.5 14 C 13.322531 14 14 13.322531 14 12.5 L 14 6.5 C 14 5.6774686 13.322531 5 12.5 5 L 10 5 L 10 2.5 C 10 1.6774686 9.3225314 1 8.5 1 L 2.5 1 z M 2.5 2 L 8.5 2 C 8.7814686 2 9 2.2185314 9 2.5 L 9 6 L 12.5 6 C 12.781469 6 13 6.2185314 13 6.5 L 13 12.5 C 13 12.781469 12.781469 13 12.5 13 L 6.5 13 C 6.2185314 13 6 12.781469 6 12.5 L 6 9 L 2.5 9 C 2.2185314 9 2 8.7814686 2 8.5 L 2 2.5 C 2 2.2185314 2.2185314 2 2.5 2 z M 3.6875 2.9804688 L 2.9804688 3.6875 L 3.3339844 4.0410156 L 4.8789062 5.5859375 L 3.4648438 7 L 7 7 L 7 3.4648438 L 5.5859375 4.8789062 L 4.0410156 3.3339844 L 3.6875 2.9804688 z M 8 8 L 8 8.7070312 L 8 11.535156 L 9.4140625 10.121094 L 11.021484 11.728516 L 11.375 12.082031 L 12.082031 11.375 L 11.728516 11.021484 L 10.121094 9.4140625 L 11.535156 8 L 8.7070312 8 L 8 8 z"))
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Rename"))
                                    .OnClick(() => state.Rename())
                                    .Content(
                                        new TextBlock()
                                            .Text("\xE8AC")
                                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Up"))
                                    .Command(SpritePlugin.EditCommands.BringLayerForward)
                                    .Content(
                                        new TextBlock()
                                            .Text("\xE74A")
                                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    ),

                                new AppButton()
                                    .With(ButtonStyle)
                                    .Label(L("Down"))
                                    .Command(SpritePlugin.EditCommands.SendLayerBackward)
                                    .Content(
                                        new TextBlock()
                                            .Text("\xE74B")
                                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    )
                            ),

                        new StackPanel()
                            .Row(1)
                            .Margin(16)
                            .Children(

                                new SliderEx()
                                    .Label(L("Opacity"))
                                    .Units("%")
                                    .Minimum(0)
                                    .Maximum(100)
                                    .Value(state, x => x.LayerOpacity, BindingMode.TwoWay),

                                new TextBlock().Text(L("Blend mode")),

                                new ComboBox()
                                    .ItemsSource(state.AvailableBlendModes)
                                    .DataTemplates(
                                        new FuncDataTemplate<BlendModeItem>((itemVm, ns) =>
                                            (Control)new TextBlock().Text(L(itemVm.Title)))!
                                    )!
                                    .SelectedItem(state, x => x.BlendMode, BindingMode.TwoWay)
                            ),
                        new Grid()
                            .Cols("*, 32")
                            .Rows("Auto, *")
                            .Margin(16)
                            .Row(2)
                            .Children(
                                new TextBlock()
                                    .Text(L("Effects")),

                                //Add effect button
                                new Button()
                                    .Col(1)
                                    .Content("\xE710")
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .FontSize(16)
                                    .AddFlyoutOnClick(
                                        new MenuFlyout() { Placement = PlacementMode.Bottom }
                                            .ItemsSource(state.AvailableEffects)
                                            .ItemTemplate((IEffectItem item) =>
                                                new MenuItem()
                                                    .Header(item.Title)
                                                    .OnClick(_ => state.AddEffect(item))
                                            )
                                    ),

                                new ItemsControl()
                                    .Row(1)
                                    .ColSpan(2)
                                    .ItemTemplate((ISKNodeEffect item) =>
                                        new LayerEffectItemView(effectsService)
                                        {
                                            Model = item,
                                            OnEffectBake = state.OnEffectBaked,
                                            OnEffectDelete = state.OnEffectDeleted
                                        })
                                    .ItemsSource(state.Effects)

                            )
                    )
            );

    private void IconStyle(PathIcon icon) => icon
        .Width(16)
        .Height(16);

    private void ButtonStyle(AppButton v) => v
        .Width(40)
        .Margin(4);

    private static IReadOnlyList<BlendModeItem> AvailableBlendModes { get; } = [
        new(SKBlendMode.SrcOver, "Normal"),
        new(SKBlendMode.Multiply),
        new(SKBlendMode.Screen),
        new(SKBlendMode.Darken),
        new(SKBlendMode.Lighten),
        new(SKBlendMode.ColorBurn),
        new(SKBlendMode.ColorDodge),
        new(SKBlendMode.Overlay),
        new(SKBlendMode.SoftLight),
        new(SKBlendMode.HardLight),
        new(SKBlendMode.Exclusion),
        new(SKBlendMode.Hue),
        new(SKBlendMode.Color),
        new(SKBlendMode.Luminosity),
    ];


    public class BlendModeItem(SKBlendMode blendMode, string? title = null)
    {
        public SKBlendMode BlendMode { get; } = blendMode;
        public string Title => title ?? BlendMode.ToString();
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IEffectsService _effectsService;
        private readonly IViewPortRefreshService _viewPortRefreshService;
        private readonly AppState _appState;
        private readonly IDialogService _dialogService;
        private bool _isSyncing;

        [ObservableProperty]
        public partial double LayerOpacity { get; set; }

        [ObservableProperty]
        public partial BlendModeItem? BlendMode { get; set; }

        [ObservableProperty]
        public partial ObservableCollection<ISKNodeEffect> Effects { get; set; } = [];

        public IReadOnlyList<BlendModeItem> AvailableBlendModes => LayerOptionsView.AvailableBlendModes;

        public IEnumerable<IEffectItem> AvailableEffects { get; }

        private SpriteEditor? SpriteEditor => _appState.CurrentProject.CurrentNodeEditor as SpriteEditor;
        private Pix2dSprite.Layer? Layer => SpriteEditor?.CurrentSprite?.SelectedLayer;

        public State(IEffectsService effectsService, IMessenger messenger, IViewPortRefreshService viewPortRefreshService,
            AppState appState, IDialogService dialogService)
        {
            _effectsService = effectsService;
            _viewPortRefreshService = viewPortRefreshService;
            _appState = appState;
            _dialogService = dialogService;
            AvailableEffects = _effectsService.GetAvailableEffects();

            ReloadFromLayer();

            messenger.Register<DrawingTargetChangedMessage>(this, _ => ReloadFromLayer());
            messenger.Register<OperationInvokedMessage>(this, msg =>
            {
                if (msg.Operation is AddEffectOperation or RemoveEffectOperation or BakeEffectOperation)
                {
                    ReloadFromLayer();
                }
            });
        }

        partial void OnLayerOpacityChanged(double value)
        {
            if (_isSyncing || Layer == null)
                return;

            if (Math.Abs(value / 100d - Layer.Opacity) < 0.01d)
                return;

            SpriteEditor?.SetOpacity((float)Math.Round(value / 100d, 2));
            OnLayerUpdated();
        }

        partial void OnBlendModeChanged(BlendModeItem? value)
        {
            if (_isSyncing || Layer == null || value == null || Layer.BlendMode == value.BlendMode)
                return;

            Layer.BlendMode = value.BlendMode;
            OnLayerUpdated();
        }

        /// <summary>
        /// Renames the selected layer through an input dialog. Fire-and-forget from the click handler,
        /// matching the artboard rename in <see cref="ObjectActionsBarView"/>; unlike that one this is
        /// undoable, since a layer title is document state the sprite editor's history already covers.
        /// </summary>
        public void Rename() => _ = RenameAsync();

        private async Task RenameAsync()
        {
            if (Layer is not { } layer)
                return;

            var result = await _dialogService.ShowInputDialogAsync(L("Layer name"), L("Rename layer"), layer.Name);
            if (string.IsNullOrWhiteSpace(result))
                return;

            SpriteEditor?.RenameLayer(layer, result);
        }

        public void AddEffect(IEffectItem item)
        {
            if (Layer == null)
                return;

            _effectsService.AddEffect(Layer, item);
            ReloadFromLayer();
        }

        public void OnEffectDeleted(ISKNodeEffect? effect)
        {
            if (Layer == null || effect == null)
                return;

            _effectsService.RemoveEffect(Layer, effect);
            ReloadFromLayer();
        }

        public void OnEffectBaked(ISKNodeEffect? effect)
        {
            if (Layer == null || effect == null)
                return;

            _effectsService.BakeEffect(Layer, effect);
            ReloadFromLayer();
        }

        private void ReloadFromLayer()
        {
            _isSyncing = true;
            LayerOpacity = Math.Round((Layer?.Opacity ?? 0) * 100d);
            BlendMode = AvailableBlendModes.FirstOrDefault(x => x.BlendMode == Layer?.BlendMode) ?? AvailableBlendModes[0];
            _isSyncing = false;

            Effects.Clear();
            if (!(Layer?.HasEffects ?? false))
                return;

            foreach (var effect in Layer.Effects)
            {
                Effects.Add(effect);
            }
        }

        private void OnLayerUpdated()
        {
            _viewPortRefreshService.Refresh();
        }
    }
}