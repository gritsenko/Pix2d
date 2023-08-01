using System.Collections.Generic;
using System.Threading.Tasks;
using Pix2d.Infrastructure.GA.Service.Request;
using Pix2d.Infrastructure.GA.Service.Response;

namespace Pix2d.Infrastructure.GA.Extensions;

public static class EventRequestExtensions
{
    public static void AddEvent(this IEventRequest request, IEvent ga4Event)
    {
        if (request.Events == null)
        {
            request.Events = new List<IEvent>();
            request.Events.Add(ga4Event);
            return;
        }

        for (var i = 0; i < 20; i++)
        {
            if (string.IsNullOrWhiteSpace(request.Events[i].Name))
            {
                request.Events[i] = ga4Event;
            }
        }
        
    }

    public static async Task<EventResponse> Execute(this IEventRequest requestService, bool debug = false)
    {
        return await requestService.Send(debug);
    }
}