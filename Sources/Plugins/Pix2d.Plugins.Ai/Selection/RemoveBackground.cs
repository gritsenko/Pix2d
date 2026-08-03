using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Diagnostics;

namespace Pix2d.Plugins.Ai.Selection;

public static class RemoveBackground
{
    const int Width = 320;
    const int Height = 320;
    const int Channels = 3;

    static double[] means = new double[] { 0.485, 0.456, 0.406 };
    static double[] stds = new double[] { 0.229, 0.224, 0.225 };

    public static int SourceWidth { get; private set; }
    public static int SourceHeight { get; private set; }

    /// <summary>
    /// Set to the failure that permanently disabled background removal on this machine, null while it is
    /// usable. Two parts of the pipeline can simply be absent from a user's installation: the native
    /// onnxruntime library the managed package P/Invokes into (appstat: <c>DllNotFoundException</c> out of
    /// <c>NativeMethods.OrtGetApiBase</c>, 3.11.3, Windows) and the embedded u2netp model. Neither can
    /// appear mid-session, so the first such failure switches AI selection off instead of re-paying the
    /// resize + tensor build on every gesture.
    /// </summary>
    public static Exception? UnavailableReason { get; private set; }

    public static bool IsAvailable => UnavailableReason == null;

    /// <summary>
    /// The last failure seen by <see cref="TryProcess"/>, permanent or not — the message the UI reports.
    /// </summary>
    public static Exception? LastFailure { get; private set; }

    /// <summary>
    /// Runs <see cref="Process"/>, returning null instead of throwing. The exception used to escape all
    /// the way out through <c>SKInput.SetPointerReleased</c> and terminate the app; the caller
    /// (<see cref="AiPixelSelector"/>) now degrades to the plain rectangle the user dragged.
    /// </summary>
    public static SKBitmap? TryProcess(SKBitmap original, string model)
    {
        if (!IsAvailable)
            return null;

        try
        {
            if (AiPlugin.ModelData is not { Length: > 0 })
                throw new InvalidOperationException(
                    "AI model 'u2netp.onnx' is not loaded — the embedded plugin resource is missing.");

            return Process(original, model);
        }
        catch (Exception ex)
        {
            LastFailure = ex;
            if (IsPermanentFailure(ex))
                UnavailableReason = ex;

            Logger.LogException(ex);
            return null;
        }
    }

    // A missing or incompatible native runtime / model is fatal for the whole session; a genuine inference
    // error is not, so that one only costs the current gesture.
    private static bool IsPermanentFailure(Exception ex) =>
        ex is DllNotFoundException or TypeInitializationException or BadImageFormatException
            or EntryPointNotFoundException or InvalidOperationException
        || (ex.InnerException is { } inner && IsPermanentFailure(inner));

    public static unsafe SKBitmap Process(SKBitmap original, string model)
    {
        SourceWidth = original.Width;
        SourceHeight = original.Height;
        var image = original.Resize(new SKSizeI(Width, Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        var input = ConvertImageToFloatData(image, means, stds);
        var sw = new Stopwatch();
        //sw.Start();

        using var session = new InferenceSession(AiPlugin.ModelData);


        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results
            = session.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("input.1", input)
            });

        //sw.Stop();
        //Console.WriteLine(sw.ElapsedMilliseconds);

        if (results.FirstOrDefault()?.Value is not Tensor<float> output)
            throw new ApplicationException("Unable to process image");

        var result = new SKBitmap(Width, Height);
        var ppt = result.GetPixels(out IntPtr len);
        var pixels = new Span<int>(ppt.ToPointer(), (int)len);

        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                pixels[y * Width + x] = (byte)(output[0, 0, x, y] * 255) << 24;

        result = result.Resize(new SKSizeI(original.Width, original.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        return result;
    }

    // Create your Tensor and add transformations as you need.
    public static Tensor<float> ConvertImageToFloatData(SKBitmap image, double[] means, double[] std)
    {
        Tensor<float> data = new DenseTensor<float>(new[] { 1, 3, image.Width, image.Height });

        //var w = image.Width;

        for (var y = 0; y < image.Height; y++)
        {
            //var pixelSpan = image.GetPixelRowSpan(y);
            for (var x = 0; x < image.Width; x++)
            {
                var color = image.GetPixel(x, y);

                //var color = span[x + y * w];
                var red = (color.Red - (float)means[0] * 255) / ((float)std[0] * 255);
                var gre = (color.Green - (float)means[1] * 255) / ((float)std[1] * 255);
                var blu = (color.Blue - (float)means[2] * 255) / ((float)std[2] * 255);
                data[0, 0, x, y] = red;
                data[0, 1, x, y] = gre;
                data[0, 2, x, y] = blu;
            }
        }
        return data;
    }
}