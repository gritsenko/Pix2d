using System;
using System.Collections.ObjectModel;
using System.Linq;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.State;
using Pix2d.Messages;
using Pix2d.Messages.Edit;
using Pix2d.Mvvm;
using SkiaNodes;

namespace Pix2d.ViewModels.SceneTree
{
    public class SceneTreeViewModel : Pix2dViewModelBase
    {
        public ISelectionService SelectionService { get; }
        public IMessenger Messenger { get; }
        public IAppState AppState { get; }

        public ObservableCollection<SceneTreeItemViewModel> Nodes { get; set; } = new ObservableCollection<SceneTreeItemViewModel>();

        public SceneTreeViewModel(IMessenger messenger, IAppState appState, ISelectionService selectionService)
        {
            SelectionService = selectionService;
            Messenger = messenger;
            AppState = appState;

            Messenger.Register<EditContextChangedMessage>(this, OnEditContextChanged);
            Messenger.Register<NodesSelectedMessage>(this, OnNodesSelected);

            throw new NotImplementedException("change to messages");
            // RELOAD VIEW MODEL
            // SceneService.SceneStructureChanged += SceneService_SceneStructureChanged;
            // SceneService.SceneCreated += SceneService_SceneCreated;

        }

        private void OnEditContextChanged(EditContextChangedMessage obj)
        {
            OnLoad();
        }

        private void OnNodesSelected(NodesSelectedMessage obj)
        {
            var selectedNodes = AppState.CurrentProject.Selection.Nodes.ToArray();

            UpdateSelectedNodesInTree(selectedNodes, Nodes);
        }

        private void UpdateSelectedNodesInTree(SKNode[] selectedNodes, ObservableCollection<SceneTreeItemViewModel> items)
        {
            foreach (var item in items)
            {
                var isSelected = selectedNodes.Contains(item.SourceNode);

                item.UpdateSelectionState(isSelected);

                if (isSelected)
                {
                    //ExpandBranch(item);
                }

                UpdateSelectedNodesInTree(selectedNodes, item.Nodes);
            }
        }

        private void ExpandBranch(SceneTreeItemViewModel item)
        {
            var parent = item.ParentItemViewModel;
            while (parent != null)
            {
                parent.IsExpanded = true;
                parent = item.ParentItemViewModel;
            }
        }

        protected override void OnLoad()
        {
            Nodes.Clear();
            var scene = AppState.CurrentProject.SceneNode;
            if (scene == null)
                return;

            var root = AppState.CurrentProject.CurrentContextType == EditContextType.Sprite ? AppState.CurrentProject.CurrentEditedNode : scene;

            foreach (var node in root.Nodes)
            {
                var vm = new SceneTreeItemViewModel(node, IsSelectedItemChanged);
                Nodes.Add(vm);
            }
        }

        private void IsSelectedItemChanged(SceneTreeItemViewModel item)
        {
            SelectionService.Select(item.SourceNode);
        }
    }
}
