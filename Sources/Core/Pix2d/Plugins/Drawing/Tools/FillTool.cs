using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Drawing.Brushes;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(HasSettings = true)]
public class FillTool : BaseTool, IDrawingTool
{
    public static ToolSettings ToolSettings { get; } = new()
    {
        DisplayName = "Fill tool",
        HotKey = null,
    };
    
    public IDrawingService DrawingService { get; }
    private readonly IPixelBrush _previewBrush = new SquareSolidBrush();
    private bool _eraseMode;

    public virtual BrushDrawingMode DrawingMode => EraseMode ? BrushDrawingMode.FillErase : BrushDrawingMode.Fill;

    public override EditContextType EditContextType => EditContextType.Sprite;
    public override string DisplayName => ToolSettings.DisplayName;

    public bool EraseMode
    {
        get => _eraseMode;
        set
        {
            _eraseMode = value;
            if (IsActive)
                DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
        }
    }

    public FillTool(IDrawingService drawingService)
    {
        DrawingService = drawingService;
    }

    public override async Task Activate()
    {
        await base.Activate();
        try
        {
            DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.Message);
            throw;
        }
    }
}
