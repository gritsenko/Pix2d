using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public partial class SKSizeConverter : JsonConverter
{
    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {

        if (reader.Value is long val)
        {
            return new SKSize(val, val);
        }

        if (reader.TokenType == JsonToken.StartObject)
        {
            var jobject = serializer.Deserialize<JObject>(reader);
            if (jobject == null)
                return null;

            SKSize size;
            if (existingValue == null)
            {
                size = new SKSize();
            }
            else
            {
                size = (SKSize)existingValue;
            }

            if (jobject.TryGetValue("height", StringComparison.InvariantCultureIgnoreCase, out var height) && height != null && (height.Type == JTokenType.Float || height.Type == JTokenType.Integer))
            {
                var hVal = height.Value<double>();
                if (hVal != null)
                    size.Height = (float)hVal;
            }

            if (jobject.TryGetValue("width", StringComparison.InvariantCultureIgnoreCase, out var width) && width != null && (width.Type == JTokenType.Float || width.Type == JTokenType.Integer))
            {
                var wVal = width.Value<double>();
                if (wVal != null)
                    size.Width = (float)wVal;
            }

            return size;
        }

        if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }

        throw new JsonSerializationException();
    }

    public override bool CanConvert(Type objectType)
    {
        return objectType == typeof(SKSize);
    }

    public override bool CanWrite { get; } = false;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
