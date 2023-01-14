using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Abstract
{
    public interface IObjectCreationService
    {
        void CreateArtboard(SKRect destRect);
        void CreateText(SKRect destRect);
        void CreateRectangle(SKRect destRect);
        
        //todo: think create sprite
        SKNode CreateSprite(SKBitmap img, SKNode groupIntoNode = null);
    }
}