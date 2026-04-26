#nullable enable
using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using Pix2d.UI.Dialogs;

namespace Pix2d.Command;

public class CrashCommands : CommandsListBase
{
    protected override string BaseName => "Crash";

    public Pix2dCommand ShowCrashReport
        => GetCommand(ShowCrashReportImpl, "Show last crash report", null, EditContextType.All);

    private void ShowCrashReportImpl()
    {
        var crashService = ServiceProvider.GetService<ICrashReportService>();
        var dialogService = ServiceProvider.GetService<IDialogService>();
        var platformService = ServiceProvider.GetService<IPlatformStuffService>();
        if (crashService == null || dialogService == null || platformService == null)
            return;

        // Always allow manual reopen — even if there's no pending report we'll show whatever's saved.
        var dialog = new CrashReportDialogView(crashService, platformService, autoShown: false);
        _ = dialogService.ShowDialogAsync(dialog);
    }
}
