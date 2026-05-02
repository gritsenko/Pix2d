using Pix2d.Primitives;
using Pix2d.Primitives.ViewPort;

namespace Pix2d.State;

public class AppState : StateBase
{
    public bool IsBusy
    {
        get => Get<bool>();
        set => Set(value);
    }

    public string WindowTitle
    {
        get => Get<string>("New project");
        set => Set(value);
    }

    public string Locale
    {
        get => Get<string>();
        set => Set(value);
    }

    public double UiScale
    {
        get => Get<double>(1.0); 
        set => Set(value);
    }

    public MouseWheelBehavior MouseWheelBehavior
    {
        get => Get<MouseWheelBehavior>(MouseWheelBehavior.Scroll);
        set => Set(value);
    }

    public bool IsTwoFingerDoubleTapUndoEnabled
    {
        get => Get(true);
        set => Set(value);
    }

    public int TwoFingerDoubleTapTimeoutMs
    {
        get => Get(500);
        set => Set(value);
    }

    public bool IsStylusModeEnabled
    {
        get => Get(false);
        set => Set(value);
    }

    public bool IsSingleFingerPanEnabled
    {
        get => Get(false);
        set => Set(value);
    }


    public LicenseType LicenseType
    {
        get => Get<LicenseType>(LicenseType.Essentials);
        set => Set(value);
    }
    public bool IsPro => LicenseType is LicenseType.Pro or LicenseType.Ultimate;

    public Pix2DAppSettings Settings { get; set; } = new();
    public UiState UiState { get; set; } = new();

    public List<ProjectState> LoadedProjects { get; set; } = [];

    public virtual ProjectState CurrentProject
    {
        get => Get<ProjectState>(new ProjectState());
        set => Set(value);
    }

    public SelectionState SelectionState { get; set; } = new();

    public ToolsState ToolsState { get; set; } = new();

    public SpriteEditorState SpriteEditorState { get; set; } = new();

}
