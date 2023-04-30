using Pix2d.Abstract.Commands;
using Pix2d.Primitives;

namespace Pix2d.Command;

public class WindowCommands : CommandsListBase {
    protected override string BaseName => "Window";

    public Pix2dCommand ToggleAlwaysOnTop => GetCommand(() => GetService<IPlatformStuffService>().ToggleTopmostWindow());
    
    public Pix2dCommand RateAppCommand => GetCommand(async () =>
    {
        var result = await CoreServices.LicenseService.RateApp();
        if (result)
        {
            AppState.UiState.ShowRatePrompt = false;

            CoreServices.SettingsService.Set("IsAppReviewed", true);
            
            GetService<ReviewService>().LogReview("Updated");
        }
        else
        {
            GetService<ReviewService>().LogReview("Not updated");
        }
    });
    public Pix2dCommand CloseRatePromptCommand => GetCommand(() =>
    {
        AppState.UiState.ShowRatePrompt = false;
        GetService<ReviewService>().LogReview("Prompt closed");
        GetService<ReviewService>().DefferNextReviewPrompt();
    });
}