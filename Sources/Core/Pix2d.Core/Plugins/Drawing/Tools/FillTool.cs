using System.Diagnostics;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.UI;

namespace Pix2d.Plugins.Drawing.Tools;

[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true,
    DisplayName = "Fill tool",
    SettingsViewType = typeof(FillToolSettingsView),
    HotKey = "G")]
public class FillTool : BaseTool, IDrawingTool
{
    public IDrawingService DrawingService { get; }
    private bool _eraseMode;

    public virtual BrushDrawingMode DrawingMode => EraseMode ? BrushDrawingMode.FillErase : BrushDrawingMode.Fill;

    public bool EraseMode
    {
        get => _eraseMode;
        set
        {
            _eraseMode = value;
            DrawingService.DrawingLayer.SetDrawingLayerMode(DrawingMode);
        }
    }

    /// <summary>
    /// Fill strength in percent (0..100). Scales the alpha of the color poured into the region — at 50
    /// the fill composites half-strength over the existing pixels, and in erase mode it removes half
    /// their alpha. Backed by <see cref="IDrawingLayer.FillOpacity"/>, which is what the pointer
    /// pipeline actually reads.
    /// </summary>
    public double Opacity
    {
        get => DrawingService.DrawingLayer.FillOpacity * 100d;
        set => DrawingService.DrawingLayer.FillOpacity = (float)(Math.Clamp(value, 0d, 100d) / 100d);
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
