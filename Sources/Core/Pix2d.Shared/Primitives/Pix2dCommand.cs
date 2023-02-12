using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommonServiceLocator;
using Pix2d.Abstract;
using SkiaNodes.Interactive;

namespace Pix2d.Primitives;

public class Pix2dSyncCommand : Pix2dCommand
{
    public Action CommandAction { get; }
    public Pix2dSyncCommand(string name,  string description, CommandShortcut defaultShortcut, EditContextType? editContextType, Action commandAction) 
        : base(name, description, defaultShortcut, editContextType)
    {
        CommandAction = commandAction;
    }
}

public class Pix2dAsyncCommand : Pix2dCommand
{
    public Func<Task> CommandActionTask { get; }
    public Pix2dAsyncCommand(string name,  string description, CommandShortcut defaultShortcut, EditContextType? editContextType, Func<Task> commandActionTask) 
        : base(name, description, defaultShortcut, editContextType)
    {
        CommandActionTask = commandActionTask;
    }
}

public abstract class Pix2dCommand : ICommand
{
    public string Name { get; }
    public string Description { get; }
    public CommandShortcut DefaultShortcut { get; }
    public EditContextType? EditContextType { get; }

    public string[] Groups { get; set; }
    
    protected Pix2dCommand(string name, string description, CommandShortcut defaultShortcut, EditContextType? editContextType)
    {
        Name = name;
        Description = description;
        DefaultShortcut = defaultShortcut;
        EditContextType = editContextType;

        SetGroupFromName(name);
    }

    public event EventHandler CanExecuteChanged;

    private void SetGroupFromName(string name)
    {
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
        return DefaultShortcut?.ToString();
    }

    public string Tooltip => Description + " [" + GetShortcutString() + "]"; 

    public bool CanExecute(object parameter)
    {
        return true;
    }

    public void Execute(object parameter)
    {
        ServiceLocator.Current.GetInstance<ICommandService>().ExecuteCommandAsync(Name);
    }
}