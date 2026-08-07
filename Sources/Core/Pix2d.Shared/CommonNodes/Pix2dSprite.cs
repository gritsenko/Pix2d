using Newtonsoft.Json;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using Pix2d.Primitives;
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

    #region Animation metadata (tags, per-frame durations, export anchors)

    // All four are optional and null by default, so an untouched sprite serializes exactly as before
    // (NodeSerializer uses NullValueHandling.Ignore) and an old file simply leaves them unset. That is
    // also why PR-3 needs no format-version bump: the additions are reader-tolerant both ways.

    /// <summary>
    /// Named animation ranges ("idle", "run", …) over this sprite's shared timeline. Null or empty when
    /// the sprite has none. Kept consistent with the frame list by the <c>ShiftAnimationMetaOn*</c>
    /// helpers below — every frame add/duplicate/delete/reorder operation calls one of them.
    /// </summary>
    public List<SpriteAnimationTag>? AnimationTags { get; set; }

    /// <summary>
    /// Per-frame duration overrides in milliseconds, indexed by frame. <c>0</c> — or any index past the
    /// end of the list — means "use <see cref="DefaultFrameDurationMs"/>", so the list is sparse by
    /// convention: it is trimmed of trailing zeros and nulled when everything is default. This lives on
    /// the sprite, not on <see cref="Layer.Frames"/>: the timeline is shared by every layer, and storing
    /// a duration per layer would both duplicate it N times and desync on layer merge/duplicate.
    /// </summary>
    public List<int>? FrameDurations { get; set; }

    /// <summary>
    /// Export anchor in unscaled canvas pixels (origin = the artboard's top-left). Null = unset.
    /// Distinct from <see cref="SKNode.PivotPosition"/>, which is the transform/rotation origin —
    /// this one is document metadata and only ever affects exported <c>meta.slices</c>.
    /// </summary>
    public SKPoint? ExportPivot { get; set; }

    /// <summary>9-slice margins in unscaled canvas pixels. Null = unset.</summary>
    public NineSliceMargins? NineSlice { get; set; }

    /// <summary>
    /// Duration of a frame that carries no override, derived from <see cref="FrameRate"/>. Changing the
    /// frame rate therefore re-times every non-overridden frame and leaves overrides alone.
    /// </summary>
    [JsonIgnore]
    public int DefaultFrameDurationMs => (int)Math.Round(1000f / Math.Max(1f, FrameRate));

    /// <summary>Effective duration of <paramref name="frameIndex"/>: its override, else the default.</summary>
    public int GetFrameDurationMs(int frameIndex)
    {
        if (FrameDurations == null || frameIndex < 0 || frameIndex >= FrameDurations.Count)
            return DefaultFrameDurationMs;

        var value = FrameDurations[frameIndex];
        return value > 0 ? value : DefaultFrameDurationMs;
    }

    /// <summary>True when <paramref name="frameIndex"/> has an explicit duration rather than the default.</summary>
    public bool HasFrameDurationOverride(int frameIndex)
        => FrameDurations != null && frameIndex >= 0 && frameIndex < FrameDurations.Count
                                  && FrameDurations[frameIndex] > 0;

    /// <summary>
    /// Sets (or with null/0 clears) a frame's duration override. Values are clamped to a sane 1..60000 ms
    /// so a stray input can't stall playback, and the backing list is re-trimmed so clearing the last
    /// override leaves the sprite byte-identical to one that never had any.
    /// </summary>
    public void SetFrameDurationMs(int frameIndex, int? milliseconds)
    {
        if (frameIndex < 0)
            return;

        var value = milliseconds is > 0 ? Math.Clamp(milliseconds.Value, 1, 60000) : 0;

        if (value == 0 && (FrameDurations == null || frameIndex >= FrameDurations.Count))
            return; // clearing something that is already default

        FrameDurations ??= [];
        while (FrameDurations.Count <= frameIndex)
            FrameDurations.Add(0);

        FrameDurations[frameIndex] = value;
        TrimFrameDurations();
    }

    private void TrimFrameDurations()
    {
        if (FrameDurations == null)
            return;

        var last = FrameDurations.FindLastIndex(d => d > 0);
        if (last < 0)
        {
            FrameDurations = null;
            return;
        }

        if (last < FrameDurations.Count - 1)
            FrameDurations.RemoveRange(last + 1, FrameDurations.Count - 1 - last);
    }

    /// <summary>
    /// Re-indexes the animation metadata after a frame was inserted at <paramref name="index"/>.
    ///
    /// <para>Tag rule: an endpoint at or after the insertion point moves right. The consequence is that
    /// inserting <i>inside</i> a range extends it (the new frame belongs to the animation the user was
    /// working in) while inserting at or before <c>From</c> shifts the whole range. Appending past the
    /// last frame (<c>AddFrameAtEnd</c>) touches nothing.</para>
    ///
    /// <para><paramref name="inheritDurationFromIndex"/> copies an existing frame's override onto the new
    /// frame — a duplicated slow frame stays slow.</para>
    /// </summary>
    public void ShiftAnimationMetaOnInsert(int index, int? inheritDurationFromIndex = null)
    {
        if (index < 0)
            return;

        if (AnimationTags != null)
        {
            foreach (var tag in AnimationTags)
            {
                if (tag.From >= index) tag.From++;
                if (tag.To >= index) tag.To++;
            }
        }

        var inherited = inheritDurationFromIndex is { } src && HasFrameDurationOverride(src)
            ? FrameDurations![src]
            : 0;

        if (FrameDurations != null && index <= FrameDurations.Count)
            FrameDurations.Insert(index, inherited);
        else if (inherited > 0)
            SetFrameDurationMs(index, inherited);

        TrimFrameDurations();
        NormalizeAnimationTags();
    }

    /// <summary>
    /// Re-indexes the animation metadata after the frame at <paramref name="index"/> was deleted.
    /// A range that covered only that frame is dropped — this is the step that makes the metadata
    /// non-invertible, which is why the operations snapshot rather than try to undo it arithmetically.
    /// </summary>
    public void ShiftAnimationMetaOnDelete(int index)
    {
        if (index < 0)
            return;

        if (AnimationTags != null)
        {
            foreach (var tag in AnimationTags)
            {
                if (tag.From > index) tag.From--;
                if (tag.To >= index) tag.To--;
            }
        }

        if (FrameDurations != null && index < FrameDurations.Count)
            FrameDurations.RemoveAt(index);

        TrimFrameDurations();
        NormalizeAnimationTags();
    }

    /// <summary>
    /// Re-indexes the animation metadata after a frame moved from one index to another. Expressed as a
    /// delete followed by an insert, which is exactly how the frames themselves slid, so a move entirely
    /// inside one tag leaves that tag alone.
    /// </summary>
    public void ShiftAnimationMetaOnMove(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex || fromIndex < 0 || toIndex < 0)
            return;

        var moved = HasFrameDurationOverride(fromIndex) ? FrameDurations![fromIndex] : 0;

        // A tag covering only the moved frame has to follow it. The delete-then-insert composition
        // below collapses such a tag to zero length and drops it, so carry those across by hand.
        var followers = AnimationTags?.Where(t => t.From == fromIndex && t.To == fromIndex).ToList();

        ShiftAnimationMetaOnDelete(fromIndex);
        ShiftAnimationMetaOnInsert(toIndex);

        if (followers is { Count: > 0 })
        {
            AnimationTags ??= [];
            foreach (var tag in followers)
            {
                tag.From = tag.To = toIndex;
                if (!AnimationTags.Contains(tag))
                    AnimationTags.Add(tag);
            }

            NormalizeAnimationTags();
        }

        if (moved > 0)
            SetFrameDurationMs(toIndex, moved);
    }

    /// <summary>
    /// Clamps every tag into the current frame range and drops the ones that no longer address any
    /// frame. Called after each shift, and by <c>SceneIntegrity</c> on load so the editor and the
    /// exporter can trust the invariant instead of each re-deriving it.
    /// </summary>
    public void NormalizeAnimationTags()
    {
        if (AnimationTags == null)
            return;

        var lastFrame = GetFramesCount() - 1;
        if (lastFrame < 0)
        {
            AnimationTags = null;
            return;
        }

        // Order matters: drop first, clamp second. Clamping first would resurrect the very tags that
        // must die — a range collapsed to zero length by a delete (To == From - 1) clamps back to a
        // valid single frame, and a range that fell entirely off the end (7..9 on a 1-frame sprite)
        // clamps onto frame 0, silently re-tagging content it never covered.
        AnimationTags.RemoveAll(t => t.To < t.From || t.From > lastFrame || t.To < 0);

        foreach (var tag in AnimationTags)
        {
            tag.From = Math.Clamp(tag.From, 0, lastFrame);
            tag.To = Math.Clamp(tag.To, 0, lastFrame);
        }

        if (AnimationTags.Count == 0)
            AnimationTags = null;
    }

    #endregion

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

    /// <summary>
    /// True when a layer still carries the auto-generated title (see <see cref="GenerateLayerName"/>),
    /// i.e. the user never named it. The UI uses this to keep the layer tiles clean — a caption reading
    /// "Layer 003" tells nobody anything, so only real names are shown.
    ///
    /// Matches the *pattern*, not the name a layer would get right now: indexes shift on reorder, so a
    /// layer created as "Layer 002" can legitimately sit at index 0 and is still unnamed.
    /// </summary>
    public static bool IsGeneratedLayerName(string? name)
        => string.IsNullOrWhiteSpace(name) || GeneratedLayerNameRegex.IsMatch(name.Trim());

    private static readonly System.Text.RegularExpressions.Regex GeneratedLayerNameRegex =
        new(@"^Layer\s+\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase
                              | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

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
        // Every "new artboard" path funnels through here (File > New, Ctrl+T, add artboard, import as
        // artboards), and a caller-supplied size can be degenerate — a failed/degenerate image decode
        // reports Size 0x0. A 0x0 sprite reaches the editor as a canvas nothing can draw on, so clamp
        // at the single creation choke point. See CanvasSize.
        var sprite = new Pix2dSprite();
        sprite.Size = CanvasSize.Sanitize(size);
        sprite.AddLayer(sprite.Size);
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

    // The three canvas-geometry mutators below all clamp through CanvasSize: they are reachable from
    // undo/redo replay and from the artboard Resize/Crop sub-mode, which build bounds from a drag and
    // bypass the min-size guard SpriteEditor.Crop applies to the interactive path.
    public override void Resize(SKSize newSize, float horizontalAnchor = 0f, float verticalAnchor = 0f)
    {
        newSize = CanvasSize.Sanitize(newSize);
        this.Size = newSize;
        foreach (var layer in Layers)
        {
            layer.Resize(newSize, horizontalAnchor, verticalAnchor);
        }
    }

    public void ResizeImage(SKSize newSize)
    {
        newSize = CanvasSize.Sanitize(newSize);
        this.Size = newSize;
        foreach (var layer in Layers)
        {
            layer.ResizeImage(newSize);
        }
    }

    public override void Crop(SKRect targetBounds)
    {
        targetBounds = CanvasSize.Sanitize(targetBounds);
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