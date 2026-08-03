#if DEBUG
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Declarative.Avalonia.AgentTools;
using ModelContextProtocol.Server;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.State;
using static Pix2d.Desktop.AgentTools.AgentToolHelpers;

namespace Pix2d.Desktop.AgentTools;

/// <summary>
/// State-changing Pix2d MCP tools. <see cref="AgentInteractionToolsAttribute"/> ties them to the
/// inspector's <c>EnableInteraction</c> switch, exactly like the built-in <c>invoke</c> tier — with
/// interaction off, only <see cref="Pix2dInspectionTools"/> is exposed.
/// <para>
/// These are the shortcuts around the UI, not a replacement for it: driving the real toolbar/canvas with
/// <c>tap</c>/<c>drag</c> is still what tests the UI. Reach for these when the UI is not what's under test
/// (e.g. put the editor in the state you need, then assert with <c>pix2d_pixels</c>).
/// </para>
/// </summary>
[McpServerToolType]
[AgentInteractionTools]
public sealed class Pix2dEditingTools(AppState state, ICommandService commandService, IToolService toolService)
{
    [McpServerTool(Name = "pix2d_command", Destructive = true), Description(
        "Executes a Pix2d command by name (see pix2d_commands), e.g. 'Edit.Undo', 'View.ZoomIn', " +
        "'Sprite.AddFrame'. Reports the edit context / tool / frame before and after so the effect is " +
        "visible. NOTE: execution is gated on the command's CanExecute flag only, NOT on the edit context — " +
        "a Sprite-context command runs in General too, so check the context first.")]
    public Task<string> RunCommand(
        [Description("Full command name, e.g. 'Edit.Undo'. Case-insensitive.")] string name)
        => OnUiAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(name))
                return "command name is required";

            if (!commandService.TryGetCommand(name, out var command))
            {
                var candidates = commandService.GetCommands()
                    .Where(c => c.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Name)
                    .Take(15)
                    .ToList();

                return $"no command named '{name}'." +
                       (candidates.Count > 0
                           ? $" Did you mean: {string.Join(", ", candidates)}"
                           : " Call pix2d_commands to list them.");
            }

            var before = Snapshot();
            if (!command.CanExecute(null))
                return $"'{command.Name}' is currently disabled (CanExecute=false).\n{before}";

            try
            {
                await commandService.ExecuteCommandAsync(command.Name);
            }
            catch (Exception e)
            {
                return $"'{command.Name}' threw {e.GetType().Name}: {e.Message}\nbefore: {before}\nafter:  {Snapshot()}";
            }

            return $"executed '{command.Name}'" +
                   $"{(command.EditContextType.HasValue ? $" (declared context {command.EditContextType})" : "")}\n" +
                   $"before: {before}\nafter:  {Snapshot()}";
        });

    [McpServerTool(Name = "pix2d_select_tool", Destructive = true), Description(
        "Activates a drawing/editing tool by name ('PenTool', 'pen', 'FillTool', 'ObjectManipulationTool', …) " +
        "without hunting for its toolbar button. Worth knowing: creating a project or a new tab re-activates " +
        "the default brush, so re-select the tool under test AFTER the canvas exists.")]
    public Task<string> SelectTool(
        [Description("Tool name; the 'Tool' suffix is optional. Omit to list the registered tools.")]
        string? name = null) => OnUi(() =>
    {
        var tools = state.ToolsState.Tools;

        if (string.IsNullOrWhiteSpace(name))
            return "registered tools:\n" + string.Join("\n", tools.Select(t =>
                $"  {(t.Name == state.ToolsState.CurrentToolKey ? "*" : " ")} {t.Name}  context={t.Context}" +
                $"  group={t.GroupName ?? "-"}  inToolbar={t.ShowInToolbar}"));

        var query = name.Trim();
        var match = tools.FirstOrDefault(t => string.Equals(t.Name, query, StringComparison.OrdinalIgnoreCase))
                    ?? tools.FirstOrDefault(t => string.Equals(t.Name, query + "Tool", StringComparison.OrdinalIgnoreCase))
                    ?? tools.FirstOrDefault(t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (match == null)
            return $"no tool matches '{name}'. Registered: {string.Join(", ", tools.Select(t => t.Name))}";

        var previous = state.ToolsState.CurrentToolKey;
        toolService.ActivateTool(match.ToolType);

        return $"tool: {previous ?? "(none)"} → {state.ToolsState.CurrentToolKey}" +
               $" (requested {match.Name}, context {match.Context}; editor context is {state.CurrentProject.CurrentContextType})";
    });

    [McpServerTool(Name = "pix2d_set_color", Destructive = true), Description(
        "Sets the active drawing color (or the sprite background) from a hex string — the reliable way to " +
        "make a stroke assertable: set a known color, draw, then read it back with pix2d_pixels.")]
    public Task<string> SetColor(
        [Description("Color as #rgb / #rgba / #rrggbb / #rrggbbaa (the '#' is optional).")] string color,
        [Description("'foreground' (default) or 'background'.")] string target = "foreground") => OnUi(() =>
    {
        if (!TryParseColor(color, out var parsed))
            return $"cannot parse color '{color}' — use #rgb, #rgba, #rrggbb or #rrggbbaa";

        var spriteEditorState = state.SpriteEditorState;
        switch (target.Trim().ToLowerInvariant())
        {
            case "foreground":
            case "fg":
            case "color":
                var previousFg = spriteEditorState.CurrentColor;
                spriteEditorState.CurrentColor = parsed;
                return $"foreground: {Hex(previousFg)} → {Hex(spriteEditorState.CurrentColor)}";

            case "background":
            case "bg":
                var previousBg = spriteEditorState.BackgroundColor;
                spriteEditorState.BackgroundColor = parsed;
                return $"background: {Hex(previousBg)} → {Hex(spriteEditorState.BackgroundColor)}";

            default:
                return $"unknown target '{target}' — use 'foreground' or 'background'";
        }
    });

    [McpServerTool(Name = "pix2d_set_option", Destructive = true), Description(
        "Sets a property on the AppState tree by dotted path — the way to reach a behaviour that only a " +
        "Settings combo box normally toggles, without restarting: 'MouseWheelBehavior=Zoom' (so a wheel " +
        "notch zooms instead of pans), 'IsStylusModeEnabled=true', 'IsSingleFingerPanEnabled=true', " +
        "'SpriteEditorState.FrameRate=24', 'CurrentProject.ViewPortState.ShowGrid=true'. Omit 'value' to " +
        "read the current value. Note this writes AppState only — a value the Settings UI also persists " +
        "through ISettingsService is not saved here, which is usually what you want for a test.")]
    public Task<string> SetOption(
        [Description("Dotted path from AppState, e.g. 'MouseWheelBehavior' or 'SpriteEditorState.FrameRate'.")]
        string path,
        [Description("New value: true/false, a number, an enum name, or #rrggbb for colors. Omit to just read.")]
        string? value = null) => OnUi(() =>
    {
        if (string.IsNullOrWhiteSpace(path))
            return "path is required, e.g. 'MouseWheelBehavior'";

        object? target = state;
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        System.Reflection.PropertyInfo? property = null;

        for (var i = 0; i < segments.Length; i++)
        {
            if (target == null)
                return $"'{string.Join('.', segments.Take(i))}' is null";

            property = target.GetType().GetProperty(segments[i],
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.IgnoreCase);

            if (property == null)
            {
                var available = target.GetType()
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Select(p => p.Name)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
                return $"'{segments[i]}' not found on {target.GetType().Name}. Available: {string.Join(", ", available)}";
            }

            if (i < segments.Length - 1)
                target = property.GetValue(target);
        }

        if (property == null || target == null)
            return $"cannot resolve '{path}'";

        var current = property.GetValue(target);
        if (value == null)
            return $"{path} = {Describe(current)} ({property.PropertyType.Name}" +
                   (property.PropertyType.IsEnum ? $"; one of {string.Join(", ", Enum.GetNames(property.PropertyType))}" : "") +
                   $"), writable={property.CanWrite}";

        if (!property.CanWrite)
            return $"{path} is read-only (currently {Describe(current)})";

        if (!TryConvert(value, property.PropertyType, out var converted, out var conversionError))
            return $"cannot set {path} to '{value}': {conversionError}";

        property.SetValue(target, converted);
        return $"{path}: {Describe(current)} → {Describe(property.GetValue(target))}";
    });

    private static string Describe(object? value) => value switch
    {
        null => "(null)",
        SkiaSharp.SKColor color => Hex(color),
        _ => value.ToString() ?? "(?)"
    };

    private static bool TryConvert(string text, Type type, out object? result, out string error)
    {
        result = null;
        error = "";
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        try
        {
            if (underlying == typeof(string))
                result = text;
            else if (underlying.IsEnum)
                result = Enum.Parse(underlying, text, ignoreCase: true);
            else if (underlying == typeof(SkiaSharp.SKColor))
            {
                if (!TryParseColor(text, out var color))
                {
                    error = "expected a color like #rrggbb";
                    return false;
                }

                result = color;
            }
            else
                result = Convert.ChangeType(text, underlying, System.Globalization.CultureInfo.InvariantCulture);

            return true;
        }
        catch (Exception e)
        {
            error = $"{e.GetType().Name}: {e.Message}" +
                    (underlying.IsEnum ? $" (expected one of {string.Join(", ", Enum.GetNames(underlying))})" : "");
            return false;
        }
    }

    private string Snapshot()
    {
        var project = state.CurrentProject;
        var sb = new StringBuilder();
        sb.Append($"context={project.CurrentContextType} tool={state.ToolsState.CurrentToolKey ?? "-"}");
        sb.Append($" tabs={state.LoadedProjects.Count}@{state.ActiveProjectIndex}");
        sb.Append($" artboards={project.SceneNode?.Nodes.OfType<Pix2d.CommonNodes.Pix2dSprite>().Count() ?? 0}");
        sb.Append($" edited=\"{project.CurrentEditedNode?.Name ?? "-"}\"");
        sb.Append($" frame={state.SpriteEditorState.CurrentFrameIndex}/{state.SpriteEditorState.FramesCount}");
        sb.Append($" layer={state.SpriteEditorState.CurrentLayerIndex}");
        sb.Append($" objectsSelected={project.Selection?.Nodes.Length ?? 0}");
        sb.Append($" pixelSelection={state.SpriteEditorState.HasSelection}");
        sb.Append($" unsaved={project.HasUnsavedChanges}");
        return sb.ToString();
    }
}
#endif
