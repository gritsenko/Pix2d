using System;

namespace Pix2d.Primitives.SpriteEditor;

public class SpriteFrameChangedEvenArgs(bool isAnimationPlaying) : EventArgs
{
    public readonly bool IsAnimationPlaying = isAnimationPlaying;
}