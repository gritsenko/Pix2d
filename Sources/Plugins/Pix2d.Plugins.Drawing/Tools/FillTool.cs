﻿using System.Diagnostics;
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
