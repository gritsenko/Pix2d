using Pix2d.Infrastructure.GA.Service.Client;

namespace Pix2d.Infrastructure.GA.Service;

public interface IMeasurementService
{
    IBasicHttpClient GetClient();
}