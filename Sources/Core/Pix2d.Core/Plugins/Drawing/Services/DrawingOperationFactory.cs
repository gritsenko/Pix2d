using System.Runtime.InteropServices;
using Pix2d.Abstract.Drawing;
using Pix2d.Operations.Drawing;

namespace Pix2d.Plugins.Drawing.Services;

internal class DrawingOperationFactory(IDrawingLayer drawingLayer, IOperationService operationService)
{
    private readonly IOperationService _operationService = operationService;
    private byte[]? _initialData;
    private IDrawingTarget? _currentOperationDrawingTarget;
    public bool IsOperationStarted { get; private set; }


    public void StartNewDrawingOperation(IDrawingTarget drawingTarget)
    {
        IsOperationStarted = true;
        _currentOperationDrawingTarget = drawingTarget;
        _initialData = drawingTarget.GetData();
    }

    public void FinishCurrentDrawingOperation()
    {
        if (_currentOperationDrawingTarget == null) return;

        try
        {
            var finalData = _currentOperationDrawingTarget.GetData();

            // Handle null or empty data
            if (_initialData == null || finalData == null || _initialData.Length == 0 || finalData.Length == 0)
            {
                var operation = new DrawingOperationWithFullState(_currentOperationDrawingTarget);
                operation.SetInitialData(_initialData);
                operation.SetFinalData(finalData);
                if (operation.HasChanges())
                    _operationService.PushOperations(operation);
                return;
            }

            var changes = GetDifferences(_initialData, finalData);

            if (changes.Count > 0)
            {
                var operation = new DrawingOperationWithDiffState(_currentOperationDrawingTarget, changes);
                operation.SetFinalData();

                if (operation.HasChanges())
                    _operationService.PushOperations(operation);
            }

        }
        finally
        {
            _initialData = null;
            _currentOperationDrawingTarget = null;
            IsOperationStarted = false;
        }
    }

    private List<DrawingOperationWithDiffState.DiffBlock> GetDifferences(byte[] initialData, byte[] finalData)
    {
        var initialPixels = MemoryMarshal.Cast<byte, int>(initialData);
        var finalPixels = MemoryMarshal.Cast<byte, int>(finalData);

        var diffBlocks = new List<DrawingOperationWithDiffState.DiffBlock>();

        // Defensive: ensure arrays have data before accessing index 0
        if (initialPixels.Length == 0 || finalPixels.Length == 0)
            return diffBlocks;

        // The loop below walks `initialPixels` and indexes `finalPixels` with the same i, so a target that
        // was resized mid-operation (crop/canvas-resize between StartNewDrawingOperation and this call —
        // e.g. crop → transform selection → Clear layer) read past the end of the shorter buffer:
        // IndexOutOfRangeException out of an otherwise ordinary Clear/stroke commit (appstat, 3.10.0).
        // A pixel-by-pixel diff across a resize is meaningless anyway — the run-length cover no longer
        // maps onto the canvas, and DrawingOperationWithDiffState.ApplyChanges would refuse to replay it —
        // so record no diff instead. The resize operation itself carries the pixel state for that step.
        if (initialPixels.Length != finalPixels.Length)
        {
            Logger.Trace($"Skipping drawing diff across a target resize: {initialPixels.Length} pixels before,"
                         + $" {finalPixels.Length} after.");
            return diffBlocks;
        }

        var prevDiff = finalPixels[0] - initialPixels[0];
        var blockLen = 0;
        int _p0 = 0, _p1 = 0;
        for (var i = 0; i < initialPixels.Length; i++)
        {
            var p0 = initialPixels[i];
            var p1 = finalPixels[i];
            var diff = p1 - p0;
            if (prevDiff != diff)
            {
                diffBlocks.Add(new DrawingOperationWithDiffState.DiffBlock(blockLen, _p0, _p1));
                blockLen = 0;
            }

            blockLen++;
            _p0 = p0;
            _p1 = p1;
            prevDiff = diff;
        }

        if (blockLen > 0)
            diffBlocks.Add(new DrawingOperationWithDiffState.DiffBlock(blockLen, _p0, _p1));

        return diffBlocks;
    }


    public void CancelCurrentOperation()
    {
        drawingLayer.CancelCurrentOperation();
        _initialData = null;
        _currentOperationDrawingTarget = null;
        IsOperationStarted = false;
    }

    public void CancelActiveDrawing()
    {
        drawingLayer.CancelActiveDrawing();
        _initialData = null;
        _currentOperationDrawingTarget = null;
        IsOperationStarted = false;
    }

    public void PushCurrentOperationAndStartNew(IDrawingTarget drawingTarget)
    {
        drawingLayer.ApplyDrawing();
        FinishCurrentDrawingOperation();
        StartNewDrawingOperation(drawingTarget);
    }
}