using Newtonsoft.Json;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public class BitmapNode : SKNode, IDrawingTarget, IBitmapNode
{
    private SKBitmap? _bitmap;
    protected SKRect _bitmapRect;
    protected SKRect _nodeRect;
    private Func<SKBitmap>? _substitute;

    /// <summary>
    /// Cached immutable snapshot used only for the minified (zoomed-out) draw path — built from whichever
    /// bitmap is currently drawn (the node's own <see cref="_bitmap"/> or the live-drawing substitute
    /// snapshot, tracked in <see cref="_mipSource"/>). Built, used and disposed exclusively on the compositor
    /// render thread — the only pass that runs with <c>RenderAdorners == true</c>; the UI thread merely flags
    /// it stale via <see cref="_renderCacheDirty"/>, so there is no cross-thread <see cref="SKImage"/> dispose
    /// race.
    /// </summary>
    private SKImage? _mipImage;
    private SKBitmap? _mipSource;
    private volatile bool _renderCacheDirty;

    /// <summary>
    /// On-screen zoom (<see cref="ViewPort.DpiEffectiveZoom"/>) at or below which bitmap nodes stop sampling
    /// the full-resolution bitmap with nearest-point <c>DrawBitmap</c> and instead draw from a cached
    /// mipmapped image with trilinear sampling. At heavy minification nearest sampling reads scattered source
    /// texels (cache-thrashing, memory-bandwidth bound) and aliases; mipmaps give coherent reads and a clean
    /// downscale. Above the threshold pixel-art crispness near 1:1 is preserved. Set to 0 to disable the mip
    /// path entirely.
    /// </summary>
    public static float MinificationMipmapZoomThreshold { get; set; } = 0.5f;

    private static readonly SKSamplingOptions MipmapSampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    public SKBitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            if (_bitmap != value)
            {
                _bitmap = value;
                _renderCacheDirty = true;

                UpdateSize(value);
                OnBitmapChanged(_bitmap);
            }

        }
    }

    SKBitmap IBitmapNode.Bitmap => _bitmap!;

    protected virtual void OnBitmapChanged(SKBitmap? newBitmap)
    {
    }

    public void EraseBitmap()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns this node's bitmap, lazily (re)allocating a blank one sized to <see cref="SKNode.Size"/> when it
    /// is missing. A frame sprite can legitimately reach the undo/redo diff-apply path with a null bitmap — e.g.
    /// after a partial session restore, or once its pixels were disposed on unload while the node is still
    /// referenced by the operation history. Throwing "Bitmap is null" there aborts the undo and leaves the
    /// operation stack inconsistent (the op is popped but never pushed to redo), so the user just sees a
    /// repeated error. Rebuilding an empty buffer of the right size lets the diff re-apply and undo keep working;
    /// we still throw only when the size is genuinely unknown (nothing recoverable). Mirrors the lazy creation
    /// in <see cref="OnSizeChanged"/>.
    /// </summary>
    private SKBitmap EnsureBitmap()
    {
        if (_bitmap != null)
            return _bitmap;

        var w = (int)Size.Width;
        var h = (int)Size.Height;
        if (w <= 0 || h <= 0)
            throw new InvalidOperationException("Bitmap is null");

        Bitmap = new SKBitmap(w, h, Pix2DAppSettings.ColorType, SKAlphaType.Premul);
        _bitmap!.Erase(SKColor.Empty);
        return _bitmap!;
    }

    public void SetData(byte[] data)
    {
        var bitmap = EnsureBitmap();

        // Allow empty data - treat as "clear bitmap"
        if (data.Length == 0)
        {
            bitmap.Erase(SKColor.Empty);
            InvalidateBitmap();
            return;
        }

        if (data.Length != bitmap.ByteCount)
        {
            throw new InvalidOperationException(
                $"Size of input data {data.Length} is not equal to the size of the bitmap {bitmap.ByteCount}");
        }

        unsafe
        {
            fixed (byte* pSource = data)
            {
                Buffer.MemoryCopy(pSource, bitmap.GetPixels().ToPointer(), data.Length, data.Length);
            }
        }
        InvalidateBitmap();
    }

    public void HideTargetBitmap()
    {
        this.IsVisible = false;
    }

    public void ShowTargetBitmap()
    {
        this.IsVisible = true;
    }

    public void SetTargetBitmapSubstitute(Func<SKBitmap>? substitute)
    {
        _substitute = substitute;
        // The substitute snapshot (and the bitmap it returns) is reused/refilled in place across strokes, so
        // reference equality alone can't tell its content changed — force a mip rebuild whenever it is set or
        // cleared (stroke start / commit).
        _renderCacheDirty = true;
    }

    public bool IsTargetBitmapVisible()
    {
        return IsVisible;
    }

    public float GetOpacity() => Opacity;
    public SKColor PickColorByPoint(int localPosX, int localPosY) =>
        Bitmap?.GetPixel(localPosX, localPosY) ?? SKColor.Empty;

    public void Draw(Action<SKCanvas> drawAction)
    {
        var bitmap = Bitmap;

        if (bitmap == null)
            return;

        using (var canvas = new SKCanvas(bitmap))
        {
            drawAction?.Invoke(canvas);
            canvas.Flush();
        }
        bitmap.NotifyPixelsChanged();
        _renderCacheDirty = true;
    }

    public void ModifyBitmap(Action<SKBitmap> processAction)
    {
        throw new NotImplementedException();
    }

    public SKSize GetSize()
    {
        return Size;
    }

    public void CopyBitmapTo(SKBitmap workingBitmap)
    {
        throw new NotImplementedException();
    }

    public void Clear()
    {
        throw new NotImplementedException();
    }

    protected void UpdateSize(SKBitmap? value)
    {
        if (value != null)
            Size = new SKSize(value.Width, value.Height);
    }

    protected override void OnSizeChanged()
    {
        if (Bitmap == null)
        {
            Bitmap = new SKBitmap((int)Size.Width, (int)Size.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            Bitmap.Erase(SKColor.Empty);
        }
        Bitmap.NotifyPixelsChanged();

        _bitmapRect = new SKRect(0, 0, Bitmap.Width, Bitmap.Height);
        _nodeRect = new SKRect(0, 0, Size.Width, Size.Height);
        base.OnSizeChanged();
    }

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        var bitmap = _substitute == null ? Bitmap : _substitute();
        if (bitmap == null)
            return;

        // Minified (zoomed-out) draw: when several source texels fall on one screen pixel, replace
        // nearest-point sampling of the full-res bitmap with a cached mipmapped image + trilinear sampling.
        // This applies to the live-drawing substitute too — it is the STABLE pre-stroke layer snapshot, not
        // the moving stroke (that lives on DrawingLayerNode's overlay) — so the active layer no longer "pops"
        // from smooth to aliased the instant a stroke starts; every layer stays uniformly downscaled while
        // drawing. Gated on RenderAdorners so the cache is only ever touched from the interactive compositor
        // pass (single thread); previews/exports run with RenderAdorners == false and keep crisp nearest.
        if (vp.Settings.RenderAdorners
            && MinificationMipmapZoomThreshold > 0
            && vp.DpiEffectiveZoom <= MinificationMipmapZoomThreshold
            && bitmap.Width > 1 && bitmap.Height > 1)
        {
            var image = GetOrCreateMipImage(bitmap);
            if (image != null)
            {
                // The whole image maps to _nodeRect (0,0..Size); the viewport matrix applies the minification.
                canvas.DrawImage(image, _nodeRect, MipmapSampling);
                return;
            }
        }

        if (Math.Abs(Size.Width - bitmap.Width) < 0.1 && Math.Abs(Size.Height - bitmap.Height) < 0.1)
        {
            canvas.DrawBitmap(bitmap, 0, 0);
        }
        else
        {
            canvas.DrawBitmap(bitmap, _bitmapRect, _nodeRect);
        }
    }

    /// <summary>
    /// Returns the cached mipmapped snapshot of <paramref name="bitmap"/>, rebuilding it if the content was
    /// flagged stale. Runs only on the compositor render thread (see <see cref="_mipImage"/>).
    /// <see cref="SKImage.FromBitmap"/> is copy-on-write here (shares the pixel ref), so building it is
    /// allocation-free in the steady state; the mip chain is generated lazily by Skia on first draw and
    /// reused across frames while the cache stays valid.
    /// </summary>
    private SKImage? GetOrCreateMipImage(SKBitmap bitmap)
    {
        // Rebuild when flagged stale, or when the source bitmap instance changed — e.g. switching between the
        // node's own bitmap and the live-drawing substitute snapshot at stroke start/commit.
        if (_renderCacheDirty || !ReferenceEquals(_mipSource, bitmap))
        {
            _mipImage?.Dispose();
            _mipImage = null;
            _renderCacheDirty = false;
        }

        if (_mipImage == null || _mipImage.Handle == IntPtr.Zero)
        {
            _mipImage?.Dispose();
            _mipImage = SKImage.FromBitmap(bitmap);
            _mipSource = bitmap;
        }

        return _mipImage;
    }

    public void ReplaceBitmap(SKBitmap bitmap, bool resetSize = false)
    {
        _bitmap = bitmap;
        if (resetSize) UpdateSize(_bitmap);
        InvalidateBitmap();
    }

    public virtual void InvalidateBitmap()
    {
        _renderCacheDirty = true;
        _bitmap?.NotifyPixelsChanged();
        OnNodeInvalidated();
    }

    /// <summary>
    /// Flags the cached mip image (used by the zoomed-out draw path) stale so the next minified frame
    /// rebuilds it. Cheap and thread-safe — only flips a flag; the dispose/rebuild happens on the render
    /// thread. Call this from code that writes pixels straight into <see cref="Bitmap"/> without going
    /// through this node's own mutators (e.g. the live-drawing commit in <see cref="Pix2dSprite"/>).
    /// </summary>
    public void InvalidateRenderCache() => _renderCacheDirty = true;

    public void MergeFrom(BitmapNode sprite, float opacity = 1)
    {
        if (_bitmap == null) return;
        using (var surface = _bitmap.GetSKSurface())
        {
            var canvas = surface.Canvas;
            var paint = new SKPaint() { Color = SKColors.Black.WithAlpha((byte)(opacity * 255)) };
            canvas.DrawBitmap(sprite.Bitmap!, sprite.GetBoundingBox(), paint);
            canvas.Flush();
        }

        InvalidateBitmap();
    }

    public byte[] GetData()
    {
        return EnsureBitmap().Bytes;
    }
    public void RotateSourceBitmap(bool resize = false)
        => ReplaceBitmap(Bitmap!.Rotate90(), resize);

    public void FlipHorizontal()
        => ReplaceBitmap(Bitmap!.FlipHorizontal());

    public void FlipVertical()
        => ReplaceBitmap(Bitmap!.FlipVertical());

    public void Resize(SKSize newSize)
        => ReplaceBitmap(Bitmap!.Resize(newSize.ToSizeI(), new SKSamplingOptions(SKFilterMode.Nearest)), true);
    public void Resize(SKSize newSize, float horizontalAnchor, float verticalAnchor)
        => ReplaceBitmap(Bitmap!.CropByAnchor(newSize.ToSizeI(), horizontalAnchor, verticalAnchor), true);

    public void Crop(SKRect targetBounds)
        => ReplaceBitmap(Bitmap!.Crop(targetBounds), true);

    // Runtime callback wired up by the editor — a delegate has no meaningful serialized form.
    [JsonIgnore]
    public Action FlushRequestedAction { get; set; } = () => { };
    public bool LockTransparentPixels { get; } = false;

    public virtual SKBitmap GetDrawingBitmap()
    {
        return this.Bitmap ?? throw new InvalidOperationException("Bitmap is null");
    }

    public override void OnUnload()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        _mipImage?.Dispose();
        _mipImage = null;
        _mipSource = null;
        base.OnUnload();
    }

    public void TakeBitmapSubstitute(BitmapNode from)
    {
        _substitute = from._substitute;
        from._substitute = null;
        _renderCacheDirty = true;
        from._renderCacheDirty = true;
    }
}