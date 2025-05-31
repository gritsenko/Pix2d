#nullable enable
using Pix2d.Abstract.Commands;
using Pix2d.Command;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Services;

public class CommandService : ICommandService
{
    private readonly IPlatformStuffService _platformStuffService;
    private readonly AppState _appState;
    private readonly IServiceProvider? _serviceProvider;
    private readonly Dictionary<string, Pix2dCommand> _commands = new();
    private readonly List<ICommandList> _commandLists = [];
    public CommandService(IPlatformStuffService platformStuffService, AppState appState, IServiceProvider? serviceProvider)
    {
        _platformStuffService = platformStuffService;
        _appState = appState;
        _serviceProvider = serviceProvider;
        SKInput.Current.KeyPressed += OnKeyPressed;
    }

    public void Initialize()
    {
        RegisterCommandList<FileCommands>();
        RegisterCommandList<ViewCommands>();
        RegisterCommandList<EditCommands>();
        RegisterCommandList<ArrangeCommands>();
        RegisterCommandList<WindowCommands>();
        RegisterCommandList<SnappingCommands>();
#if DEBUG
        RegisterCommandList(new GlobalCommands());
#endif
    }

    public TCommandList? GetCommandList<TCommandList>() where TCommandList : ICommandList
    {
        return _commandLists.OfType<TCommandList>().FirstOrDefault();
    }

    public void RegisterCommandList<TCommandList>() where TCommandList : new()
    {
        var listInstance = (ICommandList)new TCommandList();
        RegisterCommandList(listInstance);
    }

    public void RegisterCommandList(ICommandList commandList)
    {
        if (commandList is CommandsListBase commandsListBase)
        {
            commandsListBase.SetCommandService(this);

            if (_serviceProvider == null)
                throw new Exception("ServiceProvider is null. Cannot set service provider for CommandsListBase.");

            commandsListBase.SetServiceProvider(_serviceProvider);
        }

        _commandLists.Add(commandList);

        foreach (var command in commandList.GetCommands())
        {
            RegisterCommand(command);
        }
    }

    private async void OnKeyPressed(object? sender, KeyboardActionEventArgs e)
    {
        if (e.Key is VirtualKeys.Control
            or VirtualKeys.LeftControl
            or VirtualKeys.RightControl
            or VirtualKeys.Menu
            or VirtualKeys.LeftMenu
            or VirtualKeys.RightMenu)
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

    public Pix2dCommand RegisterAsyncCommand(string name, Func<Task> commandActionTask, string description, CommandShortcut? defaultShortcut, EditContextType? editContextType = null, ICommandBehaviour? behaviour = null)
    {
        var cmd = new Pix2dAsyncCommand(name, description, defaultShortcut, editContextType, commandActionTask);
        behaviour?.Attach(cmd);
        if (cmd.DefaultShortcut != null)
            cmd.DefaultShortcut.KeyConverter = KeyToString;
        RegisterCommand(cmd);
        return cmd;
    }

    public Pix2dCommand RegisterSyncCommand(string name, Action commandAction, string description, CommandShortcut? defaultShortcut, EditContextType? editContextType = null, ICommandBehaviour? behaviour = null)
    {
        var cmd = new Pix2dSyncCommand(name, description, defaultShortcut, editContextType, commandAction);
        behaviour?.Attach(cmd);
        if (cmd.DefaultShortcut != null)
            cmd.DefaultShortcut.KeyConverter = KeyToString;
        RegisterCommand(cmd);
        return cmd;
    }

    private string KeyToString(VirtualKeys key) => _platformStuffService.KeyToString(key);

    public void RegisterCommand(Pix2dCommand cmd)
    {
        cmd.DirectExecuteAction = name => _ = Task.FromResult(ExecuteCommandAsync(name));
        _commands[cmd.Name] = cmd;
    }

    public async Task ExecuteCommandAsync(string name)
    {
        if (!_commands.TryGetValue(name, out var command))
            throw new Exception($"Command {name} not found");

        if (!command.CanExecute()) return;

        SessionLogger.OpLogCommand(command.Name);
        if (command is Pix2dAsyncCommand asyncCmd)
        {
            await asyncCmd.CommandActionTask();
        }
        else if (command is Pix2dSyncCommand syncCmd)
        {
            syncCmd.CommandAction();
        }
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

    IEnumerable<Pix2dCommand> ICommandService.GetCommands()
    {
        return _commands.Values;
    }
}