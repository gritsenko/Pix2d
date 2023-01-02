using System;
using SkiaSharp;
using Spine;

namespace Pix2d.Plugins.SpinePlugin;

public class SpineAtlasTextureLoader : TextureLoader
{
    public void Load(AtlasPage page, string path)
    {
        var bm = SKBitmap.Decode(path);
        page.rendererObject = SKImage.FromBitmap(bm);
        page.width = bm.Width;
        page.height = bm.Height;
    }

    public void Unload(object texture)
    {
        throw new NotImplementedException();
    }
}