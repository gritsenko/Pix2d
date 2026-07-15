using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;

namespace Pix2d.Command;

public class WindowCommands : CommandsListBase
{
    protected override string BaseName => "Window";

    public Pix2dCommand ToggleAlwaysOnTop =>
        GetCommand(() => ServiceProvider.GetRequiredService<IPlatformStuffService>().ToggleTopmostWindow());

    public Pix2dCommand RateAppCommand => GetCommand(async () =>
    {
        // Tolerate a head with no IReviewService registered — the prompt can't normally show there, but
        // never crash the command if it somehow does.
        var reviewService = ServiceProvider.GetService<IReviewService>();
        if (reviewService != null)
            await reviewService.RateApp();

        AppState.UiState.ShowRatePrompt = false;
        ServiceProvider.GetRequiredService<ISettingsService>().Set("IsAppReviewed", true);
    });

    public Pix2dCommand CloseRatePromptCommand => GetCommand(() =>
    {
        AppState.UiState.ShowRatePrompt = false;
        ServiceProvider.GetService<IReviewService>()?.DefferNextReviewPrompt();
    });
}