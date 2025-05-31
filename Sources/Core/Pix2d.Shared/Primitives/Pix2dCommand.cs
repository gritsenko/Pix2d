#nullable enable
using System.Windows.Input;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using SkiaNodes.Interactive;

namespace Pix2d.Primitives;

public class Pix2dSyncCommand(
    string name,
    string description,
    CommandShortcut? defaultShortcut,
    EditContextType? editContextType,
    Action commandAction)
    : Pix2dCommand(name, description, defaultShortcut, editContextType)
{
    public Action CommandAction { get; } = commandAction;
}

public class Pix2dAsyncCommand(
    string name,
    string description,
    CommandShortcut? defaultShortcut,
    EditContextType? editContextType,
    Func<Task> commandActionTask)
    : Pix2dCommand(name, description, defaultShortcut, editContextType)
{
    public Func<Task> CommandActionTask { get; } = commandActionTask;
}

public interface ICommandBehaviour
{
    void Attach(Pix2dCommand command);
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
    public Action<string>? DirectExecuteAction { get; set; }

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

    public bool CanExecute(object? parameter = null)
    {
        return _canExecute;
    }

    public void SetCanExecute(bool canExecute)
    {
        if (canExecute == _canExecute) return;
        
        _canExecute = canExecute;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Execute(object? parameter = null) => DirectExecuteAction?.Invoke(Name);
}