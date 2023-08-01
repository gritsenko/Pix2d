using Pix2d.Infrastructure.GA.Service.Client;

namespace Pix2d.Infrastructure.GA.Service;

public class MeasurementService : IMeasurementService
{
    private readonly IBasicHttpClient _client;
    public IBasicHttpClient GetClient()
    {
        return _client;
    }
    public MeasurementService(IBasicHttpClient client)
    {
        _client = client;
    }
}