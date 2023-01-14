using System;

namespace Pix2d.Primitives.SpriteEditor
{
    public class SpriteFrameChangedEvenArgs : EventArgs
    {
        public SpriteFrameChangedEvenArgs(bool isAnimationPlaying)
        {
            IsAnimationPlaying = isAnimationPlaying;
        }

        public readonly bool IsAnimationPlaying;
    }
}