using OpenCvSharp;
using Pix2d.Abstract.Services;
using SkiaNodes;
using SkiaSharp;
using System.Diagnostics.CodeAnalysis;

namespace Pix2d.Desktop.Services;

[method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(OpenCvCaptureService))]
internal class OpenCvCaptureService(IViewPortService viewPortService, IDrawingService drawingService) : IImageCaptureService
{
    public async Task PasteImageAsync()
    {
        var img = await GetImageAsync();
        if (img != null)
        {
            var dln = drawingService?.DrawingLayer as SKNode;
            //var isResized = await TryToResizeCanvas(dln, img);
            var localPos = SKPoint.Empty;// dln.GetLocalPosition(viewPortService.ViewPort.ViewPortCenterGlobal);
            //if (isResized)
            //{
            //    localPos = new SKPoint(0, 0);
            //    ViewPortService.ShowAll();
            //}
            drawingService.PasteBitmap(img, localPos);
        }
    }

    public async Task<SKBitmap> GetImageAsync()
    {
        using var capture = new VideoCapture(0);
        // Check if the camera is opened successfully
        if (!capture.IsOpened())
        {
            Console.WriteLine("Error: Could not access the camera.");
            return null;
        }

        SKBitmap? result = null;

        // Create a window to display the camera feed
        //using var window = new Window("Camera Feed");
        // Main loop to capture and display frames
        while (true)
        {
            // Capture a frame from the camera
            using var frame = new Mat();
            capture.Read(frame);

            // Check if the frame is empty
            if (frame.Empty())
            {
                Console.WriteLine("Error: Could not read frame.");
                break;
            }

            // Display the frame in the window
            //window.ShowImage(frame);
            await Task.Delay(300);
            result = ConvertMatToSKBitmap(frame);
            break;
        }

        return result;
    }

    static SKBitmap ConvertMatToSKBitmap(Mat mat)
    {
        using var stream = mat.ToMemoryStream();
        return SKBitmap.Decode(stream);
    }
}