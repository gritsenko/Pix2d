using Avalonia.Input;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.DragAndDrop;
using Pix2d.CommonNodes;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.ViewModels.Layers;

namespace Pix2d.Views.Layers;

public class ItemsListBoxDropHandler : DropHandlerBase
{
    private bool Validate<T>(ListBox listBox, DragEventArgs e, object? sourceContext, object? targetContext, bool bExecute) where T : LayerViewModel
    {
        if (sourceContext is not T sourceItem
            || targetContext is not LayersListViewModel vm
            || listBox.GetVisualAt(e.GetPosition(listBox)) is not Control targetControl
            || targetControl.DataContext is not T targetItem)
        {
            return false;
        }

        var items = vm.Layers;
        var sourceIndex = items.IndexOf(sourceItem);
        var targetIndex = items.IndexOf(targetItem);

        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        switch (e.DragEffects)
        {
            case DragDropEffects.Copy:
            {
                if (bExecute)
                {
                    var editor = vm.EditService.GetCurrentEditor() as SpriteEditor;
                    var reversedTargetIndex = vm.Layers.Count - targetIndex;
                    editor.DuplicateLayer(sourceItem.SourceNode, reversedTargetIndex);
                    // var clone = new LayerViewModel(layerCopy, editor) { Name =  sourceItem.LayerName + "_copy" };
                    // InsertItem(items, clone, targetIndex);
                }
                return true;
            }
            case DragDropEffects.Move:
            {
                if (bExecute)
                {
                    items.Move(sourceIndex, targetIndex);
                    vm.SelectedLayer = sourceItem;
                }
                return true;
            }
            case DragDropEffects.Link:
            {
                if (bExecute)
                {
                    SwapItem(items, sourceIndex, targetIndex);
                }
                return true;
            }
            default:
                return false;
        }
    }

    public override bool Validate(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ListBox listBox)
        {
            return Validate<LayerViewModel>(listBox, e, sourceContext, targetContext, false);
        }
        return false;
    }

    public override bool Execute(object? sender, DragEventArgs e, object? sourceContext, object? targetContext, object? state)
    {
        if (e.Source is Control && sender is ListBox listBox)
        {
            return Validate<LayerViewModel>(listBox, e, sourceContext, targetContext, true);
        }
        return false;
    }
}