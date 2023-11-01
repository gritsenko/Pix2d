using Avalonia.Threading;
using Pix2d.Core.Tests.Mocks;
using Pix2d.Services;
using Pix2d.State;

namespace Pix2d.Core.Tests.Services;

public class GumroadLicenseServiceTests
{
    [Fact]
    public async Task VerifyLicense_HackingCodes_Rejected()
    {
        var state = new AppState();
        var dialogService = new TestDialogService();
        var service = new GumroadLicenseService(state, dialogService, new TestBusyController(),
            new TestSettingsService(), new TestPlatformStaffService());
        

        dialogService.SetInput("&redirect=http://hacker.com");
        Assert.False(await service.BuyPro());
    }
}