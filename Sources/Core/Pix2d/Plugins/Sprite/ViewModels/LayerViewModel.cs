using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Mvvm;
using Pix2d.CommonNodes;
using Pix2d.Mvvm;
using Pix2d.Plugins.Sprite.Editors;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.ViewModels.Layers
{
    public class LayerViewModel : Pix2dViewModelBase
    {
        private readonly SpriteEditor _editor;
        private float _oldOpacity;
        public string LayerType { get; set; }
        public Pix2dSprite.Layer SourceNode { get; set; }

        public SKBitmapObservable Preview { get; set; } = new SKBitmapObservable();

        public bool ShowEffectSettings
        {
            get => Get<bool>();
            set => Set(value);
        }

        public string Name { get; set; }

        public float Opacity
        {
            get => (float)Math.Round(SourceNode.Opacity * 100);
            set
            {
                SourceNode.Opacity = (float)Math.Round(value / 100, 2);
                UpdatePreview();
                RefreshViewPort();
                OnPropertyChanged();
            }
        }

        public static IReadOnlyList<BlendModeItemViewModel> AvailableBlendModes { get; set; } = new List<BlendModeItemViewModel>()
        {
            new(SKBlendMode.SrcOver, "Normal"),
            new(SKBlendMode.Multiply),
            new(SKBlendMode.Screen),
            new(SKBlendMode.Darken),
            new(SKBlendMode.Lighten),
            new(SKBlendMode.ColorBurn),
            new(SKBlendMode.ColorDodge),
            new(SKBlendMode.Overlay),
            new(SKBlendMode.SoftLight),
            new(SKBlendMode.HardLight),
            new(SKBlendMode.Exclusion),
            new(SKBlendMode.Hue),
            new(SKBlendMode.Color),
            new(SKBlendMode.Luminosity),
        };

        public BlendModeItemViewModel BlendMode
        {
            get => AvailableBlendModes.FirstOrDefault(x => x.BlendMode == SourceNode.BlendMode) ?? AvailableBlendModes[0];
            set
            {
                var oldValue = AvailableBlendModes.FirstOrDefault(x => x.BlendMode == SourceNode.BlendMode);
                if (value == null || oldValue == value) return;

                SourceNode.BlendMode = value.BlendMode;
                UpdatePreview();
                RefreshViewPort();
                OnPropertyChanged();
            }
        }

        [NotifiesOn(nameof(BlendMode))]
        public string BlendModeStr => BlendMode.Title;

        [NotifiesOn(nameof(BlendMode))]
        public bool ShowBlendModeName => BlendMode.Title != "Normal";

        public bool ColorLocked
        {
            get
            {
                if (SourceNode is Pix2dSprite.Layer layer)
                    return layer.LockTransparentPixels;

                return Get<bool>(false);
            }
            set
            {
                if (SourceNode is Pix2dSprite.Layer layer)
                {
                    layer.LockTransparentPixels = value;
                    RefreshViewPort();
                    OnPropertyChanged();
                }
            }
        }

        public bool IsVisible
        {
            get => SourceNode.IsVisible;
            set
            {
                SourceNode.IsVisible = value;
                _editor.NotifyLayerChanged(this.SourceNode as Pix2dSprite.Layer);
                RefreshViewPort();
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => Get<bool>();
            set => Set(value);
        }

        public string LayerName
        {
            get => Get<string>();
            set => Set(value);
        }

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

                return Get<bool>(false);
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


        [NotifiesOn(nameof(IsSelected))]
        [NotifiesOn(nameof(ColorLocked))]
        public bool ShowColorLockButton => IsSelected || ColorLocked;


        public ICommand CloseCommand { get; set; }

        public ICommand DeleteLayerCommand { get; set; }
        public ICommand ClearLayerCommand { get; set; }
        public ICommand MergeLayerCommand { get; set; }
        public ICommand DuplicateLayerCommand { get; set; }

        public ICommand ToggleVisibilityCommand => GetCommand(() => { this.IsVisible = !this.IsVisible; });
        public ICommand ToggleColorLockedCommand => GetCommand(() => { this.ColorLocked = !this.ColorLocked; });
        public ICommand ToggleEffectsCommand => GetCommand(() => { this.ShowEffects = !this.ShowEffects; });

        public ICommand SelectLayerCommand { get; set; }


        public IRelayCommand DeleteEffectCommand => GetCommand<EffectViewModel>(DeleteEffectCommandExecute);
        public IRelayCommand BakeEffectCommand => GetCommand<EffectViewModel>(BakeEffectCommandExecute);


        public LayerViewModel()
        {
        }

        public LayerViewModel(Pix2dSprite.Layer node, SpriteEditor editor) : this()
        {
            _editor = editor;
            SourceNode = node;
            LayerType = node.GetType().Name;
            UpdatePreview();
            UpdateEffectsFromNode();
            Name = $"Layer {node.Index + 1}/{node.Parent.Nodes.Count}";

            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                Name = node.Name;
            }
        }

        public LayerViewModel(LayerViewModel toCopy, string newName) : this(toCopy.SourceNode.Clone(), toCopy._editor)
        {
            Name = newName;
        }

        public void UpdatePreview()
        {
            if (!(SourceNode is Pix2dSprite.Layer src))
                return;

            if (Preview.Bitmap == null)
            {
                Preview.SetBitmap(new SKBitmap(100, 100, SKColorType.Bgra8888, SKAlphaType.Premul));
            }

            src.RenderPreview(src.CurrentFrameIndex, Preview.Bitmap, 1);
            Preview.RaiseBitmapChanged();
        }

        public override string ToString()
        {
            return LayerType;
        }

        public void UpdateOpacity()
        {
            _oldOpacity = SourceNode.Opacity;
            OnPropertyChanged(nameof(Opacity));
        }

        public void AddEffect(EffectViewModel vm)
        {
            CoreServices.EditService.AddEffect(vm.Effect);
        }

        private void DeleteEffectCommandExecute(EffectViewModel effect)
        {
            CoreServices.EditService.RemoveEffect(effect.Effect);
        }
        private void BakeEffectCommandExecute(EffectViewModel effectVm)
        {
            CoreServices.EditService.BakeEffect(effectVm.Effect);
        }


        public void UpdateEffectsFromNode()
        {
            Effects.Clear();
            if (SourceNode.Effects == null)
            {
                OnPropertyChanged(nameof(HasEffects));
                OnPropertyChanged(nameof(ShowEffects));
                return;
            }

            foreach (var effect in SourceNode.Effects)
            {
                Effects.Add(new EffectViewModel(effect)
                {
                    DeleteEffectCommand = this.DeleteEffectCommand,
                    BakeEffectCommand = this.BakeEffectCommand
                });
            }

            OnPropertyChanged(nameof(HasEffects));
            OnPropertyChanged(nameof(ShowEffects));
        }
    }
}