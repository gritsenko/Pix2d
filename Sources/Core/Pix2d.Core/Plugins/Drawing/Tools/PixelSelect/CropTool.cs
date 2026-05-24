using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.Plugins.Drawing.UI;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.Plugins.Drawing.Tools.PixelSelect;

/// <summary>
/// Photoshop-style crop tool. Reuses the rectangular pixel-selection marquee as the crop frame —
/// activating with an existing selection seeds the frame from that selection; otherwise the user
/// drags out a new rectangle. No pixels are lifted (contour-only mode) since the frame describes the
/// crop bounds, not a transform target. Commit / cancel are exposed via <see cref="CropToolSettingsView"/>.
/// </summary>
[Pix2dTool(
    EditContextType = EditContextType.Sprite,
    HasSettings = true,
    SettingsViewType = typeof(CropToolSettingsView),
    DisplayName = "Crop tool",
    Group = "Pixel Select",
    HotKey = "C")]
public class CropTool(
    IDrawingService drawingService,
    IMessenger messenger,
    AppState state,
    IToolService toolService,
    IEditService editService,
    IViewPortService viewPortService,
    IViewPortRefreshService viewPortRefreshService)
    : PixelSelectToolBase(drawingService, messenger, state, toolService)
{
    public override async Task Activate()
    {
        SelectionMode = PixelSelectionMode.Rectangle;
        await base.Activate();

        // Seed the crop frame: if no marquee carried over from a previous selection tool, fall back to a
        // full-sprite Select-All so the user always sees a resizable frame immediately on activation.
        if (!DrawingService.DrawingLayer.HasSelection)
            DrawingService.SelectAll();

        // Frame-resize mode: keeps the resize handles visible in contour styling (black) so the user can
        // adjust the crop rectangle without the lifted-pixels semantics of transform mode. Persists
        // across fresh marquees drawn while this tool is active.
        DrawingService.DrawingLayer.SetFrameResizeMode(true);
    }

    public override void Deactivate()
    {
        DrawingService.DrawingLayer.SetFrameResizeMode(false);
        base.Deactivate();
    }

    /// <summary>
    /// Commits the crop: snaps the sprite to the marquee bounds and drops the frame. No-op when there
    /// is no active selection — the settings view disables its Apply button in that state, but the
    /// guard keeps callers (e.g. future keyboard shortcut) safe.
    /// </summary>
    public void ApplyCrop()
    {
        var drawingLayer = DrawingService.DrawingLayer;
        if (!drawingLayer.HasSelection) return;

        var selectionLayer = drawingLayer.GetSelectionLayer();
        var bounds = selectionLayer?.GetBoundingBox() ?? default;
        drawingLayer.ApplySelection();

        if (bounds == default || bounds.Width < 1 || bounds.Height < 1) return;

        editService.CropCurrentSprite(bounds);
        viewPortService.ShowAll();
        viewPortRefreshService.Refresh();
    }

    /// <summary>
    /// Drops the crop frame without modifying the sprite. The tool stays active so the user can draw
    /// a new frame (matches Photoshop's crop-cancel behaviour).
    /// </summary>
    public void CancelCrop()
    {
        var drawingLayer = DrawingService.DrawingLayer;
        if (!drawingLayer.HasSelection) return;

        drawingLayer.ApplySelection();
        viewPortRefreshService.Refresh();
    }
}
