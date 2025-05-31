using Pix2d.Abstract;
using System.Diagnostics.CodeAnalysis;
using Pix2d.Abstract.Services;
using Pix2d.Abstract.Tools;
using System.Reflection;

namespace Pix2d.Plugins.Ai;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AiPlugin))]
public class AiPlugin : IPix2dPlugin
{
    public const string ToolIcon = "M12.21,98.7v11.97h12.66v12.21H6.1c-3.37,0-6.1-2.73-6.1-6.1V98.7H12.21L12.21,98.7z M27.89,20.54h67.64 c3.37,0,6.1,2.73,6.1,6.1v69.73c0,3.37-2.73,6.1-6.1,6.1H27.89c-3.37,0-6.1-2.73-6.1-6.1V26.64 C21.79,23.27,24.52,20.54,27.89,20.54L27.89,20.54z M91.34,30.82H32.07v61.36h59.27V30.82L91.34,30.82z M0,24.18V6.1 C0,2.73,2.73,0,6.1,0h18.76v12.21H12.21v11.97H0L0,24.18z M110.27,24.18V12.21H97.61V0h18.76c3.37,0,6.1,2.73,6.1,6.1v18.07H110.27 L110.27,24.18z M122.47,98.7v18.07c0,3.37-2.73,6.1-6.1,6.1H97.61v-12.21h12.66V98.7H122.47L122.47,98.7z";
    public IDrawingService DrawingService { get; }
    public IToolService ToolService { get; }

    public static byte[] ModelData { get; set; }

    public AiPlugin(IDrawingService drawingService, IToolService toolService)
    {
        DrawingService = drawingService;
        ToolService = toolService;
    }


    public void Initialize()
    {
        LoadModel();

        ToolService.RegisterTool<ExtractObjectTool>(EditContextType.Sprite);
        ToolService.RegisterTool<ImageGenerateTool>(EditContextType.Sprite);
    }

    private void LoadModel()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(str => str.EndsWith("u2netp.onnx", StringComparison.InvariantCultureIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var ms = new MemoryStream();
        
        if (stream == null) return;
        
        stream.CopyTo(ms);
        ModelData = ms.ToArray();
    }
}