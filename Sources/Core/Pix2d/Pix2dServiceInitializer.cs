using System;
using Pix2d.Abstract.Tools;
using Pix2d.Abstract.UI;
using Pix2d.Mvvm;
using Pix2d.Services;

namespace Pix2d
{
    public static class Pix2dServiceInitializer
    {
        public static void RegisterServices()
        {
            var container = IoC.Get<SimpleContainer>();
            container.RegisterSingleton<ISessionService, SessionService>();
            container.RegisterSingleton<IProjectService, ProjectService>();
            container.RegisterSingleton<IBusyController, DefaultBusyController>();
            container.RegisterSingleton<ISceneService, SceneService>();
            container.RegisterSingleton<IToolService, ToolService>();
            container.RegisterSingleton<IImportService, ImportService>();
            container.RegisterSingleton<ISelectionService, SelectionService>();
            container.RegisterSingleton<IEditService, EditService>();
            container.RegisterSingleton<IOperationService, OperationService>();
            container.RegisterSingleton<ICommandService, CommandService>();
            container.RegisterSingleton<IObjectCreationService, ObjectCreationService>();
            container.RegisterSingleton<IExportService, ExportService>();
            container.RegisterSingleton<ISnappingService, SnappingService>();
            container.RegisterSingleton<IPaletteService, PaletteService>();
            container.RegisterSingleton<IEffectsService, EffectsService>();
            container.RegisterSingleton<IViewModelService, ViewModelService>();
            container.RegisterSingleton<ReviewService>();
        }

        public static void RegisterService(Type interfaceType, Type instanceType)
        {
            var container = IoC.Get<SimpleContainer>();
            container.RegisterSingleton(interfaceType, instanceType);
        }

        public static void RegisterServiceInstance<TInstance>(object instance)
        {
            var container = IoC.Get<SimpleContainer>();
            container.RegisterInstance<TInstance>(instance);
        }
    }
}