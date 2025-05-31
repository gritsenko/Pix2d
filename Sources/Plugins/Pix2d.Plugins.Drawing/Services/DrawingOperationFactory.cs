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

            if (_initialData == null || finalData == null)
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

    public void PushCurrentOperationAndStartNew(IDrawingTarget drawingTarget)
    {
        drawingLayer.ApplyDrawing();
        FinishCurrentDrawingOperation();
        StartNewDrawingOperation(drawingTarget);
    }
}