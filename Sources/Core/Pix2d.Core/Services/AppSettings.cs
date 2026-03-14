using Pix2d.Primitives;

namespace Pix2d.Services;

public class AppSettings
{
    public object? Session { get; set; }
    public List<MruRecord>? Mru { get; set; }
    public string? Locale { get; set; }
    public bool ShowLayers { get; set; } = true;
    public string? CustomPalette { get; set; }
    public double UiScale { get; set; } = 1.0;
    public int MouseWheelBehavior { get; set; } = 1;
    public bool IsTwoFingerDoubleTapUndoEnabled { get; set; } = true;
    public int TwoFingerDoubleTapTimeoutMs { get; set; } = 200;
}
