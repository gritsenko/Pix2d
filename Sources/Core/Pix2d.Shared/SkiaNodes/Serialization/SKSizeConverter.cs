using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public partial class SKSizeConverter : JsonConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {

        if (reader.Value is long val)
        {
            return new SKSize(val, val);
        }

        if (reader.TokenType == JsonToken.StartObject)
        {
            var jobject = serializer.Deserialize<JObject>(reader);
            var size = (SKSize) (existingValue ?? serializer.ContractResolver.ResolveContract(objectType).DefaultCreator());
            
            if (jobject.TryGetValue("height", StringComparison.InvariantCultureIgnoreCase, out var height) && (height.Type == JTokenType.Float || height.Type == JTokenType.Integer))
            {
                size.Height = (float)height.Value<double>();
            }
            
            if (jobject.TryGetValue("width", StringComparison.InvariantCultureIgnoreCase, out var width) && (width.Type == JTokenType.Float || width.Type == JTokenType.Integer))
            {
                size.Width = (float)width.Value<double>();
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