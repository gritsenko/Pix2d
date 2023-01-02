using Pix2d.Drawing.Tools;
using Pix2d.Primitives.Drawing;
using Pix2d.Tools;

namespace Pix2d.ViewModels.ToolSettings
{
    public class VectorShapeToolSettingsViewModel : ToolSettingsBaseViewModel<VectorShapeTool>
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
    }
}