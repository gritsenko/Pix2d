using System;
using Pix2d.Abstract.Tools;
using Pix2d.Mvvm;

namespace Pix2d.ViewModels.ToolSettings
{
    public class ToolSettingsBaseViewModel<TTool> : ToolSettingsBaseViewModel
        where TTool : ITool
    {
        public new TTool Tool
        {
            get => (TTool) (base.Tool ??= ToolProviderFunc?.Invoke());
            set => base.Tool = value;
        }
    }

    public class ToolSettingsBaseViewModel : Pix2dViewModelBase
    {
        public ITool Tool { get; set; }
        public bool CompactMode
        {
            get => Get<bool>();
            set => Set(value);
        }

        public bool ShowColorPicker
        {
            get => Get<bool>();
            set => Set(value);
        }
        
        public bool ShowBrushSettings
        {
            get => Get<bool>();
            set => Set(value);
        }

        public Func<ITool> ToolProviderFunc { get; set; }

        public virtual void Activated()
        {

        }
        public virtual void Deactivated()
        {

        }

    }
}
