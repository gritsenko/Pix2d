using System.Collections.Generic;
using Newtonsoft.Json;

namespace Pix2d.Exporters.AtlasPacking;

public class SpriteSheetFrame
{
    public Rect frame { get; set; }
    public bool rotated { get; set; }
    public bool trimmed { get; set; }
    public Rect spriteSourceSize { get; set; }
    public Sourcesize sourceSize { get; set; }
    public Anchor anchor { get; set; }
}

public class Rect
{
    public int x { get; set; }
    public int y { get; set; }
    public int w { get; set; }
    public int h { get; set; }
}

public class Sourcesize
{
    public int w { get; set; }
    public int h { get; set; }
}

public class Anchor
{
    public int x { get; set; }
    public int y { get; set; }
}


public class SpriteSheetMeta

{
    //"app": "https://www.codeandweb.com/texturepacker",
    public string app { get; set; } = "http://pix2d.com";
    //"version": "1.0",
    public string version { get; set; }
    //"image": "spritesheet.png",
    public string image { get; set; }
    //"format": "RGBA8888",
    public string format { get; set; } = "RGBA8888";
    //"size": {"w":2033,"h":2030},
    public Sourcesize size { get; set; }
    //"scale": "1",
    public string scale { get; set; } = "1";
}

public class SpriteSheet
{
    [JsonProperty("frames")]
    public Dictionary<string, SpriteSheetFrame> Frames { get; set; } = new Dictionary<string, SpriteSheetFrame>();

    [JsonProperty("animations")]
    public Dictionary<string, string[]> Animations { get; set; } = new Dictionary<string, string[]>();

    [JsonProperty("meta")]
    public SpriteSheetMeta Meta { get; set; }
}