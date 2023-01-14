using System;
using System.Collections.Generic;
using System.ComponentModel;
using Mvvm;
using Pix2d.CommonNodes;
using SkiaSharp;

namespace Pix2d.ViewModels.Animations
{
    [Bindable(true)]
    public class AnimationFrameViewModel : ObservableObject
    {
        private SKBitmap _preview;

        public List<LayerFrameMeta> Layers
        {
            get => Get<List<LayerFrameMeta>>();
            set => Set(value);
        }

        public Func<AnimationFrameViewModel, SKBitmap> PreviewProvider { get; set; }
        public Action<AnimationFrameViewModel> UpdatePropertiesAction { get; set; }

        public SKBitmap Preview
        {
            get
            {
                _preview?.Dispose();
                _preview = PreviewProvider?.Invoke(this);
                return _preview;
            }
        }

        public bool IsSelected
        {
            get => Get<bool>();
            set => Set(value);
        }



        public void UpdatePreview()
        {
            OnPropertyChanged(nameof(Preview));
        }
        public void UpdateProperties()
        {
            UpdatePropertiesAction?.Invoke(this);
            OnPropertyChanged(nameof(Layers));
        }
    }
}