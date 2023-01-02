using System;
using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;

namespace Pix2d.ViewModels.ToolSettings
{
    public class ShapeToolSettingsViewModel : ToolSettingsBaseViewModel<ShapeTool>
    {
        public ShapeType ShapeType
        {
            get => Tool.ShapeType;
            set => Tool.ShapeType = value;
        }
        public string ShapeTypeKey
        {
            get => ShapeType.ToString();
            set => ShapeType = (ShapeType)System.Enum.Parse(typeof(ShapeType), value);
        }

        // public double LineThickness
        // {
        //     get => Tool.LineThickness;
        //     set => Tool.LineThickness = (float)value;
        // }

        // public double Opacity
        // {
        //     get => Math.Round(Tool.Opacity * 100);
        //     set => Tool.Opacity = (float)value / 100;
        // }


        public override void Activated()
        {
            if (Tool != null)
                Tool.ShapeTypeChanged += Tool_ShapeTypeChanged;
        }

        public override void Deactivated()
        {
            if (Tool != null)
                Tool.ShapeTypeChanged -= Tool_ShapeTypeChanged;
        }

        private void Tool_ShapeTypeChanged(object sender, System.EventArgs e)
        {
            OnPropertyChanged(nameof(ShapeType));
        }

        public ShapeToolSettingsViewModel()
        {
            ShowColorPicker = true;
        }
    }
}