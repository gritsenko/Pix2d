using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ClipboardCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Clipboard";

    // Clipboard in the General edit context is not implemented yet (sprite-context clipboard lives in
    // the Drawing plugin) — these must stay silent no-ops rather than throw: they're bound to global
    // shortcuts and a throwing placeholder ends up as a crash report.
    public Pix2dCommand Copy =>
        GetCommand(() => Logger.Trace("Clipboard.Copy is not implemented for the General context"), "Copy selection", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl), EditContextType.General);

    public Pix2dCommand TryPaste =>
        GetCommand(() => Logger.Trace("Clipboard.TryPaste is not implemented for the General context"), "Paste", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl), EditContextType.General);


    public Pix2dCommand Cut =>
        GetCommand(() => Logger.Trace("Clipboard.Cut is not implemented for the General context"), "Cut selection", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl), EditContextType.General);

}