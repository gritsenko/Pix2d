using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pix2d.Infrastructure.GA.Service.Request;

public class Event : IEvent
{
    [JsonPropertyName("name")] 
    public string Name { get; set; }
    
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>  EventParameters { get; set; }
    
    public Event(string name)
    {
        Name = name;
        EventParameters = new Dictionary<string, object>();
    }
}