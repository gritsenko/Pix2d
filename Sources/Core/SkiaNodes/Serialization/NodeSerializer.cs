using System.Reflection;
using Newtonsoft.Json;
using SkiaSharp;

namespace SkiaNodes.Serialization;

public class NodeSerializer : IDisposable
{
    private SimpleDictionaryStorage _dataStorage = new();
    private static TypeNameAssemblyExcludingSerializationBinder? _assemblyBinderInstance;
    public static Assembly[] ExtraAssemblies { get; set; } = [];

    public string Serialize(SKNode node)
    {
        JsonConverter[] converters =
        [
            new SKBitmapConverter() {DataStorage = _dataStorage},
            new SKColorConverter(),
            new NodeCollectionConverter()
        ];
        var json = JsonConvert.SerializeObject(node, GetJsonSettings(converters));
        return json;
    }

    public IDictionary<string, SKBitmap> GetDataEntries()
    {
        return _dataStorage.GetDataEntries();
    }

    private static JsonSerializerSettings GetJsonSettings(params JsonConverter[] converters)
    {
        _assemblyBinderInstance ??= new TypeNameAssemblyExcludingSerializationBinder(ExtraAssemblies);
        var settings = new JsonSerializerSettings()
        {
            ContractResolver = new WriteOnlyPropertiesContractResolver(),
            Converters = new List<JsonConverter>(converters),
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = Formatting.Indented,
            SerializationBinder = _assemblyBinderInstance
        };
        settings.Error = (sender, args) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(args.CurrentObject?.ToString() ?? "null");
            Console.WriteLine(args.ErrorContext.Path);
            Console.WriteLine($"ex: {args.ErrorContext.Error.Message}");
            Console.WriteLine($"ex: {args.ErrorContext.Error.StackTrace}");
            Console.WriteLine($"oo:{args.ErrorContext.OriginalObject}, {args.ErrorContext.Member}");

            Console.ForegroundColor = ConsoleColor.Gray;
        };
        return settings;
    }

    public void Dispose()
    {
    }

    public static T Deserialize<T>(string json, IDictionary<string, SKBitmap> images)
    {
        try
        {
            JsonConverter[] converters =
            [
                new SKBitmapConverter() { DataStorage = new SimpleDictionaryStorage(images) },
                new SKColorConverter(),
                new SKSizeConverter()
                //new NodeCollectionConverter()
            ];

            var node = JsonConvert.DeserializeObject<T>(json, GetJsonSettings(converters));
            return (T)node;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to deserialize node of type {typeof(T).Name} (exception: {ex.Message}) from JSON: {json}", ex);
        }
    }

    public static SKNode Deserialize(Type type, string json, IDictionary<string, SKBitmap> images)
    {
        JsonConverter[] converters =
        [
            new SKBitmapConverter() {DataStorage = new SimpleDictionaryStorage(images)},
            new SKColorConverter()
            //new SKSizeConverter(),
            //new NodeCollectionConverter()
        ];
        var node = JsonConvert.DeserializeObject(json, type, GetJsonSettings(converters));
        return (SKNode)node;
    }
}