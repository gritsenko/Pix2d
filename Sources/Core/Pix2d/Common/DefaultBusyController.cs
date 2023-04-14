using System;
using System.Threading.Tasks;
using Pix2d.Abstract.Platform;
using Pix2d.Abstract.State;
using Pix2d.Abstract.UI;

namespace Pix2d.Common;

public class DefaultBusyController : IBusyController
{
    public IAppState AppState { get; }
    public IDialogService DialogService { get; }

    public bool IsBusy => AppState.IsBusy;

    public DefaultBusyController(IAppState appState, IDialogService dialogService)
    {
        AppState = appState;
        DialogService = dialogService;
    }

    public async Task<bool> RunLongTaskAsync(Func<Task> task)
    {
        AppState.SetAsync(s => s.IsBusy, true);
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
            await Task.Delay(300);
            AppState.SetAsync(s => s.IsBusy, false);
        }
        return result;
    }

    private async void ShowAlert(string message)
    {
        await DialogService.ShowAlert(message, "Warning!");
    }
}