using System;
using System.Windows.Input;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;

namespace Pix2d.ViewModels.ToolSettings;

public class SelectionToolSettingsViewModel : ToolSettingsBaseViewModel<PixelSelectTool>
{
    public PixelSelectionMode SelectionMode
    {
        get => Tool.SelectionMode;
        set => Tool.SelectionMode = value;
    }

    public int SelectedIndex { get; set; }

    public ICommand SelectModeCommand => GetCommand<PixelSelectionMode>((mode) =>
    {
        SelectionMode = mode;
    });
}