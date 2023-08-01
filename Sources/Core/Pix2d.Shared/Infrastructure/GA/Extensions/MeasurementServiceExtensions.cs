using Pix2d.Infrastructure.GA.Service;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.Extensions;

public static class MeasurementServiceExtensions
{
    public static IEventRequest CreateEventRequest(this IMeasurementService requestService, IEventRequest request)
    {
        request.SetClient(requestService.GetClient());
        return request;
    }
}