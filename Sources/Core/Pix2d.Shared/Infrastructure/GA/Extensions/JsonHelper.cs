using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pix2d.Infrastructure.GA.Extensions;

public static class JsonHelper
{
    public static JsonSerializerOptions GetOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };
        
    }
}