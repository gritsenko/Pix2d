using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Mvvm;
using Pix2d.CommonNodes;
using Pix2d.Mvvm;
using Pix2d.ViewModels.Layers;
using SkiaNodes;

namespace Pix2d.ViewModels.NodeProperties
{
    public class NodeEffectsViewModel : Pix2dViewModelBase
    {
        public IEffectsService EffectsService { get; }

        public NodeEffectsViewModel(IEffectsService effectsService)
        {
            EffectsService = effectsService;
        }

        public SKNode SourceNode { get; set; }
        public List<EffectViewModel> AvailableEffects { get; set; } = new List<EffectViewModel>();

        public ObservableCollection<EffectViewModel> Effects { get; set; } = new ObservableCollection<EffectViewModel>();

        public EffectViewModel SelectedEffect
        {
            get => Get<EffectViewModel>();
            set => Set(value);
        }

        public bool HasEffects => (SourceNode as Pix2dSprite.Layer)?.HasEffects ?? false;

        public bool ShowEffects
        {
            get
            {
                if (SourceNode is Pix2dSprite.Layer layer)
                    return layer.ShowEffects;

                return Get(false);
            }
            set
            {
                if (SourceNode is Pix2dSprite.Layer layer)
                {
                    layer.ShowEffects = value;
                    RefreshViewPort();
                    OnPropertyChanged();
                }
            }
        }
        public ICommand ToggleEffectsCommand => GetCommand(() => { this.ShowEffects = !this.ShowEffects; });
        public IRelayCommand DeleteEffectCommand => GetCommand<EffectViewModel>(DeleteEffectCommandExecute);

        protected override void OnLoad()
        {
            if (SourceNode != null)
                UpdateEffectsFromNode();

            AvailableEffects.AddRange(EffectsService.GetAvailableEffects().Select(x => new EffectViewModel(Activator.CreateInstance(x.Value) as ISKNodeEffect)));
        }

        public void AddEffect(EffectViewModel vm)
        {
            CoreServices.EditService.AddEffect(vm.Effect);
            UpdateEffectsFromNode();
        }

        private void DeleteEffectCommandExecute(EffectViewModel effect)
        {
            CoreServices.EditService.RemoveEffect(effect.Effect);
            UpdateEffectsFromNode();
        }

        private void UpdateEffectsFromNode()
        {
            Effects.Clear();
            if (SourceNode.Effects == null)
                return;

            foreach (var effect in SourceNode.Effects)
            {
                Effects.Add(new EffectViewModel(effect) { DeleteEffectCommand = this.DeleteEffectCommand });
            }

            OnPropertyChanged(nameof(HasEffects));
            OnPropertyChanged(nameof(ShowEffects));
        }


    }
}
