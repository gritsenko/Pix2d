using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Pix2d.Infrastructure.GA.Service.Request;

public interface IEvent
{ 
    [JsonPropertyName("name")] 
    string Name { get; set; }
    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Dictionary<string, object>  EventParameters { get; set; }
}