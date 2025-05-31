using Pix2d.Abstract.State;
using Pix2d.Primitives;

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

    public IReadOnlyList<string> AvailableLocales { get; } = ["en", "ru"];
    public string Locale
    {
        get => Get<string>();
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

    public List<ProjectState> LoadedProjects { get; set; } = new();

    public virtual ProjectState CurrentProject { get; set; } = new();
    
    public SelectionState SelectionState { get; set; } = new();

    public ToolsState ToolsState { get; set; } = new();

    public SpriteEditorState SpriteEditorState { get; set; } = new();

}