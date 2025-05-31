using SkiaNodes.Interactive;
using SkiaSharp;

namespace SkiaNodes;

public class SKApp
{
    public const SKColorType ColorType = SKColorType.Rgba8888;

    public static bool DebugMode { get; set; }

    public static SceneManager SceneManager => SceneManager.Current;

    public static SKInput InputManager => SKInput.Current;
}