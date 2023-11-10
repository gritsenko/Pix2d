using Avalonia.Controls.Shapes;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Pix2d.Common.Common.Behaviors;
using Pix2d.UI.Common;
using Pix2d.UI.Common.Extensions;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.ViewModels.Layers;

namespace Pix2d.UI.Layers;

public class LayersView : ViewBaseSingletonVm<LayersListViewModel>
{
    protected override object Build(LayersListViewModel vm)
    {
        return new Border()
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Child(
                new Grid()
                    .Rows("36,*,62")
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .Children(
                        new LockedFunctionView()
                            .Tooltip("Number of layers is limited by 3. Upgrade to PRO to get unlimited layers.")
                            .IsLocked(vm.ProhibitAddLayers, BindingMode.OneWay, bindingSource: vm)
                            .Content(
                                new Button()
                                    .Command(vm.AddLayerCommand)
                                    .Content("\xE710")
                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                ),
                        new ListBox()
                            .Row(1).Margin(0).Padding(0)
                            .Background(Brushes.Transparent)
                            .BorderThickness(0)
                            .Classes("ItemsDragAndDrop")
                            .ItemsSource(vm.Layers)
                            .SelectedItem(vm.SelectedLayer, bindingMode: BindingMode.TwoWay)
                            .ItemTemplate((LayerViewModel itemVm) =>
                            {
                                if (itemVm == null)
                                    return new TextBlock().Text("No layer");

                                return new LayerItemView(itemVm)
                                    .AddBehavior(new ItemsListContextDragBehavior() { Orientation = Orientation.Vertical });
                            }),
                        new BackgroundSelectorView().Row(2)
                    )
            );
    }
}