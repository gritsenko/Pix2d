using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Pix2d.Mvvm;
using SkiaNodes;

namespace Pix2d.ViewModels.SceneTree;

public class SceneTreeItemViewModel : Pix2dViewModelBase
{
    private readonly Action<SceneTreeItemViewModel> _isSelectedChangedAction;
    private bool _isSelected;
    public SKNode SourceNode { get; set; }
    public SceneTreeItemViewModel ParentItemViewModel { get; set; }
    public string Name { get; set; }

    public bool IsExpanded
    {
        get => SourceNode.DesignerState.IsExpanded;
        set
        {
            if (SourceNode.DesignerState.IsExpanded != value)
            {
                SourceNode.DesignerState.IsExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsLocked
    {
        get => SourceNode.DesignerState.IsLocked;
        set
        {
            if (SourceNode.DesignerState.IsLocked != value)
            {
                SourceNode.DesignerState.IsLocked = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsVisible
    {
        get => SourceNode.IsVisible;
        set
        {
            if (SourceNode.IsVisible != value)
            {
                SourceNode.IsVisible = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                OnPropertyChanged();
                _isSelectedChangedAction?.Invoke(this);
            }
        }
    }

    public ObservableCollection<SceneTreeItemViewModel> Nodes { get; set; } = new ObservableCollection<SceneTreeItemViewModel>();

    public ICommand ToggleVisibilityCommand => GetCommand(() => IsVisible = !IsVisible);
    public SceneTreeItemViewModel(SKNode node, Action<SceneTreeItemViewModel> isSelectedChangedAction)
    {
        _isSelectedChangedAction = isSelectedChangedAction;
        SourceNode = node;
        Name = node.Name ?? node.GetType().Name;

        if (SourceNode.DesignerState.ShowChildrenInTree == true)
        {

            foreach (var child in node.Nodes)
            {
                var vm = new SceneTreeItemViewModel(child, isSelectedChangedAction) {ParentItemViewModel = this};
                Nodes.Add(vm);
            }
        }
    }

    public void UpdateSelectionState(bool isSelected)
    {
        if (_isSelected != isSelected)
        {
            _isSelected = isSelected;
            OnPropertyChanged(nameof(IsSelected));
        }
    }
}