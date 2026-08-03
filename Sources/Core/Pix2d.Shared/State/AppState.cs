using Pix2d.CommonNodes;
using Pix2d.Primitives;
using Pix2d.Primitives.ViewPort;
using SkiaSharp;

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

    /// <summary>
    /// Whether to play tactile "pen on paper" feedback while drawing on a haptic-capable stylus
    /// (e.g. Surface Slim Pen 2 on Windows 11). On by default; no effect on other devices/platforms.
    /// </summary>
    public bool IsPenHapticsEnabled
    {
        get => Get(true);
        set => Set(value);
    }

    public bool IsAutoOpenTransformEditorAfterSelectionEnabled
    {
        get => Get(true);
        set => Set(value);
    }

    /// <summary>
    /// Whether the eyedropper switches back to the previously used tool right after picking a color (#215).
    /// Off by default so the tool keeps its classic "stays until you switch" behaviour; mainly a touch-device
    /// win, where the trip back to the brush is a separate two-tap detour.
    /// </summary>
    public bool IsReturnToPreviousToolAfterColorPickEnabled
    {
        get => Get(false);
        set => Set(value);
    }

    /// <summary>
    /// Color (alpha included) of the canvas grid lines (#223). App-wide rather than per-project — it is a
    /// personal readability preference, unlike the grid cell size, which describes the artwork and lives in
    /// <see cref="ViewPortState.GridSpacing"/>. Pushed into the scene's grid nodes by <c>SnappingService</c>.
    /// </summary>
    public SKColor GridColor
    {
        get => Get(GridDefaults.Color);
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

    /// <summary>
    /// All projects opened in this session (desktop tabs). Invariant: when non-empty,
    /// <see cref="CurrentProject"/> == LoadedProjects[<see cref="ActiveProjectIndex"/>].
    /// Deliberately a plain List — list changes are signalled with ProjectsListChangedMessage.
    /// </summary>
    public List<ProjectState> LoadedProjects { get; set; } = [];

    public int ActiveProjectIndex
    {
        get => Get(0);
        set => Set(value);
    }

    public virtual ProjectState CurrentProject
    {
        get => Get<ProjectState>(new ProjectState());
        set => Set(value);
    }

    public SelectionState SelectionState { get; set; } = new();

    public ToolsState ToolsState { get; set; } = new();

    public SpriteEditorState SpriteEditorState { get; set; } = new();

}
