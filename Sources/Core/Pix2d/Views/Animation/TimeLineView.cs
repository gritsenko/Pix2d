using Avalonia.Xaml.Interactions.DragAndDrop;
using Pix2d.Common.Behaviors;
using Pix2d.Plugins.Sprite.ViewModels;
using Pix2d.ViewModels.Animations;
using SkiaSharp;
using Avalonia.Controls.Shapes;

namespace Pix2d.Views.Animation;

public class TimeLineView : ViewBaseSingletonVm<SpriteAnimationTimelineViewModel>
{

    protected override object Build(SpriteAnimationTimelineViewModel vm) =>
        new Grid()
            .Rows("50,*")
            .Cols("52,*,Auto")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(new Control[]
            {


                new StackPanel()
                {
                    Orientation = Orientation.Horizontal,
                    Children = {
                        new Button()
                            .Command(vm.StopCommand)
                            .Content("\xe907")
                            .With(ButtonStyle),
                        new Button()
                            .Command(vm.PrevFrameCommand)
                            .Content("\xe91f")
                            .With(ButtonStyle),
                        new Button()
                            .Command(vm.TogglePlayCommand)
                            .Content("\xE91E")
                            .With(ButtonStyle)
                            .Background(StaticResources.Brushes.AccentBrush),
                        new Button()
                            .Command(vm.NextFrameCommand)
                            .Content("\xe91a")
                            .With(ButtonStyle)
                    }
                },

                new StackPanel().Col(2)
                    .Orientation(Orientation.Horizontal)
                    .Children(new Control [] {
                        new TextBlock()
                            .Text(@vm.FrameInfo)
                            .VerticalAlignment(VerticalAlignment.Center),

                        new Button()
                            .Command(vm.AddFrameCommand)
                            .Content("\xE905")
                            .ToolTip("Add frame")
                            .With(ButtonStyle),
                        new Button()
                            .Command(vm.DuplicateFrameCommand)
                            .Content("\xE90D")
                            .ToolTip("Duplicate frame")
                            .With(ButtonStyle),
                        new Button()
                            .Command(vm.DeleteFrameCommand)
                            .Content("\xE926")
                            .ToolTip("Delete frame")
                            .With(ButtonStyle),

                        new ToggleButton()
                            .With(ToggleButtonStyle)
                            .VerticalContentAlignment(VerticalAlignment.Center)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .IsChecked(@vm.ShowOnionSkin, BindingMode.TwoWay)
                            .ToolTip("Onion skin")
                            .Content(new TextBlock()
                                .Text("\xe91b")
                                .Padding(4)),
                        new TextBlock()
                            .Margin(8,0,0,0)
                            .Text("Fps")
                            .VerticalAlignment(VerticalAlignment.Center),
                        new ComboBox()
                            .Items(vm.FrameRates)
                            .Margin(8,0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .SelectedItem(@vm.SelectedFramerate, BindingMode.TwoWay)
                    }),

                new ListBox().Row(1).ColSpan(3)
                    .ItemsPanel(new StackPanel().Orientation(Orientation.Horizontal))
                    .Items(Bind(vm.Frames))
                    .SelectedItem(@vm.CurrentFrame, BindingMode.TwoWay)
                    .AddBehavior(new ContextDropBehavior { Handler = new FrameItemsListBoxDropHandler() })
                    .ItemTemplate<AnimationFrameViewModel>(itemVm =>
                        new Border()
                            .Background(StaticResources.Brushes.CheckerTilesBrush)
                            .Child(
                                new Rectangle()
                                    .Width(52)
                                    .Height(52)
                                    .Fill(Bind(itemVm, vm => vm.Preview)
                                        .Converter(new FuncValueConverter<SKBitmap, IBrush>(v =>
                                            v != null ? new ImageBrush(v.ToBitmap()) : Brushes.Transparent))
                                    )
                            ).AddBehavior(new ItemsListContextDragBehavior
                                { HorizontalDragThreshold = 3, VerticalDragThreshold = 3 })
                    )//ItemTemplate
            });

    private void ButtonStyle(Button b) => b
        .Classes("TimelineButton")
        .Width(32)
        .Height(32)
        .Padding(0)
        .FontSize(14)
        .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily);

    private void ToggleButtonStyle(ToggleButton b) => b
        .Classes("TimelineButton")
        .Width(32)
        .Height(32)
        .FontSize(14)
        .Padding(0)
        .FontFamily(StaticResources.Fonts.Pix2dThemeFontFamily);

}