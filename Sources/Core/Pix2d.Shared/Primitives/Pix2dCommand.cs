using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using CommonServiceLocator;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.State;
using SkiaNodes.Interactive;

namespace Pix2d.Primitives;

public class Pix2dSyncCommand : Pix2dCommand
{
    public Action CommandAction { get; }
    public Pix2dSyncCommand(string name,  string description, CommandShortcut? defaultShortcut, EditContextType? editContextType, Action commandAction) 
        : base(name, description, defaultShortcut, editContextType)
    {
        CommandAction = commandAction;
    }
}

public class Pix2dAsyncCommand : Pix2dCommand
{
    public Func<Task> CommandActionTask { get; }
    public Pix2dAsyncCommand(string name,  string description, CommandShortcut? defaultShortcut, EditContextType? editContextType, Func<Task> commandActionTask) 
        : base(name, description, defaultShortcut, editContextType)
    {
        CommandActionTask = commandActionTask;
    }
}

public interface ICommandBehaviour
{
    void Attach(Pix2dCommand command);
}

public class DisableOnAnimation : ICommandBehaviour
{
    public static DisableOnAnimation Instance = new DisableOnAnimation();

    private List<Pix2dCommand> _commands = new();
    private readonly ProjectState _state;

    private DisableOnAnimation()
    {
        _state = ServiceLocator.Current.GetInstance<AppState>().CurrentProject;
        _state.WatchFor(x => x.IsAnimationPlaying, OnAnimationPlayingChanged);
    }

    private void OnAnimationPlayingChanged()
    {
        var canExecute = !_state.IsAnimationPlaying;
        foreach (var command in _commands)
        {
            command.SetCanExecute(canExecute);
        }
    }

    public void Attach(Pix2dCommand command)
    {
        _commands.Add(command);
    }
}

public abstract class Pix2dCommand : ICommand
{
    private bool _canExecute = true;
    public string Name { get; }
    public string Description { get; }
    public CommandShortcut? DefaultShortcut { get; }
    public EditContextType? EditContextType { get; }

    public string[] Groups { get; set; }

    public event EventHandler? CanExecuteChanged;

    protected Pix2dCommand(string name, string description, CommandShortcut? defaultShortcut, EditContextType? editContextType)
    {
        Name = name;
        Description = description;
        DefaultShortcut = defaultShortcut;
        EditContextType = editContextType;

        //SetGroupFromName;
        var parts = name.Split('.');
        Groups = parts;
    }

    public bool CheckShortcut(VirtualKeys key, KeyModifier modifiers, EditContextType editContextType)
    {
        if (DefaultShortcut?.Key == key && DefaultShortcut?.KeyModifiers == modifiers)
        {
            if(!EditContextType.HasValue || EditContextType.Value == Abstract.EditContextType.All || EditContextType.Value == editContextType)
                return true;
        }
        return false;
    }

    public override string ToString()
    {
        return Name;
    }

    public string GetShortcutString()
    {
        return $"{DefaultShortcut}";
    }

    public string Tooltip
    {
        get
        {
            var shortcutString = GetShortcutString();
            return $"{Description}{(shortcutString == string.Empty ? "" : $" [{shortcutString}]")}";
        }
    }

    public bool CanExecute(object? parameter = default)
    {
        return _canExecute;
    }

    public void SetCanExecute(bool canExecute)
    {
        if (canExecute == _canExecute) return;
        
        _canExecute = canExecute;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Execute(object? parameter = default)
    {
        ServiceLocator.Current.GetInstance<ICommandService>().ExecuteCommandAsync(Name);
    }
}