using System.Text.Json.Serialization;

namespace Pix2d.Infrastructure.GA.Service.Request;

public class ValidationMessage
{
    [JsonPropertyName("fieldPath")]
    public string FieldPath { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("validationCode")]
    public string ValidationCode { get; set; }
}