using System;
using Pix2d.CommonNodes;

namespace Pix2d.Modules.Sprite.Editors
{
    public class LayerChangedEventArgs : EventArgs
    {
        public Pix2dSprite.Layer Layer { get; }

        public LayerChangedEventArgs(Pix2dSprite.Layer layer)
        {
            Layer = layer;
        }
    }
}