using Pix2d.Abstract.State;
using Pix2d.Primitives.Drawing;
using SkiaSharp;

namespace Pix2d.State;

public class SpriteEditorState : StateBase
{
    public int CurrentFrameIndex
    {
        get => Get<int>(0);
        set => Set(value);
    }
    public int CurrentLayerIndex
    {
        get => Get<int>(0);
        set => Set(value);
    }

    public int FramesCount
    {
        get => Get<int>();
        set => Set(value);
    }

    public bool IsPlayingAnimation
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool ShowOnionSkin
    {
        get => Get<bool>();
        set => Set(value);
    }

    public int FrameRate
    {
        get => Get<int>(15);
        set => Set(value);
    }

    public int[] FrameRates { get; } = Enumerable.Range(1, 60).ToArray();

    public SKColor CurrentColor
    {
        get => Get(SKColor.Parse("d2691e"));
        set => Set(value);
    }

    public SKColor BackgroundColor
    {
        get => Get(SKColors.White);
        set => Set(value);
    }

    public List<BrushSettings> BrushPresets
    {
        get => Get(new List<BrushSettings>());
        set => Set(value);
    }

    public BrushSettings CurrentBrushSettings
    {
        get => Get(new BrushSettings());
        set
        {
            value.InitBrush();
            Set(value);
        }
    }

    public float Scale
    {
        get => CurrentBrushSettings.Scale;
        set
        {
            CurrentBrushSettings.Scale = value;
            OnPropertyChanged();
        }
    }

    public float Opacity
    {
        get => Get(100f);
        set => Set(value);
    }

    public float Spacing
    {
        get => Get(1f);
        set => Set(value);
    }

    public BrushSettings CurrentPixelBrushPreset
    {
        get => Get(new BrushSettings());
        set => Set(value);
    }

    public bool IsPixelPerfectDrawingModeEnabled
    {
        get => Get(false);
        set => Set(value);
    }

    public bool HasSelection
    {
        get => Get(false);
        set => Set(value);
    }
    public bool ShowBackground
    {
        get => Get(false);
        set => Set(value);
    }

}