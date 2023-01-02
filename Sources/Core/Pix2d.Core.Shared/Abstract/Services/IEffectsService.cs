using System;
using System.Collections.Generic;
using SkiaNodes;

namespace Pix2d.Abstract.Services
{
    public interface IEffectsService
    {
        Dictionary<string, Type> GetAvailableEffects();
        ISKNodeEffect GetEffect(string name);
    }
}