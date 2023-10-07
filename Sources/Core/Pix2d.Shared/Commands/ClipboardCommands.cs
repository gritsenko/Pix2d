using System;
using Pix2d.Abstract;
using Pix2d.Abstract.Commands;
using Pix2d.Primitives;
using Pix2d.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Command
{
    public class ClipboardCommands : CommandsListBase
    {
        protected override string BaseName => "Edit.Clipboard";

        public Pix2dCommand Copy =>
            GetCommand("Copy selection", new CommandShortcut(VirtualKeys.C, KeyModifier.Ctrl), EditContextType.General,
                () => throw new NotImplementedException());

        public Pix2dCommand TryPaste => GetCommand("Paste", new CommandShortcut(VirtualKeys.V, KeyModifier.Ctrl),
            EditContextType.General,
            () => { throw new NotImplementedException(); });


        public Pix2dCommand Cut => GetCommand("Cut selection", new CommandShortcut(VirtualKeys.X, KeyModifier.Ctrl),
            EditContextType.General,
            () => throw new NotImplementedException());

    }
}