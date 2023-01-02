using System;
using System.Collections.Generic;

namespace Pix2d.Abstract.State;

public interface IAppState: IStateBase
{
    bool IsBusy { get; }
    string WindowTitle { get; }

    IEnumerable<IProjectState> LoadedProjects { get; }
    
    IProjectState CurrentProject { get; }

    ISelectionState SelectionState { get; }

    IDrawingState DrawingState { get; }

}