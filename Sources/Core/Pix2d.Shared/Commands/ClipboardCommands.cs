﻿using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Command;

public class ClipboardCommands : CommandsListBase
{
    protected override string BaseName => "Edit.Clipboard";

    public Pix2dCommand Copy =>
        GetCommand(() => throw new NotImplementedException(), "Copy selection", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl), EditContextType.General);

    public Pix2dCommand TryPaste => 
        GetCommand(() => throw new NotImplementedException(), "Paste", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl), EditContextType.General);


    public Pix2dCommand Cut => 
        GetCommand(() => throw new NotImplementedException(), "Cut selection", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl), EditContextType.General);

}