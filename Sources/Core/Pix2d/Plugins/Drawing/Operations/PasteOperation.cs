using System.Collections.Generic;
using System.Linq;
using Pix2d.Abstract.Drawing;
using Pix2d.Operations;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class PasteOperation : EditOperationBase
{
    private readonly SKBitmap _image;
    private readonly SKPoint _position;
    private readonly byte[] _initialTargetData;
    private readonly IDrawingTarget _initialTarget;

    public PasteOperation(SKBitmap image, SKPoint position)
    {
        _image = image;
        _position = position;
        _initialTarget = CoreServices.DrawingService.DrawingLayer.DrawingTarget;
        _initialTargetData = _initialTarget.GetData();
    }
    public override void OnPerform()
    {
        CoreServices.DrawingService.PasteBitmap(_image, _position);
    }

    public override void OnPerformUndo()
    {
        CoreServices.DrawingService.CancelCurrentOperation();
        _initialTarget.SetData(_initialTargetData);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return Enumerable.Empty<SKNode>();
    }
}