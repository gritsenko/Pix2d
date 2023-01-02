using System;
using System.Collections.Generic;
using System.Linq;
using Mvvm;
using Pix2d.Abstract.Selection;
using Pix2d.CommonNodes;
using Pix2d.Mvvm;
using SkiaNodes;
using HorizontalAlignment = Pix2d.Primitives.Selection.HorizontalAlignment;
using VerticalAlignment = Pix2d.Primitives.Selection.VerticalAlignment;

namespace Pix2d.ViewModels.NodeProperties;

public class SelectionPropertiesViewModel : Pix2dViewModelBase
{
    private static Dictionary<Type, Func<SKNode, ViewModelBase>> _specificNodePropertiesVms = new Dictionary<Type, Func<SKNode, ViewModelBase>>
    {
        { typeof(TextNode), n => new TextNodePropertiesViewModel(n as TextNode) },
    };


    private NodesSelection _selection = null;
    private SKNode _lastSelectedNode;
    private readonly Dictionary<string, object> _oldPropValues = new Dictionary<string, object>();


    public bool HasSelection => _selection != null && _selection.Nodes.Any();

    private NodesSelection Selection => _selection ?? NodesSelection.Empty;

    public string Path => Selection.Path;

    public string Name
    {
        get => Selection.Name;
        set => Selection.Name = value;
    }

    public float X
    {
        get => Selection.X;
        set => Selection.X = value;
    }

    public float Y
    {
        get => Selection.Y;
        set => Selection.Y = value;
    }

    public float Width
    {
        get => Selection.Width;
        set => Selection.Width = value;
    }

    public float Height
    {
        get => Selection.Height;
        set => Selection.Height = value;
    }

    public float Opacity
    {
        get => Selection.Opacity;
        set => Selection.Opacity = value;
    }

    public bool IsVisible
    {
        get => Selection.IsVisible;
        set => Selection.IsVisible = value;
    }

    public ViewModelBase SpecificNodeTypeProperties
    {
        get => Get<ViewModelBase>();
        set => Set(value);
    }

    public NodeEffectsViewModel SelectionEffects
    {
        get => Get<NodeEffectsViewModel>();
        set => Set(value);
    }

    public bool ExportIgnore
    {
        get => Selection.ExportMode == NodeExportMode.Ignore;
        set => Selection.ExportMode = value ? NodeExportMode.Ignore : NodeExportMode.Export;
    }

    public float ExportScale
    {
        get => Selection.ExportScale;
        set => Selection.ExportScale = value;
    }

    public NodeExportFormat ExportFormat
    {
        get => Selection.ExportFormat;
        set => Selection.ExportFormat = value;
    }

    public IEnumerable<NodeExportFormat> ExportFormats =>
        Enum.GetValues(typeof(NodeExportFormat))
            .Cast<NodeExportFormat>();

    public string ExportTextureKey
    {
        get => HasSelection ? Selection.Nodes?[0].DesignerState.ExportSettings.TextureKey : "";
        set
        {
            if (Selection.Nodes != null)
            {
                foreach (var node in Selection.Nodes)
                {
                    node.DesignerState.ExportSettings.TextureKey = value;
                }
            }
        }
    }

    public string ExportClassName
    {
        get => HasSelection ? Selection.Nodes?[0].DesignerState.ExportSettings.ClassName : "";
        set
        {
            if (Selection.Nodes != null)
            {
                foreach (var node in Selection.Nodes)
                {
                    node.DesignerState.ExportSettings.ClassName = value;
                }
            }
        }
    }

    public string OnClickHandlerName
    {
        get => HasSelection ? Selection.Nodes?[0].DesignerState.ExportSettings.OnClickHandlerName : "";
        set
        {
            if(Selection.Nodes != null)
                Selection.Nodes[0].DesignerState.ExportSettings.OnClickHandlerName = value;
        }
    }

    public RelayCommand<string> HorizontalAlignCommand => GetCommand<string>(OnHorizontalAlignCommandExecute);
    public RelayCommand<string> VerticalAlignCommand => GetCommand<string>(OnVerticalAlignCommandExecute);

    public IViewModelService ViewModelService { get; }

    public SelectionPropertiesViewModel(IViewModelService viewModelService)
    {
        ViewModelService = viewModelService;
    }

    private void OnVerticalAlignCommandExecute(string param)
    {
        var val = (VerticalAlignment)Enum.Parse(typeof(VerticalAlignment), param);
        Selection.AlignVertically(val);
    }

    private void OnHorizontalAlignCommandExecute(string param)
    {
        var val = (HorizontalAlignment)Enum.Parse(typeof(HorizontalAlignment), param);
        Selection.AlignHorizontally(val);
    }

    public void Clear()
    {
        UpdateSelection(null);
    }

    public void UpdateSelection(INodesSelection selection)
    {
        var oldHasSelection = HasSelection;
        _selection = selection as NodesSelection;

        UpdateTypeSpecificProperties();
        UpdateEffectsViewModel();

        if (oldHasSelection != HasSelection)
            OnPropertyChanged(nameof(HasSelection));

        OnPropertyChanged(nameof(Path));

        NotifyChangedProperties();
    }

    private async void UpdateEffectsViewModel()
    {
        if (!HasSelection)
        {
            SelectionEffects = null;
            return;
        }

        SelectionEffects = ViewModelService.GetViewModel<NodeEffectsViewModel>(false);
        if (SelectionEffects != null)
        {
            SelectionEffects.SourceNode = _selection.Nodes[0];
            await SelectionEffects.Load();
        }
    }

    private void UpdateTypeSpecificProperties()
    {
        ViewModelBase changedVm = null;

        try
        {
            if (_selection == null) return;

            var node = _selection.Nodes.FirstOrDefault();

            if (_lastSelectedNode == node)
                return;

            if (node != null && _specificNodePropertiesVms.TryGetValue(node.GetType(), out var getter))
            {
                changedVm = getter.Invoke(node);
            }

            _lastSelectedNode = node;
        }
        finally
        {
            if (changedVm != null && changedVm != SpecificNodeTypeProperties)
            {
                if (SpecificNodeTypeProperties != null && SpecificNodeTypeProperties is IDisposable dvm)
                {
                    dvm.Dispose();
                }

                SpecificNodeTypeProperties = changedVm;
            }
        }
    }


    private void NotifyChangedProperties()
    {
        var propInfos = this.GetType().GetProperties();
        foreach (var info in propInfos)
        {
            if (!_oldPropValues.TryGetValue(info.Name, out var oldVal))
            {
                OnPropertyChanged(info.Name);
                continue;
            }

            var newPropVal = info.GetValue(this);

            if (oldVal != newPropVal)
            {
                OnPropertyChanged(info.Name);
            }

            _oldPropValues[info.Name] = newPropVal; 
        }
    }
}