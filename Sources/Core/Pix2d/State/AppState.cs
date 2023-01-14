using System.Collections.Generic;
using Pix2d.Abstract.State;

namespace Pix2d.State
{
    public class AppState : StateBase, IAppState
    {
        public bool IsBusy { get; set; }

        public string WindowTitle { get; set; }

        public Pix2DAppSettings Settings { get; set; } = new Pix2DAppSettings();
        public UiState UiState { get; set; } = new();

        
        public IEnumerable<IProjectState> LoadedProjects { get; set; } = new List<ProjectState>();

        public IProjectState CurrentProject { get; set; } = new ProjectState();
        
        public ISelectionState SelectionState { get; set; } = new SelectionState();

        public IDrawingState DrawingState { get; set; } = new DrawingState();

    }
}
