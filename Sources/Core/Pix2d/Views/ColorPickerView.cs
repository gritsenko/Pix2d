using System;
using System.Globalization;
using Avalonia.Input;
using Pix2d.Shared;
using Pix2d.ViewModels.Color;

namespace Pix2d.Views;

public class ColorPickerView : ViewBaseSingletonVm<ColorPickerViewModel>
{
    protected override object Build(ColorPickerViewModel vm) =>
        new Grid().Width(200)
            .Rows("140, Auto, *")
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .Children(
                new ColorPicker()
                    .Margin(10)
                    .Color(@vm.SelectedColor, BindingMode.TwoWay),

                //new Grid().Row(1)
                //    .Cols("Auto, Auto, *")
                //    .Margin(10, 0, 10, 0)
                //    .Children(
                //        new ToggleButton() //Eyedropper
                //            .Width(38)
                //            .Height(38)
                //            .Background(Brushes.Transparent)
                //            .FontSize(20)
                //            .Padding(-1)
                //            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                //            .Content("\xEF3C"),

                //        new ToggleButton().Col(1) //Color editors
                //            .Width(38)
                //            .Height(38)
                //            .FontSize(20)
                //            .FontFamily(StaticResources.Fonts.IconFontSegoe)
                //            .IsChecked(Bind(@vm.EditorMode, BindingMode.TwoWay))
                //            .Background(Brushes.Transparent)
                //            .Padding(0)
                //            .Content("\xE9E9")
                //    ),

                new Border().Row(2)
                    .Margin(8,8)
                    .MinHeight(100)
                    //.IsVisible(@vm.EditorMode)
                    .Child(
                        new TabControl()
                            .SelectedIndex(@vm.ColorTypeIndex)
                            .Items(
                                new TabItem() //PALETTE EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("List")
                                    .Content(
                                        new StackPanel().Row(2)
                                            .IsVisible(@vm.EditorMode, converter: StaticResources.Converters.InverseBooleanConverter)
                                            .Children(
                                                new TextBlock()
                                                    .Text("Recent colors"),

                                                new ColorPalette().Row(1)
                                                    .Colors(vm.RecentColors)
                                                    .SelectColorCommand(vm.SetColorCommand),

                                                new TextBlock()
                                                    .Text("Custom colors"),

                                                new ColorPalette().Row(1)
                                                    .Colors(vm.CustomColors)
                                                    .CanAddColor(true)
                                                    .AddColorCommand(vm.OnAddColorCommand)
                                                    .RemoveColorCommand(vm.OnRemoveColorCommand)
                                                    .ColorToAdd(@vm.SelectedColor)
                                                    .SelectColorCommand(vm.SetColorCommand)
                                            )
                                    ),
                                new TabItem() //HEX EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("Hex")
                                    .Content(
                                        new TextBox()
                                            .VerticalAlignment(VerticalAlignment.Top)
                                            .Text(@vm.HexValue, BindingMode.TwoWay)
                                            .OnKeyDown(args =>
                                            {
                                                if (args.Key == Key.Enter) vm.ApplyHexInput();
                                                if (args.Key == Key.Escape) vm.CancelHexInput();
                                            })
                                            .OnLostFocus(args => vm.ApplyHexInput())
                                    ),
                                new TabItem() // HSV EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("HSV")
                                    .Content(
                                        new StackPanel()
                                            .Children(
                                                new SliderEx()
                                                    .Header("Hue")
                                                    .Minimum(0)
                                                    .Maximum(360)
                                                    .Value(@vm.HsvHPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Header("Saturation")
                                                    .Minimum(0)
                                                    .Maximum(100)
                                                    .Value(@vm.HsvSPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Header("Value")
                                                    .Minimum(0)
                                                    .Maximum(100)
                                                    .Value(@vm.HsvVPart, BindingMode.TwoWay)
                                            )
                                    ),

                                new TabItem() // RGB EDITOR
                                    .Foreground(Brushes.White)
                                    .Header("RGB")
                                    .Content(
                                        new StackPanel()
                                            .Children(
                                                new SliderEx()
                                                    .Header("Red")
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(@vm.RedColorPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Header("Green")
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(@vm.GreenColorPart, BindingMode.TwoWay),
                                                new SliderEx()
                                                    .Header("Blue")
                                                    .Minimum(0)
                                                    .Maximum(255)
                                                    .Value(@vm.BlueColorPart, BindingMode.TwoWay)
                                            )
                                    )
                            )
                    )
            );
}