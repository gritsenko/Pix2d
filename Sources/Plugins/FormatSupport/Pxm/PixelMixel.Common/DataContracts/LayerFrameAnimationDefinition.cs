using System;

namespace PixelMixel.Modules.PixelCanvas.Layers
{
    public class LayerFrameAnimationDefinition : AnimationDefinition
    {
        public Guid LayerId { get; set; }

        public BitmapFrameAnimationKeyDefinition[] Keys { get; set; }
    }
}