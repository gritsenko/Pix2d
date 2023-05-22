using Avalonia.Xaml.Interactions.DragAndDrop;
using Pix2d.Common.Behaviors;
using Pix2d.ViewModels.Layers;

namespace Pix2d.Views.Layers;

public class LayersView : ViewBaseSingletonVm<LayersListViewModel>
{
    protected override object Build(LayersListViewModel vm) =>
        new Border()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Child(
                new Grid()
                    .Rows("36,*,62")
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .Children(

                        new Button()
                            .Background(StaticResources.Brushes.SelectedItemBrush)
                            .BorderBrush(StaticResources.Brushes.SelectedItemBrush)
                            .Command(vm.AddLayerCommand)
                            .Content("\xE710")
                            .FontFamily(StaticResources.Fonts.IconFontSegoe),

                        new ListBox()
                            .Row(1).Margin(0).Padding(0)
                            .Background(Brushes.Transparent)
                            .BorderThickness(0)
                            .Classes("ItemsDragAndDrop")
                            .ItemsSource(@vm.Layers)
                            .SelectedItem(@vm.SelectedLayer, bindingMode: BindingMode.OneWay)
                            .AddBehavior(new ContextDropBehavior { Handler = new ItemsListBoxDropHandler() })
                            .ItemTemplate((LayerViewModel itemVm) => new LayerItemView(itemVm)
                                    .AddBehavior(new ItemsListContextDragBehavior { HorizontalDragThreshold = 3, VerticalDragThreshold = 3 })

                            ),

                        new BackgroundSelectorView().Row(2)
                    )
            );
}