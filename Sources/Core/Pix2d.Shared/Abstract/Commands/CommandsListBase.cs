using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CommonServiceLocator;
using Pix2d.Abstract.State;
using Pix2d.Primitives;

namespace Pix2d.Abstract.Commands;

public abstract class CommandsListBase : ICommandList
{
    protected abstract string BaseName { get; }

    protected static IAppState AppState => ServiceLocator.Current.GetInstance<IAppState>();

    private ICommandService CommandService => ServiceLocator.Current.GetInstance<ICommandService>();
    protected string GetKey([CallerMemberName] string commandName = null)
    {
        return BaseName + "." + commandName;
    }

    protected Pix2dCommand GetCommand(Action action, EditContextType contextType = EditContextType.All, CommandShortcut shortcut = default, [CallerMemberName] string commandName = null)
        => GetCommand(commandName, shortcut, contextType, action, commandName);

    protected Pix2dCommand GetCommand(string description, CommandShortcut shortcut, EditContextType contextType, Action action, [CallerMemberName] string commandName = null)
    {
        var key = GetKey(commandName);
        if (CommandService.TryGetCommand(key, out var command))
        {
            return command;
        }

        return CommandService.RegisterSyncCommand(key, action, description, shortcut, contextType);
    }

    protected Pix2dCommand GetCommand(string description, CommandShortcut shortcut, EditContextType contextType, Func<Task> actionTask, [CallerMemberName] string commandName = null)
    {
        var key = GetKey(commandName);
        if (CommandService.TryGetCommand(key, out var command))
        {
            return command;
        }

        return CommandService.RegisterAsyncCommand(key, actionTask, description, shortcut, contextType);
    }

    public IEnumerable<Pix2dCommand> GetCommands()
    {
        var props = GetType().GetProperties().Where(x => x.PropertyType == typeof(Pix2dCommand)).ToArray();

        foreach (var propertyInfo in props)
        {
            yield return propertyInfo.GetValue(this, new object[0]) as Pix2dCommand;
        }
    }
}