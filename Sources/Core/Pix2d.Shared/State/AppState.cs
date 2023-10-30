using System.Collections.Generic;
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

    public ProjectState CurrentProject { get; set; } = new();
    
    public SelectionState SelectionState { get; set; } = new();

    public DrawingState DrawingState { get; set; } = new();
}
