using System;
using Pix2d.Abstract.Selection;

namespace Pix2d.Primitives.Selection;

public class SelectedNodesChangedEventArgs : EventArgs
{
    public INodesSelection Selection;
}