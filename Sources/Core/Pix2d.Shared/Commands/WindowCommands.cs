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
        // Dismiss the banner *before* awaiting: RateApp() blocks on a platform rate dialog (Google Play /
        // MS Store) that can take seconds to appear, and a button left live in the meantime invited the
        // second tap that double-logged the funnel's "Accepted" event.
        AppState.UiState.ShowRatePrompt = false;

        // Tolerate a head with no IReviewService registered — the prompt can't normally show there, but
        // never crash the command if it somehow does. Persisting "reviewed" belongs to RateApp() itself,
        // so it can't be forgotten by another entry point (and isn't written when there's no funnel at all).
        var reviewService = ServiceProvider.GetService<IReviewService>();
        if (reviewService != null)
            await reviewService.RateApp();
    });

    public Pix2dCommand CloseRatePromptCommand => GetCommand(() =>
    {
        AppState.UiState.ShowRatePrompt = false;
        ServiceProvider.GetService<IReviewService>()?.DefferNextReviewPrompt();
    });
}