using System;
using System.Collections.Generic;
using System.Linq;
using Mvvm.Messaging;
using Pix2d.Abstract;
using Pix2d.Abstract.State;
using Pix2d.Abstract.Tools;
using Pix2d.Messages;
using Pix2d.State;
using Pix2d.Tools;

namespace Pix2d.Services
{
    public class ToolService : IToolService
    {
        public IMessenger Messenger { get; }
        public IAppState AppState { get; }
        private readonly SimpleContainer _container;
        
        private readonly Dictionary<string, ToolMeta> _tools = new Dictionary<string, ToolMeta>();
        private readonly Dictionary<EditContextType, string> _defaultContextTool = new Dictionary<EditContextType, string>();

        private ITool CurrentTool
        {
            get => AppState.CurrentProject.CurrentTool;
            set => ((ProjectState)AppState.CurrentProject).CurrentTool = value;
        }

        public ToolService(SimpleContainer container, IMessenger messenger, IAppState appState)
        {
            Messenger = messenger;
            AppState = appState;
            _container = container;
            messenger.Register<ProjectLoadedMessage>(this, m => ActivateDefaultTool());
        }

        public async void ActivateTool(string key)
        {
            if (!_tools.ContainsKey(key))
                return;

            if (CurrentTool?.Key == key) return;

            var newTool = GetToolByKey(key);
            //Tool is just action - no need to select
            if (newTool.Behavior == ToolBehaviorType.OneAction && newTool.IsEnabled)
            {
                await newTool.Activate();

                if (!string.IsNullOrWhiteSpace(newTool.NextToolKey))
                {
                    ActivateTool(newTool.NextToolKey);
                }
                return;
            }

            var oldTool = CurrentTool;

            CurrentTool?.Deactivate();
            CurrentTool = newTool;
            
            if (CurrentTool?.IsEnabled == true)
            {
                await CurrentTool.Activate();

                OnToolChanged(oldTool, CurrentTool);
            }
        }

        public void ActivateDefaultTool()
        {
            var context = AppState.CurrentProject.CurrentContextType;
            if(!_defaultContextTool.TryGetValue(context, out var defaultTool))
            {
                defaultTool = _defaultContextTool[EditContextType.General];
            }

            ActivateTool(defaultTool);
        }

        public void Initialize()
        {
            RegisterTool<ObjectManipulationTool>(EditContextType.General);
        }

        public void RegisterTool<TTool>(EditContextType context)
            where TTool : ITool
        {
            var toolMeta = new ToolMeta<TTool>(context);

            _tools[toolMeta.Key] = toolMeta;

            if (!_defaultContextTool.ContainsKey(context))
            {
                _defaultContextTool[context] = toolMeta.Key;
            }
        }

        public ITool GetToolByKey(string key)
        {
            var meta = _tools[key];
            
            if (meta.Instance == null) 
                meta.Instance = _container.BuildInstance(meta.ToolType) as ITool;
            
            return meta.Instance;
        }

        public IEnumerable<Type> GetTools(EditContextType contextType)
        {
            return _tools.Values
                .Where(x => x.ContextType == contextType)
                .Select(x => x.ToolType)
                .ToArray();
        }
        
        public void DeactivateTool()
        {
            CurrentTool?.Deactivate();
        }
        protected virtual void OnToolChanged(ITool oldTool, ITool newTool)
        {
            Messenger.Send(new CurrentToolChangedMessage(oldTool, newTool));
        }

        private class ToolMeta
        {
            public Type ToolType { get; set; }
            public ITool Instance { get; set; }
            public EditContextType ContextType { get; set; }
            public string Key { get; set; }
        }
        
        private class ToolMeta<TTool> : ToolMeta
            where TTool : ITool
        {

            public ToolMeta(EditContextType contextType)
            {
                ContextType = contextType;
                Key = typeof(TTool).Name;
                ToolType = typeof(TTool);
            }
        }

    }
}
