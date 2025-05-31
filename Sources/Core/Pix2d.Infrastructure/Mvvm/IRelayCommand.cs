using System.Windows.Input;

namespace Mvvm;

public interface IRelayCommand : ICommand
{
    void RaiseCanExecuteChanged();
}