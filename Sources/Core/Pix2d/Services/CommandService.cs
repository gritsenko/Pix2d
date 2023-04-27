using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.UI;
using Pix2d.Command;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Services;

public class CommandService : ICommandService
{
    private readonly IPlatformStuffService _platformStuffService;
    private readonly AppState _appState;
    private readonly IBusyController _busyController;
    private readonly Dictionary<string, Pix2dCommand> _commands = new Dictionary<string, Pix2dCommand>();

    public CommandService(IPlatformStuffService platformStuffService, AppState appState, IBusyController busyController)
    {
        _platformStuffService = platformStuffService;
        _appState = appState;
        _busyController = busyController;
        SKInput.Current.KeyPressed += OnKeyPressed;
    }

    public void Initialize()
    {

        RegisterCommandList<FileCommands>();
        RegisterCommandList<ViewCommands>();
        RegisterCommandList<EditCommands>();
        RegisterCommandList<ArrangeCommands>();
        RegisterCommandList<WindowCommands>();

#if DEBUG
        //cheats
        //RegisterCommand("Cheats.TogglePro",
        //    () => CoreServices.LicenseService.ToggleIsPro(), "Toggle PRO mode",
        //    new CommandShortcut(VirtualKeys.F11, KeyModifier.Ctrl));
#endif

    }

    public void RegisterCommandList<TCommandList>() where TCommandList : new()
    {
        var listInstance = new TCommandList() as ICommandList;

        RegisterCommandList(listInstance);
    }

    private async void OnKeyPressed(object sender, KeyboardActionEventArgs e)
    {
        if (e.Key == VirtualKeys.Control || e.Key == VirtualKeys.Menu)
        {
            return;
        }
        var editContextType = _appState.CurrentProject.CurrentContextType;
        if (FindCommand(e.Key, e.Modifiers, editContextType, out var command))
        {
            await ExecuteCommandAsync(command.Name);
        }
    }

    private bool FindCommand(VirtualKeys key, KeyModifier modifiers, EditContextType editContextType, out Pix2dCommand command)
    {
        command = null;
        foreach (var cmd in _commands.Values)
        {
            if (cmd.CheckShortcut(key, modifiers, editContextType))
            {
                command = cmd;
                return true;
            }
        }

        return false;
    }

    public Pix2dCommand RegisterAsyncCommand(string name, Func<Task> commandActionTask, string description, CommandShortcut? defaultShortcut, EditContextType? editContextType = null)
    {
        var cmd = new Pix2dAsyncCommand(name, description, defaultShortcut, editContextType, commandActionTask);
        if (cmd.DefaultShortcut != null)
            cmd.DefaultShortcut.KeyConverter = KeyToString;
        RegisterCommand(cmd);
        return cmd;
    }

    public Pix2dCommand RegisterSyncCommand(string name, Action commandAction, string description, CommandShortcut? defaultShortcut, EditContextType? editContextType = null)
    {
        var cmd = new Pix2dSyncCommand(name, description, defaultShortcut, editContextType, commandAction);
        if (cmd.DefaultShortcut != null)
            cmd.DefaultShortcut.KeyConverter = KeyToString;
        RegisterCommand(cmd);
        return cmd;
    }

    private string KeyToString(VirtualKeys key)
    {
        return _platformStuffService.KeyToString(key);
    }

    public void RegisterCommand(Pix2dCommand cmd)
    {
        _commands[cmd.Name] = cmd;
    }

    public async Task ExecuteCommandAsync(string name)
    {
        if (_commands.TryGetValue(name, out var command))
        {
            SessionLogger.OpLogCommand(command.Name);
            if (command is Pix2dAsyncCommand asyncCmd)
            {
                await _busyController.RunLongTaskAsync(asyncCmd.CommandActionTask);
            }
            else if (command is Pix2dSyncCommand syncCmd)
            {
                syncCmd.CommandAction();
            }
            return;
        }
        throw new Exception($"Command {name} not found");
    }

    public bool TryGetCommand(string name, out Pix2dCommand command)
    {
        if (_commands.TryGetValue(name, out var result))
        {
            command = result;
            return true;
        }

        command = null;
        return false;
    }

    public void RegisterCommandList(ICommandList commands)
    {
        foreach (var command in commands.GetCommands())
        {
            RegisterCommand(command);
        }
    }

    IEnumerable<Pix2dCommand> ICommandService.GetCommands()
    {
        return _commands.Values;
    }
}