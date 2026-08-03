#if DEBUG
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Pix2d.Abstract.Services;
using SkiaNodes;
using SkiaSharp;

namespace Pix2d.Desktop.AgentTools;

/// <summary>
/// Shared plumbing for Pix2d's own MCP tool packs (see <see cref="Pix2dInspectionTools"/> /
/// <see cref="Pix2dEditingTools"/>), registered with the in-process AgentTools inspector via
/// <c>UseAgentInspector(o => o.WithTools&lt;…&gt;())</c>. Debug-only, like the inspector itself.
/// <para>
/// Two things every tool here needs: (1) everything it touches — <c>AppState</c>, the scene graph,
/// the <see cref="ViewPort"/> — is UI-thread-affine while MCP calls arrive on ASP.NET request
/// threads, so every tool body runs through <see cref="OnUi{T}"/>; (2) the editor canvas is one
/// opaque <see cref="Control"/> to the generic inspector, so mapping artwork pixels to the
/// absolute client-DIP coordinates its pointer tools take is the bridge that makes them usable
/// on the canvas at all (<see cref="CanvasFrame"/>).
/// </para>
/// </summary>
internal static class AgentToolHelpers
{
    public static async Task<T> OnUi<T>(Func<T> func) => await Dispatcher.UIThread.InvokeAsync(func);

    /// <summary>
    /// Same, for a tool body that awaits (e.g. <c>ICommandService.ExecuteCommandAsync</c>). The explicit
    /// type argument is required: without it the call is ambiguous between Avalonia's
    /// <c>InvokeAsync&lt;TResult&gt;(Func&lt;TResult&gt;)</c> and <c>InvokeAsync&lt;TResult&gt;(Func&lt;Task&lt;TResult&gt;&gt;)</c>.
    /// </summary>
    public static Task<T> OnUiAsync<T>(Func<Task<T>> body) => Dispatcher.UIThread.InvokeAsync<T>(body);

    /// <summary>
    /// The live editor canvas control plus everything needed to convert between artwork pixels and
    /// the coordinate frame the inspector's pointer/screenshot tools speak.
    /// <para>
    /// <b>Three spaces.</b> <i>canvas</i> = a pixel of the artboard (sprite-local, what the artist
    /// sees as "pixel 3,7"); <i>world</i> = scene coordinates (an artboard sits at its own scene
    /// offset); <i>screen</i> = absolute client DIP of the window, the frame
    /// <c>click_at</c>/<c>tap</c>/<c>drag</c>/<c>pointer_*</c>/<c>screenshot_region</c> use.
    /// The viewport itself works in physical pixels — <see cref="SkiaCanvas"/> multiplies every
    /// incoming pointer DIP by <see cref="ViewPort.ScaleFactor"/> — hence the /Scale on the way out.
    /// </para>
    /// </summary>
    public sealed class CanvasFrame
    {
        public SkiaCanvas Canvas { get; init; } = null!;
        public ViewPort ViewPort { get; init; } = null!;
        public Point Origin { get; init; }
        public float Scale => ViewPort.ScaleFactor <= 0 ? 1f : ViewPort.ScaleFactor;

        public SKPoint WorldToScreen(SKPoint world)
        {
            var vp = ViewPort.WorldToViewport(world);
            return new SKPoint((float)(Origin.X + vp.X / Scale), (float)(Origin.Y + vp.Y / Scale));
        }

        public SKPoint ScreenToWorld(SKPoint screen)
            => ViewPort.ViewportToWorld(new SKPoint(
                (float)((screen.X - Origin.X) * Scale),
                (float)((screen.Y - Origin.Y) * Scale)));

        public SKRect WorldToScreen(SKRect world)
        {
            var p0 = WorldToScreen(new SKPoint(world.Left, world.Top));
            var p1 = WorldToScreen(new SKPoint(world.Right, world.Bottom));
            return new SKRect(p0.X, p0.Y, p1.X, p1.Y);
        }
    }

    /// <summary>
    /// Finds the editor canvas in the live visual tree (the head has exactly one) and captures its
    /// window-relative origin. Null before the first project/viewport exists. The <see cref="ViewPort"/>
    /// comes from <see cref="IViewPortService"/> — <c>SkiaCanvas</c> keeps its own reference private, and
    /// the service is handed the very same instance in <c>SkiaCanvas.InitCore</c>.
    /// </summary>
    public static CanvasFrame? GetCanvasFrame(IViewPortService viewPortService)
    {
        var canvas = FindCanvas();
        if (canvas == null || viewPortService.ViewPort == null)
            return null;

        var topLevel = TopLevel.GetTopLevel(canvas);
        var origin = topLevel == null ? new Point() : canvas.TranslatePoint(new Point(0, 0), topLevel) ?? new Point();

        return new CanvasFrame { Canvas = canvas, ViewPort = viewPortService.ViewPort, Origin = origin };
    }

    private static SkiaCanvas? FindCanvas()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                var canvas = window.GetVisualDescendants().OfType<SkiaCanvas>().FirstOrDefault();
                if (canvas != null)
                    return canvas;
            }
        }

        return null;
    }

    public static string Hex(SKColor color) => $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}{color.Alpha:x2}";

    /// <summary>Parses <c>#rgb</c>/<c>#rgba</c>/<c>#rrggbb</c>/<c>#rrggbbaa</c> (with or without the #).</summary>
    public static bool TryParseColor(string? text, out SKColor color)
    {
        color = SKColors.Transparent;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = text.Trim().TrimStart('#');
        if (s.Length is 3 or 4)
            s = string.Concat(s.Select(c => new string(c, 2)));

        if (s.Length == 6)
            s += "ff";

        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
            return false;

        color = new SKColor((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }

    public static string N(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Pt(SKPoint p) => $"({N(p.X)}, {N(p.Y)})";

    public static string Rect(SKRect r) => $"({N(r.Left)}, {N(r.Top)}) {N(r.Width)}×{N(r.Height)}";

    public static string Size(SKSize s) => $"{N(s.Width)}×{N(s.Height)}";
}
#endif
