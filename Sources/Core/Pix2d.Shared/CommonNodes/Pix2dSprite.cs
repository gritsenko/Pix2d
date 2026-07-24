using Newtonsoft.Json;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public partial class Pix2dSprite : DrawingContainerBaseNode, IDrawingTarget, IClippingSource, IAnimatedNode
{
    private int _currentFrameIndex;
    private bool _isPlaying;

    [JsonIgnore]
    public new SKNodeClipMode ClipMode => SKNodeClipMode.Rect;
    [JsonIgnore]
    public new SKRect ClipBounds => LocalBounds;

    [JsonIgnore]
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            FlushRequestedAction?.Invoke();
        }
    }

    public bool LockTransparentPixels => SelectedLayer?.LockTransparentPixels ?? false;

    [JsonIgnore]
    public bool EditMode { get; set; }

    public float FrameRate { get; set; } = 15;

    public OnionSkinSettings OnionSkinSettings { get; set; } = new OnionSkinSettings();
    public Pix2dSprite()
    {
        DesignerState.ShowChildrenInTree = false;
    }

    [JsonIgnore] public Layer? SelectedLayer => GetLayer(SelectedLayerIndex);

    public int SelectedLayerIndex { get; set; }

    public int CurrentFrameIndex => _currentFrameIndex;

    [JsonIgnore]
    public IEnumerable<Layer> Layers => Nodes.OfType<Layer>();

    // Bounds-checked: SelectedLayerIndex can briefly outrun Nodes during a layer add/remove while the
    // layers list is being rebuilt (LayerItemView virtualization reads SelectedLayer mid-rebuild), which
    // otherwise throws ArgumentOutOfRangeException. Every caller already treats null as "no layer".
    private Layer? GetLayer(int index) => index >= 0 && index < Nodes.Count ? Nodes[index] as Layer : null;

    // The playback timer reads this every tick; GetFramesCount() is 0 for a sprite with no layers
    // (mid-load / after the last layer is removed), which would make the modulo throw DivideByZero.
    public int NextFrameIndex => GetFramesCount() is var count && count > 0 ? (CurrentFrameIndex + 1) % count : 0;

    public void SetNextFrame(bool cycled = true)
    {
        var newFrameIndex = CurrentFrameIndex + 1;
        var maxFrame = GetFramesCount() - 1;
        if (newFrameIndex > maxFrame)
        {
            newFrameIndex = cycled ? 0 : maxFrame;
        }

        SetFrameIndex(newFrameIndex);
        //CurrentFrameIndex = newFrameIndex;
    }

    public void SetPrevFrame(bool cycled = true)
    {
        var newFrameIndex = CurrentFrameIndex - 1;
        if (newFrameIndex < 0)
            newFrameIndex = cycled ? GetFramesCount() - 1 : 0;

        //CurrentFrameIndex = newFrameIndex;
        SetFrameIndex(newFrameIndex);
    }

    [JsonIgnore]
    public Action FlushRequestedAction { private get; set; } = () => { };

    public void SetData(byte[] data)
    {
        SelectedLayer?.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        SelectedLayer?.SetData(CurrentFrameIndex, data);
    }

    public byte[] GetData()
    {
        var selectedFrame = SelectedLayer?.GetSpriteByFrame(CurrentFrameIndex);
        return selectedFrame?.GetData() ?? Array.Empty<byte>();
    }

    public void HideTargetBitmap()
    {
        this.SelectedLayer?.HideFrame(CurrentFrameIndex);
    }

    public void ShowTargetBitmap()
    {
        this.SelectedLayer?.ShowFrame(CurrentFrameIndex);
    }

    public void SetTargetBitmapSubstitute(Func<SKBitmap>? substitute)
    {
        SelectedLayer?.GetSpriteByFrame(CurrentFrameIndex)?.SetTargetBitmapSubstitute(substitute);
    }

    public bool IsTargetBitmapVisible()
    {
        return this.SelectedLayer?.IsVisible ?? false;
    }

    public float GetOpacity()
    {
        return SelectedLayer?.Opacity ?? 1f;
    }

    public SKColor PickColorByPoint(int localPosX, int localPosY)
    {
        if (localPosX < 0 || localPosY < 0 || localPosX >= Size.Width || localPosY >= Size.Height)
            return default;

        var renderedFrame = GetFramePreview(CurrentFrameIndex, 1f, false);
        return renderedFrame.GetPixel(localPosX, localPosY);
    }

    public void Draw(Action<SKCanvas> drawAction)
    {
        if (SelectedLayer == null)
            return;

        SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        var bitmap = sprite?.Bitmap;

        if (bitmap == null)
            return;

        using (var canvas = new SKCanvas(bitmap))
        {
            drawAction?.Invoke(canvas);
            canvas.Flush();
        }
        bitmap.NotifyPixelsChanged();
        // We wrote into the frame sprite's bitmap behind its back — drop its zoomed-out mip cache so the
        // next minified frame rebuilds from the new pixels instead of showing the pre-stroke snapshot.
        sprite!.InvalidateRenderCache();
    }

    public void ModifyBitmap(Action<SKBitmap> processAction)
    {
        if (SelectedLayer == null)
            return;

        SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        var bitmap = sprite?.Bitmap;

        if (bitmap == null)
            return;

        processAction?.Invoke(bitmap);
        bitmap.NotifyPixelsChanged();
        sprite!.InvalidateRenderCache();
    }

    public SKSize GetSize()
    {
        return Size;
    }

    public void CopyBitmapTo(SKBitmap targetBitmap)
    {
        var sprite = SelectedLayer?.GetSpriteByFrame(CurrentFrameIndex);

        if (sprite == null || targetBitmap == null)
            return;
        var count = sprite!.Bitmap!.ByteCount;
        targetBitmap.CopyFrom(sprite.Bitmap);
    }

    /// <summary>Accent border drawn around the active artboard so the user can tell which one is edited.</summary>
    private static readonly SKColor ActiveArtboardHighlightColor = new(0x29, 0xB0, 0xF3);

    protected override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        // Always render the base content (checkerboard / background). Additionally, when this sprite is the
        // active edit target, draw a thin highlight border. RenderAdorners is false for previews/exports,
        // so the border never leaks into thumbnails or exported images.
        base.OnDraw(canvas, vp);

        if (EditMode && vp.Settings.RenderAdorners)
            DrawBoundingBox(canvas, vp, 2, ActiveArtboardHighlightColor);
    }

    //public override void RenderRecursive(SKCanvas canvas, ViewPort vp)
    //{
    //    _adornerTransform = canvas.TotalMatrix;
    //    base.RenderRecursive(canvas, vp);
    //}

    //protected internal override void OnDraw(SKCanvas canvas, ViewPort vp)
    //{
    //    if (EditMode && vp.Settings.RenderAdorners)
    //    {
    //        base.OnDraw(canvas, vp);
    //        DrawBoundingBox(canvas, vp, 1, SKColors.Gray);
    //    }

    //    //RENDER SOLID BACKGROUND
    //    if (UseBackgroundColor && BackgroundColor != default)
    //    {
    //        using var paint = canvas.GetSolidFillPaint(BackgroundColor);
    //        canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);
    //    }

    //    var localViewport = new ViewPort((int)Size.Width, (int)Size.Height);


    //    //RENDER ONION SKINS
    //    if (OnionSkinSettings.IsEnabled && vp.Settings.RenderAdorners)
    //    {
    //        for (var i = 0; i < Nodes.Count; i++)
    //        {
    //            var layer = (Layer)Nodes[i];
    //            var frameIndex = _currentFrameIndex - 1;
    //            if (frameIndex < 0)
    //            {
    //                frameIndex += GetFramesCount();
    //            }
    //            layer.RenderFrame(frameIndex, canvas, vp, 0.3f);
    //        }
    //    }

    //    //var mt = default(SKMatrix);
    //    //SKMatrix.Concat(ref mt, canvas.TotalMatrix, Transform);
    //    //canvas.SetMatrix(mt);

    //    for (var i = 0; i < Nodes.Count; i++)
    //    {
    //        //canvas.Save();
    //        if (Nodes[i].IsVisible)
    //            Nodes[i].RenderRecursive(canvas, localViewport);

    //        if (SelectedLayer == Nodes[i])
    //        {
    //            //base.RenderAdorner(canvas, vp, _adornerTransform);
    //        }

    //        //canvas.Restore();
    //    }
    //}

    public void UpdateLayerFrameFromBitmap(int frameIndex, int layerIndex, SKBitmap sourceBitmap)
    {
        var layer = Layers.ToArray()[layerIndex];
        layer.EnsureFrameHasUniqueSprite(frameIndex);
        layer.SetData(frameIndex, sourceBitmap.Bytes);
    }

    public void EraseBitmap()
    {
        SelectedLayer?.ClearFrame(CurrentFrameIndex);
    }

    public Layer AddLayer(SKSize size = default)
    {
        //new Pix2d sprite doesn't have size yet
        if (Size == default && size != default)
        {
            Size = size;
        }
        else if (size == default && Size != default)
        {
            size = Size;
        }

        var frameCount = Layers.FirstOrDefault()?.FrameCount ?? 1;

        var layer = new Layer(size, frameCount);
        this.Nodes.Add(layer);
        layer.Name = GenerateLayerName(layer);
        SelectLayer(layer);
        layer.SetFrame(this.CurrentFrameIndex);

        return layer;
    }

    private string GenerateLayerName(Layer layer)
    {
        return "Layer " + layer.Index.ToString("000");
    }

    public SKBitmap GetFramePreview(int frameIndex, float scale = 1, bool useBackgroundColor = false)
    {
        var bitmap = new SKBitmap(new SKImageInfo((int)(Size.Width * scale), (int)(Size.Height * scale),
            Pix2DAppSettings.ColorType));
        RenderFramePreview(frameIndex, ref bitmap, scale, useBackgroundColor);
        return bitmap;
    }

    public void RenderFramePreview(int frameIndex, ref SKBitmap targetBitmap, float scale = 1f, bool useBackgroundColor = false)
    {
        var vp = new ViewPort((int)(targetBitmap.Width), (int)(targetBitmap.Height));
        vp.Settings.RenderAdorners = false;

        if (Math.Abs(scale - 1f) > 0.1)
        {
            // Layers paint their frames in the sprite's LOCAL space (SKNodeRenderer.Render keeps only the
            // node's local transform). Fitting the viewport to the GLOBAL bounding box would shift the
            // preview by the artboard's scene offset, so off-origin artboards render displaced. Use local
            // bounds so the preview area matches what actually gets drawn.
            vp.ShowArea(LocalBounds);
        }

        RenderFramePreview(frameIndex, ref targetBitmap, vp, useBackgroundColor);
    }

    public void RenderFramePreview(int frameIndex, ref SKBitmap targetBitmap, ViewPort vp, bool useBackgroundColor = false)
    {
        using var canvas = new SKCanvas(targetBitmap);
        canvas.Clear(useBackgroundColor ? BackgroundColor : SKColor.Empty);

        foreach (var layer in Layers)
        {
            if (layer.IsVisible)
                layer.RenderFrame(frameIndex, canvas, vp, renderHidden: true);
        }
        canvas.Flush();
    }


    public int GetFramesCount()
    {
        var firstLayer = Layers.FirstOrDefault();
        // Legacy .pix2d files store frames as raw child nodes with an empty Frames list; the frame
        // metadata is rebuilt lazily only on first frame *access* (GetFrameByIndex). Counting never
        // triggered that, so headless callers (CLI / SpriteSheetBuilder) that count before accessing
        // saw 0 frames and produced empty sheets. Ensure init here so the count is correct on load.
        firstLayer?.EnsureFramesInitialized();
        return firstLayer?.FrameCount ?? 0;
    }

    public static Pix2dSprite CreateEmpty(SKSize size)
    {
        var sprite = new Pix2dSprite();
        sprite.Size = size;
        sprite.AddLayer(size);
        return sprite;
    }
    public static Pix2dSprite CreateFromBitmap(SKBitmap source)
    {
        var sprite = new Pix2dSprite();
        var size = new SKSize(source.Width, source.Height);
        sprite.Size = size;
        sprite.AddLayer(size);
        sprite.UpdateLayerFrameFromBitmap(0, 0, source);
        return sprite;
    }

    public void DeleteLayer(Layer layer)
    {
        var index = layer.Index;
        this.Nodes.Remove(layer);
        var newIndex = Math.Max(0, index - 1);
        var newSelectedLayer = GetLayer(newIndex);
        if (newSelectedLayer != null)
            SelectLayer(newSelectedLayer);
    }

    public SKNode DuplicateLayer(Layer layer, int insertIndex = -1)
    {
        var layerCopy = layer.Copy();
        this.Nodes.Insert(insertIndex, layerCopy);
        SelectLayer(layerCopy);

        return layerCopy;
    }

    public void MergeDownLayer(Layer layer, bool deleteSource = true)
    {
        var bottomLayer = GetLayer(layer.Index - 1);

        if (bottomLayer != null)
        {
            bottomLayer.MergeFrom(layer);

            if (deleteSource)
                DeleteLayer(layer);
        }
    }

    public bool CanMergeDownLayer(Layer layer)
    {
        if (layer.Index == 0)
            return false;

        if (Layers.Count() < 2)
            return false;

        return true;
    }

    public void SelectLayer(Layer layer) => SelectLayer(layer, false);
    public void SelectLayer(Layer layer, bool cancelRequestedAction)
    {
        if (!cancelRequestedAction)
            FlushRequestedAction?.Invoke();
        SelectedLayerIndex = layer.Index;
    }

    public void SetFrameIndex(int index) => SetFrameIndex(index, false);
    public void SetFrameIndex(int index, bool cancelRequestedAction)
    {
        // Clamp at the single entry point that mutates the sprite's frame pointer. Indexes arrive from the
        // timeline view-models, undo operations and the playback timer, any of which can be one edit behind
        // the model (e.g. a frame deleted while the timeline still selects it). Every drawing path reads
        // CurrentFrameIndex and forwards it to Layer.Frames[..], so a stale value used to be fatal.
        var framesCount = GetFramesCount();
        index = framesCount == 0 ? 0 : Math.Clamp(index, 0, framesCount - 1);

        if (_currentFrameIndex != index)
        {
            if (!cancelRequestedAction && !IsPlaying)
                FlushRequestedAction?.Invoke();

            _currentFrameIndex = index;
        }

        foreach (var layer in Layers)
        {
            layer.SetFrame(_currentFrameIndex);
        }

    }

    public override void Resize(SKSize newSize, float horizontalAnchor = 0f, float verticalAnchor = 0f)
    {
        this.Size = newSize;
        foreach (var layer in Layers)
        {
            layer.Resize(newSize, horizontalAnchor, verticalAnchor);
        }
    }

    public void ResizeImage(SKSize newSize)
    {
        this.Size = newSize;
        foreach (var layer in Layers)
        {
            layer.ResizeImage(newSize);
        }
    }

    public override void Crop(SKRect targetBounds)
    {
        this.Size = targetBounds.Size;
        foreach (var layer in Layers) layer.Crop(targetBounds);
    }

    public void SetEditMode(bool enabled)
    {
        this.EditMode = enabled;
        if (enabled)
        {
            InvalidateLayersAndFrames();
        }
    }

    /// <summary>
    /// Used for fixing invalid offsets on children nodes
    /// also for ensure that layer frame index is equal to sprite frame index
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void InvalidateLayersAndFrames()
    {
        foreach (var layer in Layers)
        {
            if (layer.Position != SKPoint.Empty)
                layer.Position = SKPoint.Empty;

            layer.SetFrame(CurrentFrameIndex);
        }
    }

    public void InvalidateFrames()
    {
        foreach (var layer in Layers)
        {
            layer.EnsureFramesInitialized();
        }
    }
}