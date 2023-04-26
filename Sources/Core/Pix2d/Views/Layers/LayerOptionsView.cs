using Pix2d.Effects;
using Pix2d.Plugins.Sprite;
using Pix2d.Shared;
using Pix2d.ViewModels.Layers;

namespace Pix2d.Views.Layers;

public class LayerOptionsView : ViewBaseSingletonVm<LayersListViewModel>
{
    protected override object Build(LayersListViewModel vm) =>
        new Grid()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Rows("Auto, *, Auto, Auto")
            .Children(
                new WrapPanel()
                    .Margin(8)
                    .Children(
                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Delete")
                            .Command(vm.DeleteLayerCommand)
                            .Content(
                                new PathIcon().With(IconStyle).Data(Geometry.Parse(
                                    "M 6.496094 1 C 5.675781 1 5 1.675781 5 2.496094 L 5 3 L 2 3 L 2 4 L 3 4 L 3 12.5 C 3 13.324219 3.675781 14 4.5 14 L 10.5 14 C 11.324219 14 12 13.324219 12 12.5 L 12 4 L 13 4 L 13 3 L 10 3 L 10 2.496094 C 10 1.675781 9.324219 1 8.503906 1 Z M 6.496094 2 L 8.503906 2 C 8.785156 2 9 2.214844 9 2.496094 L 9 3 L 6 3 L 6 2.496094 C 6 2.214844 6.214844 2 6.496094 2 Z M 4 4 L 11 4 L 11 12.5 C 11 12.78125 10.78125 13 10.5 13 L 4.5 13 C 4.21875 13 4 12.78125 4 12.5 Z M 5 5 L 5 12 L 6 12 L 6 5 Z M 7 5 L 7 12 L 8 12 L 8 5 Z M 9 5 L 9 12 L 10 12 L 10 5 Z "))
                            ),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Clear")
                            .Command(vm.ClearLayerCommand)
                            .Content(
                                new PathIcon().Data(Geometry.Parse(
                                    "M 4.4746094 2 C 3.652078 2 2.9746094 2.6774686 2.9746094 3.5 L 2.9746094 12.5 C 2.9746094 13.322531 3.652078 14 4.4746094 14 L 9 14 L 9 13 L 4.4746094 13 C 4.1931408 13 3.9746094 12.781469 3.9746094 12.5 L 3.9746094 3.5 C 3.9746094 3.2185314 4.1931408 3 4.4746094 3 L 8.9746094 3 L 8.9746094 6 L 11.974609 6 L 11.974609 9 L 12.974609 9 L 12.974609 5.2929688 L 9.6816406 2 L 4.4746094 2 z M 9.9746094 3.7070312 L 11.267578 5 L 9.9746094 5 L 9.9746094 3.7070312 z M 10.728516 10.021484 L 10.021484 10.728516 L 12.292969 13 L 10.021484 15.271484 L 10.728516 15.978516 L 13 13.707031 L 15.271484 15.978516 L 15.978516 15.271484 L 13.707031 13 L 15.978516 10.728516 L 15.271484 10.021484 L 13 12.292969 L 10.728516 10.021484 z"))
                            ),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Duplicate")
                            .Command(vm.DuplicateLayerCommand)
                            .Content(
                                new PathIcon().Data(Geometry.Parse(
                                    "M 2.5 1 C 1.675781 1 1 1.675781 1 2.5 L 1 10.5 C 1 11.324219 1.675781 12 2.5 12 L 4 12 L 4 12.5 C 4 13.324219 4.675781 14 5.5 14 L 13.5 14 C 14.324219 14 15 13.324219 15 12.5 L 15 4.5 C 15 3.675781 14.324219 3 13.5 3 L 12 3 L 12 2.5 C 12 1.675781 11.324219 1 10.5 1 Z M 2.5 2 L 10.5 2 C 10.78125 2 11 2.21875 11 2.5 L 11 10.5 C 11 10.78125 10.78125 11 10.5 11 L 2.5 11 C 2.21875 11 2 10.78125 2 10.5 L 2 2.5 C 2 2.21875 2.21875 2 2.5 2 Z M 12 4 L 13.5 4 C 13.78125 4 14 4.21875 14 4.5 L 14 12.5 C 14 12.78125 13.78125 13 13.5 13 L 5.5 13 C 5.21875 13 5 12.78125 5 12.5 L 5 12 L 10.5 12 C 11.324219 12 12 11.324219 12 10.5 Z "))
                            ),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Merge")
                            .Command(vm.MergeLayerCommand)
                            .Content(
                                new PathIcon().Data(Geometry.Parse(
                                    "M 2.5 1 C 1.6774686 1 1 1.6774686 1 2.5 L 1 8.5 C 1 9.3225314 1.6774686 10 2.5 10 L 5 10 L 5 12.5 C 5 13.322531 5.6774686 14 6.5 14 L 12.5 14 C 13.322531 14 14 13.322531 14 12.5 L 14 6.5 C 14 5.6774686 13.322531 5 12.5 5 L 10 5 L 10 2.5 C 10 1.6774686 9.3225314 1 8.5 1 L 2.5 1 z M 2.5 2 L 8.5 2 C 8.7814686 2 9 2.2185314 9 2.5 L 9 6 L 12.5 6 C 12.781469 6 13 6.2185314 13 6.5 L 13 12.5 C 13 12.781469 12.781469 13 12.5 13 L 6.5 13 C 6.2185314 13 6 12.781469 6 12.5 L 6 9 L 2.5 9 C 2.2185314 9 2 8.7814686 2 8.5 L 2 2.5 C 2 2.2185314 2.2185314 2 2.5 2 z M 3.6875 2.9804688 L 2.9804688 3.6875 L 3.3339844 4.0410156 L 4.8789062 5.5859375 L 3.4648438 7 L 7 7 L 7 3.4648438 L 5.5859375 4.8789062 L 4.0410156 3.3339844 L 3.6875 2.9804688 z M 8 8 L 8 8.7070312 L 8 11.535156 L 9.4140625 10.121094 L 11.021484 11.728516 L 11.375 12.082031 L 12.082031 11.375 L 11.728516 11.021484 L 10.121094 9.4140625 L 11.535156 8 L 8.7070312 8 L 8 8 z"))
                            ),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Up")
                            .Command(SpritePlugin.EditCommands.BringLayerForward)
                            .Content(
                                new TextBlock()
                                    .Text("\xE74A")
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            ),

                        new AppButton()
                            .With(ButtonStyle)
                            .Label("Down")
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
                            .Header("Opacity")
                            .Units("%")
                            .Minimum(0)
                            .Maximum(100)
                            .DataContext(@vm.SelectedLayer, out var sl)
                            .Value(@sl?.Opacity ?? 0, BindingMode.TwoWay),

                        new TextBlock().Text("Blend mode"),

                        new ComboBox()
                            .ItemsSource(LayerViewModel.AvailableBlendModes)
                            .DataTemplates(
                                new FuncDataTemplate<BlendModeItemViewModel>((itemVm,ns)=>new TextBlock().Text(itemVm.Title)))
                            .DataContext(@vm.SelectedLayer)
                            .SelectedItem(@sl?.BlendMode)
                    ),
                new Grid()
                    .Cols("*, 32")
                    .Rows("Auto, *")
                    .Margin(16)
                    .Row(2)
                    .Children(
                        new TextBlock()
                            .Text("Effects"),

                        //Add effect button
                        new Button()
                            .Col(1)
                            .Content("\xE710")
                            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                            .With(b =>
                            {
                                var flyout = new MenuFlyout() {Placement = PlacementMode.Bottom};
                                foreach (var effect in vm.AvailableEffects)
                                {

                                    flyout.AddItem(effect.Name, vm.AddEffectToLayerCommand, effect);
                                }

                                b.Click += (s, e) => flyout.ShowAt(b);
                            }),

                        new ItemsControl()
                            .Row(1)
                            .ColSpan(2)
                            .DataContext(@vm.SelectedLayer, out var sl1)
                            .ItemTemplate(EffectItemTemplate)
                            .Items(@sl1?.Effects)

                    )
            );

    private FuncDataTemplate<EffectViewModel> EffectItemTemplate => new((vm, ns) =>
        new Grid()
            .Rows("Auto,*")
            .Cols("*,Auto,Auto")
            .Children(
                new TextBlock()
                    .Text(@vm.Name),

                new Button()
                    .Col(1)
                    .Command(vm.BakeEffectCommand)
                    .CommandParameter(vm)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .Content("\xE930")
                    .ToolTip("Bake effect to layer"),

                new Button()
                    .Col(2)
                    .Command(vm.DeleteEffectCommand)
                    .CommandParameter(vm)
                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                    .Content("\xE74D")
                    .ToolTip("Delete effect"),

                new ContentControl().Row(1).Col(0).ColSpan(3)
                    .Content(@vm)
                    .ContentTemplate(EffectTemplates.GetTemplateByEffect(vm))
                )
    );

    private void IconStyle(PathIcon icon) => icon
        .Width(16)
        .Height(16);

    private void ButtonStyle(ViewBase v) => v
        .Width(50);
}

public static class EffectTemplates
{
    public static FuncDataTemplate<EffectViewModel> ColorOverlayEffectTemplate => new((vm, ns) =>
        new StackPanel().Children(
            new TextBlock().Text("Color"),
            new ColorPickerButton(),
            new TextBlock().Text("Opacity"),
            new SliderEx()
                .Minimum(0)
                .Maximum(255)
                .DataContext((ColorOverlayEffect)vm.Effect, out var effect)
                .Value(@effect.Opacity)
            )
    );

    public static FuncDataTemplate<EffectViewModel> ShadowEffectTemplate => new((vm, ns) =>
        new StackPanel().Children(
            new TextBlock().Text("Shadow")
            )
    );

    public static FuncDataTemplate<EffectViewModel> BlurEffectTemplate => new((vm, ns) =>
        new StackPanel().Children(
            new TextBlock().Text("Blur")
            )
    );

    public static FuncDataTemplate<EffectViewModel> GrayscaleEffectTemplate => new((vm, ns) =>
        new StackPanel().Children(
            new TextBlock().Text("Grayscale")
            )
    );
    public static FuncDataTemplate<EffectViewModel> ImageAdjustSettingsEffectTemplate => new((vm, ns) =>
        new StackPanel().Children(
            new TextBlock().Text("ImageAdjustSettings")
            )
    );

    public static IDataTemplate GetTemplateByEffect(EffectViewModel vm)
    {
        return vm.Effect switch
        {
            ColorOverlayEffect => ColorOverlayEffectTemplate,
            PixelShadowEffect => ShadowEffectTemplate,
            PixelBlurEffect => BlurEffectTemplate,
            GrayscaleEffect => GrayscaleEffectTemplate,
            ImageAdjustEffect => ImageAdjustSettingsEffectTemplate,
            _ => default
        };
    }
}