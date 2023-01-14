using System;
using System.Windows.Input;
using Mvvm;
using SkiaNodes;

namespace Pix2d.ViewModels.Layers
{
    public class EffectViewModel : ViewModelBase
    {
        public ISKNodeEffect Effect { get; }

        public ICommand DeleteEffectCommand { get; set; }
        public ICommand BakeEffectCommand { get; set; }

        public string Name => Effect.Name;

        public string Key => Effect.GetType().Name;

        public EffectViewModel(ISKNodeEffect effect)
        {
            Effect = effect;

            Effect.SettingsChanged += Effect_SettingsChanged;
        }

        private void Effect_SettingsChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(Effect));

            CoreServices.ViewPortService.Refresh();
        }

    }
}