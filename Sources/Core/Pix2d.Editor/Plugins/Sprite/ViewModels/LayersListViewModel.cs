using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Operations;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.UI;
using Pix2d.Common;
using Pix2d.CommonNodes;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Modules.Sprite.Editors;
using Pix2d.Mvvm;
using Pix2d.Operations;
using Pix2d.Plugins.Sprite;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.Plugins.Sprite.Operations.Effects;
using Pix2d.Primitives.SpriteEditor;
using SkiaNodes;
using SkiaNodes.Common;
using SkiaSharp;

namespace Pix2d.ViewModels.Layers
{
    public class LayersListViewModel : Pix2dViewModelBase
    {
        private readonly IPanelsController _panelsController;
        public IEditService EditService { get; }
        public IEffectsService EffectsService { get; } 
        public IMessenger Messenger { get; }

        private bool _internalLayersUpdate;
        private bool _layersReordering;
        private SpriteEditor _editor;
        private ChangeOpacityOperation _changeOpacityOperation;
        private int _reorderingNodeOldIndex;

        public ObservableCollection<LayerViewModel> Layers { get; set; } = new ObservableCollection<LayerViewModel>();
        public ArtboardNode CurrentArtboard { get; set; }

        public bool UseBackgroundColor
        {
            get => _editor?.CurrentSprite?.UseBackgroundColor ?? false;
            set
            {
                if (_editor == null)
                    return;

                if (value != _editor.CurrentSprite.UseBackgroundColor)
                {
                    _editor.CurrentSprite.UseBackgroundColor = value;
                    _editor.NotifyLayerChanged(SelectedLayer.SourceNode as Pix2dSprite.Layer);
                    OnPropertyChanged();
                    RefreshViewPort();
                }
            }
        }

        public SKColor SelectedBackgroundColor
        {
            get => _editor?.CurrentSprite?.BackgroundColor ?? SKColor.Empty;
            set
            {
                if (_editor == null)
                    return;

                if (value != _editor.CurrentSprite.BackgroundColor)
                {
                    _editor.CurrentSprite.BackgroundColor = value;
                    UseBackgroundColor = true;
                    _editor.NotifyLayerChanged(SelectedLayer.SourceNode as Pix2dSprite.Layer);
                    OnPropertyChanged();
                    RefreshViewPort();
                }
            }
        }

        [NotifiesOn(nameof(SelectedBackgroundColor))]
        [NotifiesOn(nameof(UseBackgroundColor))]
        public SKColor ResultBackgroundColor => UseBackgroundColor ? SelectedBackgroundColor : SKColor.Empty;


        public LayerViewModel SelectedLayer { get; set; }

        public bool ShowLayerProperties
        {
            get => _panelsController?.ShowLayerProperties ?? false;
            set
            {
                if (_panelsController != null)
                    _panelsController.ShowLayerProperties = value;
            }
        }

        public List<EffectViewModel> AvailableEffects { get; set; } = new List<EffectViewModel>();

        public Action<int> OnPreviewChanged { get; set; }
        public IRelayCommand ToggleUseBackgroundCommand => GetCommand(() =>
            {
                UseBackgroundColor = !UseBackgroundColor;
                RefreshViewPort();
            });

        public IRelayCommand CloseLayerPropertiesCommand => GetCommand(() => _panelsController.ShowLayerProperties = false);

        public IRelayCommand ClearLayerCommand => GetCommand(() => CoreServices.DrawingService.ClearCurrentLayer());
        public IRelayCommand AddLayerCommand => GetCommand(() => _editor.AddEmptyLayer());
        public IRelayCommand DuplicateLayerCommand => GetCommand(() => _editor.DuplicateLayer());
        public IRelayCommand AddEffectToLayerCommand => GetCommand<EffectViewModel>(vm => SelectedLayer?.AddEffect(vm));

        public ICommand SendBackwardCommand => MapCommand(SpritePlugin.EditCommands.SendLayerBackward);
        public ICommand BringForwardCommand => MapCommand(SpritePlugin.EditCommands.BringLayerForward);


        public IRelayCommand MergeLayerCommand => GetCommand(
            () => _editor.MergeDownLayer(),
            () => _editor?.CanMergeDownLayer() ?? false);

        public IRelayCommand DeleteLayerCommand => GetCommand(() =>
        {
            if (SelectedLayer?.SourceNode == null)
                return;

            _editor.DeleteLayer();
        }, () => Layers.Count > 1);

        public IRelayCommand SelectLayerCommand => GetCommand<LayerViewModel>((layerVm) =>
        {
            if (layerVm != null)
            {
                if (SelectedLayer == layerVm)
                {
                    _panelsController.ShowLayerProperties = !_panelsController.ShowLayerProperties;
                }
                else
                {
                    SetSelectedLayer(layerVm, true);
                }
            }
        });


        public LayersListViewModel(IEditService editService, IPanelsController panelsController, IEffectsService effectsService, IMessenger messenger)
        {
            if (IsDesignMode)
                return;

            _panelsController = panelsController;

            EditService = editService;
            EffectsService = effectsService;
            Messenger = messenger;

            Layers.CollectionChanged += Layers_CollectionChanged;

            Messenger.Register<NodeEditorChangedMessage>(this, OnNodeEditorChanged);
            Messenger.Register<OperationInvokedMessage>(this, OnOperationInvoked);
            Messenger.Register<CanvasSizeChanged>(this, msg => Reload());
            
            AvailableEffects.AddRange(EffectsService.GetAvailableEffects().Select(x => new EffectViewModel(Activator.CreateInstance(x.Value) as ISKNodeEffect)));
            OnEditorChanged();
        }

        private void OnNodeEditorChanged(NodeEditorChangedMessage obj)
        {
            OnEditorChanged();
            Reload();
        }

        private void SetSelectedLayer(LayerViewModel layerVm, bool sendUpdateToEditor)
        {
            var oldLayerVm = SelectedLayer;

            oldLayerVm?.UpdatePreview();

            if (oldLayerVm != layerVm)
            {
                if (oldLayerVm != null)
                    oldLayerVm.IsSelected = false;

                SelectedLayer = layerVm;
                ResetPropertyChangedOperation();

                if (layerVm != null)
                {
                    layerVm.IsSelected = true;

                    if (layerVm.SourceNode is SKNode l) SessionLogger.OpLog("Select layer: " + l.Index + " " + l.Name);

                    if (sendUpdateToEditor)
                        _editor.SelectLayer(layerVm.SourceNode as Pix2dSprite.Layer);
                }

                OnPropertyChanged(nameof(SelectedLayer));
                UpdateCommandsCanExecute();
            }
        }
        private void UpdateCommandsCanExecute()
        {
            DeleteLayerCommand.RaiseCanExecuteChanged();
            DuplicateLayerCommand.RaiseCanExecuteChanged();
            AddLayerCommand.RaiseCanExecuteChanged();
            MergeLayerCommand.RaiseCanExecuteChanged();
        }
        
        private void OnOperationInvoked(OperationInvokedMessage e)
        {
            if(SelectedLayer == null || _editor == null || e.Operation == null)
                return;
            
            if (SelectedLayer.SourceNode != _editor.SelectedLayer)
            {
                EditorOnSelectedLayerChanged(this, default);
            }

            if (e.Operation is ChangeOpacityOperation opc && e.OperationType != OperationEventType.Perform)
            {
                SelectedLayer.UpdateOpacity();
            }

            if (e.Operation is AddEffectOperation aeo
                && (e.OperationType == OperationEventType.Perform
                    || e.OperationType == OperationEventType.Undo
                    || e.OperationType == OperationEventType.Redo))
            {
                SelectedLayer.UpdateEffectsFromNode();
                SelectedLayer.SelectedEffect = SelectedLayer.Effects?.FirstOrDefault(x => x.Effect == aeo.GetEffect(SelectedLayer.SourceNode));
            }

            if (e.Operation is BakeEffectOperation
                && (e.OperationType == OperationEventType.Perform
                    || e.OperationType == OperationEventType.Undo
                    || e.OperationType == OperationEventType.Redo))
            {
                SelectedLayer.UpdateEffectsFromNode();
                SelectedLayer.SelectedEffect = null;
            }

            if (e.Operation is RemoveEffectOperation
                && (e.OperationType == OperationEventType.Perform
                    || e.OperationType == OperationEventType.Undo
                    || e.OperationType == OperationEventType.Redo))
            {
                SelectedLayer.UpdateEffectsFromNode();
                SelectedLayer.SelectedEffect = null;
            }

            DeferredAction.Run(100, () =>
            {
                SelectedLayer?.UpdatePreview();

                if (OnPreviewChanged != null && SelectedLayer != null)
                {
                    OnPreviewChanged(Layers.IndexOf(SelectedLayer));
                }
            });
        }

        private void OnEditorChanged()
        {
            var newEditor = EditService.GetCurrentEditor() as SpriteEditor;
            if (_editor != newEditor)
            {
                if (_editor != null)
                {
                    _editor.LayersChanged -= EditorOnLayersChanged;
                    _editor.FramesChanged -= EditorOnFramesChanged;
                    _editor.CurrentFrameChanged -= EditorOnCurrentFrameChanged;
                    _editor.SelectedLayerChanged -= EditorOnSelectedLayerChanged;
                    _editor.PlaybackStateChanged -= EditorOnPlaybackStateChanged;
                }

                _editor = newEditor;

                if (_editor != null)
                {
                    _editor.LayersChanged += EditorOnLayersChanged;
                    _editor.FramesChanged += EditorOnFramesChanged;
                    _editor.CurrentFrameChanged += EditorOnCurrentFrameChanged;
                    _editor.SelectedLayerChanged += EditorOnSelectedLayerChanged;
                    _editor.PlaybackStateChanged += EditorOnPlaybackStateChanged;
                }
            }
        }

        private void EditorOnPlaybackStateChanged(object sender, EventArgs e)
        {
            Reload();
        }

        private void EditorOnSelectedLayerChanged(object sender, EventArgs e)
        {
            var layerVm = Layers.FirstOrDefault(x => x.SourceNode == _editor.SelectedLayer);
            SetSelectedLayer(layerVm, false);
        }

        private void EditorOnLayersChanged(object sender, EventArgs e)
        {
            Reload();
        }

        public void Reload()
        {
            if (!_editor.IsPlaying)
                OnLoad();
        }

        private void EditorOnCurrentFrameChanged(object sender, SpriteFrameChangedEvenArgs e)
        {
            if (!e.IsAnimationPlaying)
            {
                UpdateLayerThumbs();
            }
        }

        private void UpdateLayerThumbs()
        {
            foreach (var layerViewModel in Layers.ToArray())
            {
                layerViewModel.UpdatePreview();
            }
        }

        private void EditorOnFramesChanged(object sender, FramesChangedEventArgs e)
        {

        }

        private void Layers_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_internalLayersUpdate)
                return;

            _layersReordering = true;

            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                _reorderingNodeOldIndex = e.OldStartingIndex;
            }

            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var newIndex = e.NewStartingIndex;

                var editor = EditService.GetCurrentEditor() as SpriteEditor;

                var reversedOldIndex = Layers.Count - _reorderingNodeOldIndex - 1;
                var reversedNewIndex = Layers.Count - newIndex - 1;

                editor.ReorderLayers(reversedOldIndex, reversedNewIndex);
                _reorderingNodeOldIndex = -1;
            }

            _layersReordering = false;
            //Debug.WriteLine(e.Action);
        }

        public void ResetPropertyChangedOperation()
        {
            if (SelectedLayer == null)
            {
                _changeOpacityOperation = null;
                return;
            }

            _changeOpacityOperation = new ChangeOpacityOperation(SelectedLayer.SourceNode.Yield());
        }
        public void PushPropertyChangedOperation()
        {
            _changeOpacityOperation?.SetFinalData();
            _changeOpacityOperation?.PushToHistory();
            ResetPropertyChangedOperation();
        }

        protected override void OnLoad()
        {
            if (_layersReordering)
                return;

            _internalLayersUpdate = true;
            if (_editor?.CurrentSprite == null)
                return;

            FillLayersFromNodes(_editor.CurrentSprite.Layers);
            SetSelectedLayer(Layers.FirstOrDefault(x => x.SourceNode == _editor.CurrentSprite.SelectedLayer), false);
            ShowLayerProperties = false;
            OnPropertyChanged(nameof(UseBackgroundColor));
            OnPropertyChanged(nameof(ResultBackgroundColor));

            _internalLayersUpdate = false;
        }

        private void FillLayersFromNodes(IEnumerable<Pix2dSprite.Layer> nodes)
        {
            Layers.Clear();
            var reversedNodes = nodes.Reverse().ToArray();
            foreach (var node in reversedNodes)
            {
                var layerVm = new LayerViewModel(node, _editor)
                {
                    CloseCommand = CloseLayerPropertiesCommand,
                    ClearLayerCommand = this.ClearLayerCommand,
                    DeleteLayerCommand = this.DeleteLayerCommand,
                    DuplicateLayerCommand = this.DuplicateLayerCommand,
                    MergeLayerCommand = this.MergeLayerCommand,
                    BringForwardCommand = this.BringForwardCommand,
                    SendSendBackwardCommand = this.SendBackwardCommand,
                    SelectLayerCommand = this.SelectLayerCommand
                };
                Layers.Add(layerVm);
            }
        }

    }
}