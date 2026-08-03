#if DEBUG
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Pix2d.Abstract.Services;
using Pix2d.CommonNodes;
using Pix2d.State;
using SkiaNodes;
using SkiaSharp;
using static Pix2d.Desktop.AgentTools.AgentToolHelpers;

namespace Pix2d.Desktop.AgentTools;

/// <summary>
/// Read-only Pix2d MCP tools: what the generic AgentTools inspector cannot see, because to it the whole
/// editor is one opaque control. These answer "what is the app's state", "what is on the scene", "what
/// exactly got drawn" and "where do I click to hit artwork pixel (x,y)".
/// <para>
/// Registered in <see cref="Program.BuildAvaloniaApp"/> via <c>UseAgentInspector(o =&gt; o.WithTools&lt;…&gt;())</c>;
/// constructed from Pix2d's own DI container, so it takes the real services.
/// </para>
/// </summary>
[McpServerToolType]
public sealed class Pix2dInspectionTools(AppState state, ICommandService commandService, IViewPortService viewPortService)
{
    private const int MaxPixelCells = 4096;
    private const string PaletteChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    [McpServerTool(Name = "pix2d_state", ReadOnly = true), Description(
        "Pix2d's editor state in one call: active tool, edit context, open project tabs, artboards, the " +
        "active sprite's layers/frames, brush + colors, selection, and the viewport's zoom/pan. Start here " +
        "instead of guessing from the visual tree — none of this is reachable through Avalonia controls.")]
    public Task<string> GetState() => OnUi(() =>
    {
        var sb = new StringBuilder();
        var project = state.CurrentProject;

        sb.AppendLine($"tool: {state.ToolsState.CurrentToolKey ?? "(none)"}" +
                      $"{(string.IsNullOrEmpty(state.ToolsState.ActiveToolGroup) ? "" : $" group={state.ToolsState.ActiveToolGroup}")}" +
                      $"{(state.ToolsState.IsColorPickerModeActive ? " [alt-eyedropper]" : "")}" +
                      $"  context={project.CurrentContextType}");
        sb.AppendLine($"app: locale={state.Locale} uiScale={N(state.UiScale)} license={state.LicenseType} busy={state.IsBusy}" +
                      $" wheel={state.MouseWheelBehavior} stylusMode={state.IsStylusModeEnabled}");

        sb.AppendLine($"tabs: {state.LoadedProjects.Count} (active {state.ActiveProjectIndex})" +
                      string.Concat(state.LoadedProjects.Select((p, i) =>
                          $"\n  [{i}]{(i == state.ActiveProjectIndex ? "*" : " ")} \"{p.Title}\" unsaved={p.HasUnsavedChanges} new={p.IsNewProject} ctx={p.CurrentContextType}")));

        var artboards = GetArtboards().ToList();
        var active = GetActiveSprite();
        var frame = GetCanvasFrame(viewPortService);
        sb.AppendLine($"artboards: {artboards.Count}");
        foreach (var a in artboards)
        {
            var world = a.GetBoundingBox();
            var screen = frame == null ? "" : $" screen={Rect(frame.WorldToScreen(world))}";
            sb.AppendLine($"  {(a == active ? "*" : " ")} \"{a.Name}\" {Size(a.Size)} world={Rect(world)}{screen}" +
                          $" layers={a.LayersCount} frames={a.GetFramesCount()}");
        }

        if (active != null)
        {
            sb.AppendLine($"sprite: \"{active.Name}\" {Size(active.Size)} frame={active.CurrentFrameIndex}/{active.GetFramesCount()}" +
                          $" fps={N(active.FrameRate)} playing={active.IsPlaying} onionSkin={state.SpriteEditorState.ShowOnionSkin}");
            foreach (var layer in active.Layers)
                sb.AppendLine($"  {(layer.Index == active.SelectedLayerIndex ? "*" : " ")} [{layer.Index}] \"{layer.Name}\"" +
                              $" visible={layer.IsVisible} opacity={N(layer.Opacity)} frames={layer.FrameCount}" +
                              $" lockAlpha={layer.LockTransparentPixels}");
        }
        else
        {
            sb.AppendLine("sprite: (no active artboard)");
        }

        var ses = state.SpriteEditorState;
        sb.AppendLine($"color: fg={Hex(ses.CurrentColor)} bg={Hex(ses.BackgroundColor)} showBackground={ses.ShowBackground}");
        sb.AppendLine($"brush: size={N(ses.CurrentBrushSettings.Scale)} opacity={N(ses.Opacity)} spacing={N(ses.Spacing)}" +
                      $" pixelPerfect={ses.IsPixelPerfectDrawingModeEnabled}");

        var objectSelection = project.Selection?.Nodes ?? [];
        sb.AppendLine($"selection: pixels={ses.HasSelection} userSelecting={state.SelectionState.IsUserSelecting}" +
                      $" objects={objectSelection.Length}" +
                      (objectSelection.Length > 0
                          ? $" [{string.Join(", ", objectSelection.Select(n => $"\"{n.Name}\""))}] bounds={Rect(project.Selection!.Bounds)}"
                          : ""));

        if (frame != null)
        {
            var vp = frame.ViewPort;
            // ViewPort.Size is the canvas control's DIP size (SkiaCanvas.GetViewPortSize), while the
            // transform maps world → DIP*ScaleFactor, which is where the /Scale in CanvasFrame comes from.
            sb.AppendLine($"viewport: size={Size(vp.Size)} DIP scale={N(vp.ScaleFactor)} zoom={N(vp.Zoom)}" +
                          $" (effective {N(vp.DpiEffectiveZoom)}) pan={Pt(vp.Pan)} visibleWorld={Rect(vp.GetVisibleArea())}");
            sb.AppendLine($"canvas control: origin=({N(frame.Origin.X)}, {N(frame.Origin.Y)}) " +
                          $"size={N(frame.Canvas.Bounds.Width)}×{N(frame.Canvas.Bounds.Height)} DIP" +
                          $"  grid={project.ViewPortState.ShowGrid} spacing={Size(project.ViewPortState.GridSpacing)}");
        }
        else
        {
            sb.AppendLine("viewport: (not initialized)");
        }

        return sb.ToString().TrimEnd();
    });

    [McpServerTool(Name = "pix2d_scene_tree", ReadOnly = true), Description(
        "The SKNode scene graph of the active project — artboards, layers, frame sprites — with sizes, " +
        "world bounds and (for the top levels) the absolute client-DIP rect to click. This is the canvas " +
        "equivalent of get_visual_tree, which only ever sees one control here.")]
    public Task<string> GetSceneTree(
        [Description("Depth to walk. 1 = artboards only, 2 = + layers, 3 = + per-frame sprites. Default 3.")]
        int? maxDepth = null,
        [Description("Include adorner/overlay nodes (selection frames, labels, grid). Default false.")]
        bool includeAdorners = false) => OnUi(() =>
    {
        var scene = state.CurrentProject.SceneNode;
        if (scene == null)
            return "(no scene — no project loaded)";

        var frame = GetCanvasFrame(viewPortService);
        var active = GetActiveSprite();
        var selected = state.CurrentProject.Selection?.Nodes ?? [];
        var depth = Math.Clamp(maxDepth ?? 3, 1, 8);
        var sb = new StringBuilder();

        void Dump(SKNode node, int level)
        {
            var indent = new string(' ', level * 2);
            var world = node.GetBoundingBox();
            var flags = new List<string>();
            if (node == active) flags.Add("ACTIVE");
            if (selected.Contains(node)) flags.Add("SELECTED");
            if (!node.IsVisible) flags.Add("hidden");
            if (node.IsAdorner) flags.Add("adorner");
            if (Math.Abs(node.Opacity - 1f) > 0.001f) flags.Add($"opacity={N(node.Opacity)}");

            var extra = node switch
            {
                Pix2dSprite s => $" layers={s.LayersCount} frames={s.GetFramesCount()} currentFrame={s.CurrentFrameIndex} fps={N(s.FrameRate)}",
                Pix2dSprite.Layer l => $" frames={l.FrameCount}",
                _ => ""
            };

            var screen = frame != null && level <= 1 ? $" screen={Rect(frame.WorldToScreen(world))}" : "";

            sb.AppendLine($"{indent}{node.GetType().Name} \"{node.Name}\" {Size(node.Size)} pos={Pt(node.Position)}" +
                          $" world={Rect(world)}{screen}{extra}" +
                          (flags.Count > 0 ? $" [{string.Join(" ", flags)}]" : ""));

            if (level + 1 >= depth)
            {
                var hiddenChildren = node.Nodes.Count(n => includeAdorners || !n.IsAdorner);
                if (hiddenChildren > 0)
                    sb.AppendLine($"{indent}  … {hiddenChildren} more node(s) (raise maxDepth)");
                return;
            }

            foreach (var child in node.Nodes)
            {
                if (!includeAdorners && child.IsAdorner)
                    continue;
                Dump(child, level + 1);
            }
        }

        sb.AppendLine($"scene \"{scene.Name}\" ({scene.GetType().Name}) — depth {depth}, " +
                      $"coords: world = scene units, screen = absolute client DIP (pointer/screenshot frame)");
        foreach (var child in scene.Nodes)
        {
            if (!includeAdorners && child.IsAdorner)
                continue;
            Dump(child, 0);
        }

        return sb.ToString().TrimEnd();
    });

    [McpServerTool(Name = "pix2d_pixels", ReadOnly = true), Description(
        "Reads actual artwork pixels as a compact character grid + hex legend, so a drawing operation can " +
        "be ASSERTED instead of eyeballed on a screenshot. Coordinates are artboard pixels (0,0 = top-left " +
        "of the sprite), independent of zoom/pan. Reads the composited frame by default, or one layer.")]
    public Task<string> GetPixels(
        [Description("Left edge, in artboard pixels.")] int x = 0,
        [Description("Top edge, in artboard pixels.")] int y = 0,
        [Description("Width in pixels. Default 16. width*height must be <= 4096.")] int width = 16,
        [Description("Height in pixels. Default 16.")] int height = 16,
        [Description("Layer index to read instead of the composited result (0 = bottom).")] int? layer = null,
        [Description("Frame index. Default: the frame currently being edited.")] int? frame = null,
        [Description("Artboard name or #index. Default: the active artboard.")] string? artboard = null) => OnUi(() =>
    {
        if (!TryResolveArtboard(artboard, out var sprite, out var error))
            return error;

        var frameIndex = frame ?? sprite.CurrentFrameIndex;
        var frameCount = sprite.GetFramesCount();
        if (frameIndex < 0 || frameIndex >= frameCount)
            return $"frame {frameIndex} out of range (sprite has {frameCount} frame(s))";

        if (width <= 0 || height <= 0)
            return "width and height must be positive";
        if ((long)width * height > MaxPixelCells)
            return $"region {width}×{height} is {width * height} pixels; the cap is {MaxPixelCells}. Read it in tiles.";

        SKBitmap? bitmap = null;
        var source = "composited";
        try
        {
            if (layer.HasValue)
            {
                var layers = sprite.Layers.ToList();
                if (layer.Value < 0 || layer.Value >= layers.Count)
                    return $"layer {layer.Value} out of range (sprite has {layers.Count} layer(s))";

                var layerNode = layers[layer.Value];
                bitmap = layerNode.GetSpriteByFrame(frameIndex)?.Bitmap;
                source = $"layer {layer.Value} \"{layerNode.Name}\"";
                if (bitmap == null)
                    return $"{source}, frame {frameIndex}: no bitmap (empty frame) — all pixels transparent";
            }
            else
            {
                bitmap = sprite.GetFramePreview(frameIndex);
            }

            var sb = new StringBuilder();
            sb.AppendLine($"\"{sprite.Name}\" {Size(sprite.Size)} — {source}, frame {frameIndex}, region ({x},{y}) {width}×{height}");

            var symbols = new Dictionary<SKColor, char>();
            var rows = new List<string>();
            var opaque = 0;

            for (var row = 0; row < height; row++)
            {
                var line = new StringBuilder();
                for (var col = 0; col < width; col++)
                {
                    var px = x + col;
                    var py = y + row;
                    if (px < 0 || py < 0 || px >= bitmap.Width || py >= bitmap.Height)
                    {
                        line.Append('?');
                        continue;
                    }

                    var color = bitmap.GetPixel(px, py);
                    if (color.Alpha == 0)
                    {
                        line.Append('.');
                        continue;
                    }

                    opaque++;
                    if (!symbols.TryGetValue(color, out var ch))
                    {
                        if (symbols.Count >= PaletteChars.Length)
                        {
                            ch = '+';
                        }
                        else
                        {
                            ch = PaletteChars[symbols.Count];
                            symbols[color] = ch;
                        }
                    }

                    line.Append(ch);
                }

                rows.Add(line.ToString());
            }

            var labelWidth = (y + height - 1).ToString(CultureInfo.InvariantCulture).Length;
            sb.AppendLine(new string(' ', labelWidth + 1) + BuildColumnRuler(x, width));
            for (var row = 0; row < rows.Count; row++)
                sb.AppendLine((y + row).ToString(CultureInfo.InvariantCulture).PadLeft(labelWidth) + " " + rows[row]);

            sb.AppendLine($"legend: '.'=transparent" +
                          (symbols.Count >= PaletteChars.Length ? ", '+'=(palette overflow)" : "") +
                          ", '?'=outside the sprite");
            foreach (var (color, ch) in symbols.OrderBy(kv => kv.Value))
                sb.AppendLine($"  {ch} = {Hex(color)}");
            sb.Append($"{opaque} non-transparent pixel(s) of {width * height}, {symbols.Count} distinct color(s)");

            return sb.ToString();
        }
        finally
        {
            if (!layer.HasValue)
                bitmap?.Dispose();
        }
    });

    [McpServerTool(Name = "pix2d_canvas_png", ReadOnly = true), Description(
        "Renders one artboard frame to a PNG at exact artwork resolution (nearest-neighbour upscale via " +
        "'scale'), bypassing screenshots entirely: no UI chrome, no zoom/pan, no viewport transform. Use it " +
        "to see the artwork itself; use screenshot_window when you need the surrounding UI.")]
    public Task<CallToolResult> GetCanvasPng(
        [Description("Nearest-neighbour upscale factor, 1-32. Default 1 (one image pixel per artwork pixel).")]
        int scale = 1,
        [Description("Frame index. Default: the frame currently being edited.")] int? frame = null,
        [Description("Artboard name or #index. Default: the active artboard.")] string? artboard = null,
        [Description("Composite over the sprite's background color instead of transparency. Default false.")]
        bool withBackground = false) => OnUi(() =>
    {
        if (!TryResolveArtboard(artboard, out var sprite, out var error))
            return Text(error, isError: true);

        var frameIndex = frame ?? sprite.CurrentFrameIndex;
        var frameCount = sprite.GetFramesCount();
        if (frameIndex < 0 || frameIndex >= frameCount)
            return Text($"frame {frameIndex} out of range (sprite has {frameCount} frame(s))", isError: true);

        scale = Math.Clamp(scale, 1, 32);

        using var rendered = sprite.GetFramePreview(frameIndex, 1f, withBackground);
        var image = rendered;
        SKBitmap? upscaled = null;
        try
        {
            if (scale > 1)
            {
                upscaled = rendered.Resize(
                    new SKSizeI(rendered.Width * scale, rendered.Height * scale),
                    new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
                if (upscaled != null)
                    image = upscaled;
            }

            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();

            return new CallToolResult
            {
                Content =
                [
                    new TextContentBlock
                    {
                        Text = $"\"{sprite.Name}\" frame {frameIndex} of {frameCount} — {rendered.Width}×{rendered.Height} " +
                               $"artwork pixels rendered at {scale}× ({image.Width}×{image.Height} PNG, {bytes.Length} bytes)"
                    },
                    // ImageContentBlock.Data is the wire field and holds the base64 TEXT (DecodedData is the
                    // read-only raw view) — handing it the PNG bytes straight puts raw binary in the JSON.
                    new ImageContentBlock
                    {
                        Data = Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes)),
                        MimeType = "image/png"
                    }
                ]
            };
        }
        finally
        {
            upscaled?.Dispose();
        }
    });

    [McpServerTool(Name = "pix2d_map_point", ReadOnly = true), Description(
        "Converts a point between the three coordinate frames that meet on the canvas — 'canvas' (artboard " +
        "pixel), 'world' (scene units) and 'screen' (absolute client DIP). Call this to turn 'draw at pixel " +
        "(3,7)' into the x/y that tap/drag/pointer_press take, or to find which pixel a screen point hit.")]
    public Task<string> MapPoint(
        [Description("X in the source space.")] double x,
        [Description("Y in the source space.")] double y,
        [Description("Source space: 'canvas' (artboard pixel, default), 'world' or 'screen'.")] string space = "canvas",
        [Description("Artboard name or #index for the canvas space. Default: the active artboard.")] string? artboard = null)
        => OnUi(() =>
        {
            var frame = GetCanvasFrame(viewPortService);
            if (frame == null)
                return "viewport not initialized — no canvas on screen yet";

            if (!TryResolveArtboard(artboard, out var sprite, out var error))
                return error;

            var transform = sprite.GetGlobalTransform();
            SKPoint world;
            switch (space.Trim().ToLowerInvariant())
            {
                case "canvas":
                case "pixel":
                case "sprite":
                    world = transform.MapPoint((float)x, (float)y);
                    break;
                case "world":
                case "scene":
                    world = new SKPoint((float)x, (float)y);
                    break;
                case "screen":
                case "dip":
                case "client":
                    world = frame.ScreenToWorld(new SKPoint((float)x, (float)y));
                    break;
                default:
                    return $"unknown space '{space}' — use 'canvas', 'world' or 'screen'";
            }

            var canvasPoint = sprite.GetLocalPosition(world);
            var screen = frame.WorldToScreen(world);
            var inside = canvasPoint.X >= 0 && canvasPoint.Y >= 0 &&
                         canvasPoint.X < sprite.Size.Width && canvasPoint.Y < sprite.Size.Height;

            // A whole canvas coordinate is a pixel *corner*, so the DIP it maps to sits on the boundary and
            // rounding can land the stroke one pixel up/left. Aim at the pixel's centre instead.
            var pixelX = (float)Math.Floor(canvasPoint.X);
            var pixelY = (float)Math.Floor(canvasPoint.Y);
            var centreScreen = frame.WorldToScreen(transform.MapPoint(pixelX + 0.5f, pixelY + 0.5f));

            return $"artboard \"{sprite.Name}\" {Size(sprite.Size)}\n" +
                   $"canvas: {Pt(canvasPoint)} → pixel ({N(pixelX)}, {N(pixelY)})" +
                   $" {(inside ? "inside" : "OUTSIDE the artboard")}\n" +
                   $"world:  {Pt(world)}\n" +
                   $"screen: {Pt(screen)}  (absolute client DIP — pass these to tap/drag/pointer_*)\n" +
                   $"screen of pixel ({N(pixelX)}, {N(pixelY)}) centre: {Pt(centreScreen)}  ← USE THIS to hit that pixel\n" +
                   $"one artboard pixel = {N(frame.ViewPort.Zoom)} DIP on screen (zoom {N(frame.ViewPort.Zoom)}, " +
                   $"render scale {N(frame.ViewPort.ScaleFactor)})";
        });

    [McpServerTool(Name = "pix2d_commands", ReadOnly = true), Description(
        "Lists Pix2d's registered commands with their shortcut and the edit context that gates them — the " +
        "inventory pix2d_command executes. Optional substring filter, e.g. 'Edit.' or 'export'.")]
    public Task<string> GetCommands(
        [Description("Case-insensitive substring filter on the command name.")] string? filter = null) => OnUi(() =>
    {
        var commands = commandService.GetCommands()
            .Where(c => string.IsNullOrWhiteSpace(filter) ||
                        c.Name.Contains(filter!, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (commands.Count == 0)
            return $"no commands match '{filter}'";

        var sb = new StringBuilder();
        sb.AppendLine($"{commands.Count} command(s){(string.IsNullOrWhiteSpace(filter) ? "" : $" matching '{filter}'")}" +
                      $" — current context is {state.CurrentProject.CurrentContextType}");
        foreach (var c in commands)
        {
            var shortcut = c.GetShortcutString();
            sb.AppendLine($"  {c.Name}" +
                          $"{(string.IsNullOrWhiteSpace(shortcut) ? "" : $"  [{shortcut}]")}" +
                          $"  context={c.EditContextType?.ToString() ?? "any"}" +
                          $"  canExecute={c.CanExecute(null)}");
        }

        return sb.ToString().TrimEnd();
    });

    private static string BuildColumnRuler(int startX, int width)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < width; i++)
        {
            var col = startX + i;
            sb.Append(col % 10 == 0 ? ((col / 10) % 10).ToString(CultureInfo.InvariantCulture) : "·");
        }

        sb.Append("  (tens marked; '·' = ones)");
        return sb.ToString();
    }

    private static CallToolResult Text(string text, bool isError = false) => new()
    {
        Content = [new TextContentBlock { Text = text }],
        IsError = isError ? true : null
    };

    private IEnumerable<Pix2dSprite> GetArtboards()
        => state.CurrentProject.SceneNode?.Nodes.OfType<Pix2dSprite>() ?? [];

    private Pix2dSprite? GetActiveSprite()
        => state.CurrentProject.CurrentEditedNode as Pix2dSprite
           ?? GetArtboards().FirstOrDefault();

    private bool TryResolveArtboard(string? selector, out Pix2dSprite sprite, out string error)
    {
        error = "";
        sprite = null!;

        if (string.IsNullOrWhiteSpace(selector))
        {
            var active = GetActiveSprite();
            if (active == null)
            {
                error = "no artboard on the scene (no project loaded?)";
                return false;
            }

            sprite = active;
            return true;
        }

        var artboards = GetArtboards().ToList();
        var s = selector.Trim();

        if (s.StartsWith('#') && int.TryParse(s[1..], out var index))
        {
            if (index < 0 || index >= artboards.Count)
            {
                error = $"artboard #{index} out of range (scene has {artboards.Count})";
                return false;
            }

            sprite = artboards[index];
            return true;
        }

        var match = artboards.FirstOrDefault(a => string.Equals(a.Name, s, StringComparison.OrdinalIgnoreCase))
                    ?? artboards.FirstOrDefault(a => a.Name.Contains(s, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            error = $"no artboard matches '{selector}'. Present: {string.Join(", ", artboards.Select(a => $"\"{a.Name}\""))}";
            return false;
        }

        sprite = match;
        return true;
    }
}
#endif
