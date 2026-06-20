#nullable enable
using Pix2d.Abstract.Services;

namespace Pix2d.Services;

/// <summary>
/// Default <see cref="IPenHapticsService"/> for platforms/heads without pen haptics. Does nothing.
/// The desktop head replaces this with a WinRT-backed implementation on Windows
/// (registration order makes the Windows one win — see <c>DesktopPix2dBootstrapperDI</c>).
/// </summary>
public sealed class NullPenHapticsService : IPenHapticsService
{
    public void Attach(nint windowHandle) { }
    public void Detach() { }
    public void BeginStroke(PenHapticTool tool) { }
    public void EndStroke() { }
}
