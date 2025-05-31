using System;
using Pix2d.Services;
using SkiaSharp;

namespace Pix2d.Tools;

public class ArtboardTool(
    IObjectCreationService objectCreationService,
    ISceneService sceneService,
    IEditService editService) : ObjectCreationTool(objectCreationService, sceneService, editService)
{
    protected override void CreateObjectCore(SKRect destRect)
    {
        if (destRect.Width < 1 || Math.Abs(destRect.Height) < 1)
            destRect = new SKRect(destRect.Left, destRect.Top, destRect.Left + 64, destRect.Top + 64);
        objectCreationService.CreateArtboard(destRect);
    }
}