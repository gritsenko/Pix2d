using System.Net.Http;
using System.Threading.Tasks;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.Service.Client;

public interface IBasicHttpClient
{
    /// <summary>
    /// Send a Post request.
    /// </summary>
    /// <param name="path">Lets us send if its debug or collection end point.</param>
    /// <param name="data">The event to send.</param>
    /// <returns></returns>
    Task<HttpResponseMessage> PostAsync(string path, IEventRequest data);
    
}