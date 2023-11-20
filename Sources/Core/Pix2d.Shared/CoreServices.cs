using Pix2d.Abstract.Platform;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using Pix2d.State;

namespace Pix2d;

public static class CoreServices
{
    public static IViewPortService ViewPortService => GetService<IViewPortService>();
    public static IProjectService ProjectService => GetService<IProjectService>();
    public static ISelectionService SelectionService => GetService<ISelectionService>();
    public static IExportService ExportService => GetService<IExportService>();
    public static IEditService EditService => GetService<IEditService>();
    public static IDrawingService DrawingService => GetService<IDrawingService>();

    public static IToolService ToolService => GetService<IToolService>();
    public static IOperationService OperationService => GetService<IOperationService>();
    public static ICommandService CommandService => GetService<ICommandService>();
    public static IClipboardService ClipboardService => GetService<IClipboardService>();
    public static ISnappingService SnappingService => GetService<ISnappingService>();
    public static IImportService ImportService => GetService<IImportService>();
    public static ISettingsService SettingsService => GetService<ISettingsService>();
    public static IPlatformStuffService PlatformStuffService => GetService<IPlatformStuffService>();
    public static IAppStateService<AppState> AppStateService => GetService<IAppStateService<AppState>>();
    private static T GetService<T>() => CommonServiceLocator.ServiceLocator.Current.GetInstance<T>();

}