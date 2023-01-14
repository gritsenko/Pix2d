using System;
using System.Windows.Input;
using CommonServiceLocator;
using Pix2d.Abstract;
using Pix2d.Primitives;

namespace Pix2d.Mvvm;

public class Pix2dCommandMapping : ICommand
{
    public event EventHandler CanExecuteChanged;

    private readonly string _commandName;
    private readonly Action _afterExecuteAction;

    public Pix2dCommandMapping(Pix2dCommand pix2dCommand, Action afterExecuteAction = null)
    {
        _commandName = pix2dCommand.Name;
        _afterExecuteAction = afterExecuteAction;
    }

    public bool CanExecute(object parameter)
    {
        return true;
    }

    public async void Execute(object parameter)
    {
        var commandService = ServiceLocator.Current.GetInstance<ICommandService>();
        await commandService?.ExecuteCommandAsync(_commandName);

        _afterExecuteAction?.Invoke();
    }

    protected virtual void OnCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}