using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Pix2d.Infrastructure.GA.Extensions;
using Pix2d.Infrastructure.GA.Service.Client;
using Pix2d.Infrastructure.GA.Service.Response;

namespace Pix2d.Infrastructure.GA.Service.Request;


public class EventRequest : IEventRequest
{
    public IBasicHttpClient Client { get; set; }
    
    [JsonPropertyName("client_id")] 
    public string ClientId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("user_id")] 
    public string UserId { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [JsonPropertyName("timestamp_micros")] 
    public double TimestampMicros { get; set; }

    [JsonPropertyName("non_personalized_ads")]
    public bool NonPersonalizedAds { get; set; }

    [JsonPropertyName("events")] 
    public List<IEvent> Events { get; set; }

    [JsonPropertyName("user_properties")]
    public Dictionary<string,object> UserProperties { get; set; }
    
    
    public EventRequest(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentNullException(nameof(clientId));
        
        var timespanSinceEpoch = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        TimestampMicros = Math.Truncate(timespanSinceEpoch.TotalMicroseconds());
        ClientId = clientId;
    }
    public EventRequest(string clientId, string userId) : this(clientId)
    {
        UserId = userId;
    }
    public EventRequest(string clientId, string userId, bool nonPersonalizedAds = false) : this(clientId, userId)
    {
        NonPersonalizedAds = nonPersonalizedAds;
    }
    public async Task<EventResponse> Send(bool debug = false)
    {
        var path = "/mp/collect";
        
        if (debug) path = "/debug/mp/collect";
        
        var response = await Client.PostAsync(path, this);
        var responseData = await response.Content.ReadAsStringAsync();

        return new EventResponse
        {
            IsSuccess = response.IsSuccessStatusCode,
            Message = response,
            DebugResponse = debug ? JsonSerializer.Deserialize<DebugResponse>(responseData) : null
        };
    }

    public void SetClient(IBasicHttpClient client)
    {
        Client = client;
    }
}