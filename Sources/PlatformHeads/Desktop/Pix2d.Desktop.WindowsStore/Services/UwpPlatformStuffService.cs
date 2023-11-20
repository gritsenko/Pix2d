using Windows.Services.Store;
using Pix2d.State;

namespace Pix2d.Desktop.Services;

public class UwpPlatformStuffService : PlatformStuffService
{
    public static StoreContext WindowsStoreContext { get; set; }

    public UwpPlatformStuffService(AppState state) : base(state)
    {
    }

    internal static void InitStoreContext()
    {
        var hwnd = EditorApp.TopLevel.TryGetPlatformHandle()!.Handle;
        WindowsStoreContext = StoreContext.GetDefault();
        // Initialize the dialog using wrapper funcion for IInitializeWithWindow
        WinRT.Interop.InitializeWithWindow.Initialize(WindowsStoreContext, hwnd);
    }
}