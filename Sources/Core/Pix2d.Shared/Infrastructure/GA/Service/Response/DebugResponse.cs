using System.Collections.Generic;
using System.Text.Json.Serialization;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.Service.Response;

public class DebugResponse
{
    [JsonPropertyName("validationMessages")]
    public List<ValidationMessage> ValidationMessages { get; set; }
}