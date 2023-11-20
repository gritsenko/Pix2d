using CommonServiceLocator;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;

namespace Pix2d.Command;

public class WindowCommands : CommandsListBase {
    protected override string BaseName => "Window";

    public Pix2dCommand ToggleAlwaysOnTop => GetCommand(() => GetService<IPlatformStuffService>().ToggleTopmostWindow());
    
    public Pix2dCommand RateAppCommand => GetCommand(async () =>
    {
        var result = await ServiceLocator.Current.GetInstance<IReviewService>().RateApp();
        if (result)
        {
            AppState.UiState.ShowRatePrompt = false;

            CoreServices.SettingsService.Set("IsAppReviewed", true);
            
            GetService<IReviewService>().LogReview("Updated");
        }
        else
        {
            GetService<IReviewService>().LogReview("Not updated");
        }
    });
    public Pix2dCommand CloseRatePromptCommand => GetCommand(() =>
    {
        AppState.UiState.ShowRatePrompt = false;
        GetService<IReviewService>().LogReview("Prompt closed");
        GetService<IReviewService>().DefferNextReviewPrompt();
    });
}