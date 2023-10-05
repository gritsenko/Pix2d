using System;
using System.Threading.Tasks;
using Pix2d.Abstract.UI;

namespace Pix2d.Common;

public class DefaultBusyController : IBusyController
{
    public AppState AppState { get; }
    public IDialogService DialogService { get; }

    public bool IsBusy => AppState.IsBusy;

    public DefaultBusyController(AppState appState, IDialogService dialogService)
    {
        AppState = appState;
        DialogService = dialogService;
    }

    public async Task<bool> RunLongTaskAsync(Func<Task> task)
    {
        AppState.IsBusy = true;
        var result = false;
        try
        {
            await task.Invoke();
            result = true;
        }
        catch (Exception e)
        {
            Logger.LogException(e);
            ShowAlert("Can't finish task: " + e.Message);
        }
        finally
        {
            await Task.Delay(300);//hack: web assembly needs delay, otherwise it always show busy on initialization
            AppState.IsBusy = false;
        }
        return result;
    }

    private async void ShowAlert(string message)
    {
        await DialogService.ShowAlert(message, "Warning!");
    }
}