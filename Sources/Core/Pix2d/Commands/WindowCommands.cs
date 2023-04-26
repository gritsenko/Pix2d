using Pix2d.Abstract.Commands;
using Pix2d.Primitives;

namespace Pix2d.Command;

public class WindowCommands : CommandsListBase {
    protected override string BaseName => "Window";

    public Pix2dCommand ToggleAlwaysOnTop => GetCommand(() => CommonServiceLocator.ServiceLocator.Current.GetInstance<IPlatformStuffService>().ToggleTopmostWindow());

}