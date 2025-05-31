using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class PasteOperation : EditOperationBase
{
    private readonly SKBitmap _image;
    private readonly SKPoint _position;
    private readonly byte[] _initialTargetData;
    private readonly IDrawingTarget _drawingTarget;
    private readonly IDrawingLayer _drawingLayer;
    private readonly IDrawingService _drawingService;
    private readonly IToolService _toolService;

    public PasteOperation(SKBitmap image, SKPoint position,
        IDrawingTarget drawingTarget,
        IDrawingLayer drawingLayer,
        IDrawingService drawingService,
        IToolService toolService)
    {
        _image = image;
        _position = position;
        _drawingTarget = drawingTarget;
        _drawingLayer = drawingLayer;
        _drawingService = drawingService;
        _toolService = toolService;
        _initialTargetData = _drawingTarget.GetData();
    }

    public override void OnPerform()
    {
        _toolService.ActivateTool<PixelSelectToolBase>();
        _drawingLayer?.ApplySelection();
        _drawingLayer?.SetSelectionFromExternal(_image, _position);

        //        CoreServices.DrawingService.PasteBitmap(_image, _position);
    }

    public override void OnPerformUndo()
    {
        _drawingService.CancelCurrentOperation();
        _drawingTarget.SetData(_initialTargetData);
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return [];
    }
}