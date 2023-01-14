using System;
using Newtonsoft.Json;
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
        else if (reader.TokenType == JsonToken.StartObject)
        {
            existingValue = existingValue ?? serializer.ContractResolver.ResolveContract(objectType).DefaultCreator();
            serializer.Populate(reader, existingValue);
            return existingValue;
        }
        else if (reader.TokenType == JsonToken.Null)
        {
            return null;
        }
        else
        {
            throw new JsonSerializationException();
        }

        //reader.Read();
        //var value = reader.ReadAsString();
        //reader.Read();

        //var val = float.TryParse(value, out var f);
        //{
        //    return new SKSize(f, f);
        //}
        //return new SKSize(8, 8);
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