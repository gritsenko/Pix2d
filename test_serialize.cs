using System;
using System.IO;
using System.Collections.Generic;
using SkiaSharp;
using Newtonsoft.Json;
using SkiaNodes.Serialization;

class P {
    static void Main() {
        var images = new Dictionary<string, SKBitmap>();
        var bm = new SKBitmap(10, 10);
        images["testkey.png"] = bm;
        var r = new SKBitmapRef(bm, new SimpleDictionaryStorage(images));
        
        var json = JsonConvert.SerializeObject(r, Formatting.Indented);
        Console.WriteLine("JSON: " + json);
    }
}
