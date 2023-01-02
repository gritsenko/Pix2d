using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SkiaSharp;

namespace SkiaNodes.Serialization
{
    public class NodeSerializer : IDisposable
    {
        SimpleDictionaryStorage dataStorage = new SimpleDictionaryStorage();

        public string Serialize(SKNode node)
        {
            JsonConverter[] converters =
            {
                new SKBitmapConverter() {DataStorage = dataStorage},
                new SKColorConverter(),
                new NodeCollectionConverter()
            };
            var json = JsonConvert.SerializeObject(node, GetJsonSettings(converters));
            return json;
        }

        public IDictionary<string, SKBitmap> GetDataEntries()
        {
            return dataStorage.GetDataEnries();
        }

        private static JsonSerializerSettings GetJsonSettings(params JsonConverter[] converters)
        {
            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new WriteOnlyPropertiesContractResolver(),
                Converters = new List<JsonConverter>(converters),
                NullValueHandling = NullValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.Indented
            };

            return settings;
        }

        public void Dispose()
        {
            dataStorage = null;
        }

        public static T Deserialize<T>(string json, IDictionary<string, SKBitmap> images)
        {
            JsonConverter[] converters =
            {
                new SKBitmapConverter() {DataStorage = new SimpleDictionaryStorage(images)},
                new SKColorConverter(),
                new SKSizeConverter(),
                //new NodeCollectionConverter()
            };
            
            var node = JsonConvert.DeserializeObject<T>(json, GetJsonSettings(converters));
            return (T) node;
        }
        public static SKNode Deserialize(Type type, string json, IDictionary<string, SKBitmap> images)
        {
            JsonConverter[] converters =
            {
                new SKBitmapConverter() {DataStorage = new SimpleDictionaryStorage(images)},
                new SKColorConverter()
                //new SKSizeConverter(),
                //new NodeCollectionConverter()
            };
            var node = JsonConvert.DeserializeObject(json, type, GetJsonSettings(converters));
            return (SKNode)node;
        }
    }
}