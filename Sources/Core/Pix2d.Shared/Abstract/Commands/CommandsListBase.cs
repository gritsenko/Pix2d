#nullable enable
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using Pix2d.State;

namespace Pix2d.Abstract.Commands;

public abstract class CommandsListBase : ICommandList
{
    protected abstract string BaseName { get; }

    private ICommandService _commandService = null!;//injected from CommandService

    protected IServiceProvider ServiceProvider { get; set; } = null!; //injected from CommandService
    public void SetServiceProvider(IServiceProvider serviceProvider) => ServiceProvider = serviceProvider;
    public void SetCommandService(ICommandService commandService) => _commandService = commandService;

    protected AppState AppState => ServiceProvider!.GetRequiredService<AppState>();

    protected string GetKey([CallerMemberName] string? commandName = null) => BaseName + "." + commandName;

    protected Pix2dCommand GetCommand(
        Action action,
        string? description = null,
        CommandShortcut? shortcut = null,
        EditContextType contextType = EditContextType.All,
        [CallerMemberName] string? commandName = null,
        ICommandBehaviour? behaviour = null)
    {
        var key = GetKey(commandName);
        if (_commandService.TryGetCommand(key, out var command))
            return command;

        return _commandService.RegisterSyncCommand(key, action, description ?? "No description", shortcut, contextType, behaviour);
    }

    protected Pix2dCommand GetCommand(
        Func<Task> asyncAction,
        string? description = null,
        CommandShortcut? shortcut = null,
        EditContextType contextType = EditContextType.All,
        [CallerMemberName] string? commandName = null,
        ICommandBehaviour? behaviour = null)
    {
        var key = GetKey(commandName);
        if (_commandService.TryGetCommand(key, out var command))
            return command;

        return _commandService.RegisterAsyncCommand(key, asyncAction, description ?? "No description", shortcut, contextType, behaviour);
    }

    public IEnumerable<Pix2dCommand> GetCommands()
    {
        var props = GetType()
            .GetProperties()
            .Where(x => x.PropertyType == typeof(Pix2dCommand)).ToArray();

        foreach (var propertyInfo in props)
            yield return (propertyInfo.GetValue(this, []) as Pix2dCommand)!;
    }
}