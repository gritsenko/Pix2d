using Microsoft.Extensions.DependencyInjection;
using Pix2d.Abstract.Commands;
using Pix2d.Abstract;
using Pix2d.Abstract.Services;
using Pix2d.Primitives;
using SkiaNodes.Interactive;

namespace Pix2d.Plugins.OpenCv.Commands;

internal class OpenCvCommands : CommandsListBase
{
    protected override string BaseName => "Sprite.OpenCV";

    public Pix2dCommand CaptureImage
        => GetCommand(() => ServiceProvider.GetRequiredService<IImageCaptureService>().PasteImageAsync(),
            "Capture image from camera", new CommandShortcut(VirtualKeys.Q, KeyModifier.Ctrl | KeyModifier.Shift),
            EditContextType.Sprite, behaviour: ServiceProvider.GetRequiredService<DisableOnAnimationCommandBehavior>());
}