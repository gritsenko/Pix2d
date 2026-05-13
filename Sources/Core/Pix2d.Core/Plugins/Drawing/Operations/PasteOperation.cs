using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Tools;
using Pix2d.Operations;
using Pix2d.Plugins.Drawing.Tools.PixelSelect;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Operations;

public class PasteOperation : EditOperationBase, ISpriteEditorOperation, IToolAwareOperation
{
    private readonly SKBitmap _image;
    private readonly SKPoint _position;
    private readonly byte[] _initialTargetData;
    private readonly IDrawingTarget _drawingTarget;
    private readonly IDrawingLayer _drawingLayer;
    private readonly IDrawingService _drawingService;
    private readonly IToolService _toolService;

    public HashSet<int> AffectedFrameIndexes { get; } = new();
    public HashSet<int> AffectedLayerIndexes { get; } = new();

    public string? ToolKeyBeforeOperation { get; }

    // Paste places a selection on the canvas — once Stage 2 lands this becomes PixelTransformTool.
    // For now we just want Undo to restore the pre-paste tool; we don't override the active tool on Redo.
    public string? ToolKeyAfterOperation => null;

    public PasteOperation(SKBitmap image, SKPoint position,
        IDrawingTarget drawingTarget,
        IDrawingLayer drawingLayer,
        IDrawingService drawingService,
        IToolService toolService,
        string? activeToolKey = null)
    {
        _image = image;
        _position = position;
        _drawingTarget = drawingTarget;
        _drawingLayer = drawingLayer;
        _drawingService = drawingService;
        _toolService = toolService;
        _initialTargetData = _drawingTarget.GetData();
        ToolKeyBeforeOperation = activeToolKey;

        // Track affected frames/layers for timeline preview refresh
        if (_drawingTarget is IAnimatedNode animatedNode)
        {
            AffectedFrameIndexes.Add(animatedNode.CurrentFrameIndex);
            AffectedLayerIndexes.Add(animatedNode.SelectedLayerIndex);
        }
    }

    public override void OnPerform()
    {
        // Paste always lifts pixels (the pasted bitmap is shown above the canvas, ready to be positioned),
        // so we hand off to PixelTransformTool — the single owner of the "pixels lifted" state. Note: order
        // matters — set up the selection FIRST so the transform tool's Activate sees HasSelection=true and
        // enters the editor in transform mode rather than falling back to PixelSelectRectTool.
        _drawingLayer?.ApplySelection();
        _drawingLayer?.SetSelectionFromExternal(_image, _position);
        _toolService.ActivateTool<PixelTransformTool>();
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