using System;
using System.Collections.Generic;
using SkiaSharp;
using Newtonsoft.Json;
using SkiaNodes.Serialization;

class P {
    static void Main() {
        var images = new Dictionary<string, SKBitmap>();
        var bm = new SKBitmap(10, 10);
        images["testkey.png"] = bm;
        
        var json = JsonConvert.SerializeObject(bm, new JsonSerializerSettings() { 
            TypeNameHandling = TypeNameHandling.All, 
            Formatting = Formatting.Indented,
            Converters = { new SKBitmapConverter() { DataStorage = new SimpleDictionaryStorage(images) } } 
        });
        Console.WriteLine("JSON Output:");
        Console.WriteLine(json);
        
        var result = JsonConvert.DeserializeObject<SKBitmap>(json, new JsonSerializerSettings() { 
            TypeNameHandling = TypeNameHandling.All, 
            Converters = { new SKBitmapConverter() { DataStorage = new SimpleDictionaryStorage(images) } } 
        });
        Console.WriteLine("Restored: " + (result != null));
    }
}
