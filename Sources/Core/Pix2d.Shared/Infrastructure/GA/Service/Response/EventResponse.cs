using System.Net.Http;

namespace Pix2d.Infrastructure.GA.Service.Response;

public class EventResponse
{
    /// <summary>
    /// The response from the http request. should always be true
    /// </summary>
    public bool IsSuccess { get; set; }
    
    // The http response message from the request.
    public HttpResponseMessage Message { get; set; }
    
    /// <summary>
    /// The debug response from a validation end point call.
    ///
    /// Null if this was sent to the collection endpoint. 
    /// </summary>
    public DebugResponse? DebugResponse { get; set; }

    
    /// <summary>
    /// Requests sent to the collection endpoint are always assumed ok. Google doesnt return errors.
    ///
    /// if the call was sent to the debug endpoint and there are no validation errors then its valid.
    /// </summary>
    /// <returns></returns>
    public bool IsRequestValid()
    {
        if (DebugResponse == null) return true;
        var hold =  DebugResponse.ValidationMessages.Count == 0;

        return hold;
    }
}