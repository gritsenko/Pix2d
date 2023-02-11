using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Pix2d.Abstract;
using Pix2d.Abstract.Drawing;
using Pix2d.Abstract.NodeTypes;
using SkiaNodes;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.CommonNodes;

public partial class Pix2dSprite : DrawingContainerBaseNode, IUpdatableNode, IDrawingTarget, IImportTarget, IClippingSource, IAnimatedNode
{
    private int _currentFrameIndex;

    public override bool DoNotRenderChildren => true;


    private float _timeToNextFrame = 0;
    private bool _isPlaying;
    private SKMatrix _adornerTransform;

    [JsonIgnore]
    public SKNodeClipMode ClipMode => SKNodeClipMode.Rect;
    [JsonIgnore]
    public SKRect ClipBounds => LocalBounds;

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

    public bool EditMode { get; set; }

    public float FrameRate { get; set; } = 15;

    public OnionSkinSettings OnionSkinSettings { get; set; } = new OnionSkinSettings();
    public Pix2dSprite()
    {
        DesignerState.ShowChildrenInTree = false;
    }

    [JsonIgnore] public Layer SelectedLayer => GetLayer(SelectedLayerIndex);

    public int SelectedLayerIndex { get; set; }

    public int CurrentFrameIndex => _currentFrameIndex;

    [JsonIgnore]
    public IEnumerable<Layer> Layers => Nodes.OfType<Layer>();

    private Layer GetLayer(int index) => Nodes[index] as Layer;

    public void OnUpdate(float dt)
    {
        if (!IsPlaying || FrameRate < 0.01)
            return;

        _timeToNextFrame -= dt;
        if (_timeToNextFrame <= 0)
        {
            _timeToNextFrame = 1000 / FrameRate;
            SetNextFrame();
        }
    }

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

    public Action FlushRequestedAction { private get; set; }

    public void InvalidateBitmap()
    {
        //SelectedLayer.InvalidateBitmap();
    }

    public void SetData(byte[] data)
    {
        SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        SelectedLayer.SetData(CurrentFrameIndex, data);
    }

    public byte[] GetData()
    {
        var selectedFrame = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        return selectedFrame?.GetData();
    }

    public void HideTargetBitmap()
    {
        this.SelectedLayer.IsVisible = false;
    }

    public void ShowTargetBitmap()
    {
        this.SelectedLayer.IsVisible = true;
    }

    public bool IsTargetBitmapVisible()
    {
        return this.SelectedLayer.IsVisible;
    }

    public float GetOpacity()
    {
        return SelectedLayer.Opacity;
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
        if(SelectedLayer == null)
            return;
        
        SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        var bitmap = sprite?.Bitmap;

        if(bitmap == null)
            return;
        
        using (var canvas = new SKCanvas(bitmap))
        {
            drawAction?.Invoke(canvas);
            canvas.Flush();
        }
        bitmap.NotifyPixelsChanged();
    }

    public void ModifyBitmap(Action<SKBitmap> processAction)
    {
        if(SelectedLayer == null)
            return;

        SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        var bitmap = sprite?.Bitmap;

        if(bitmap == null)
            return;

        processAction?.Invoke(bitmap);
        bitmap.NotifyPixelsChanged();
    }

    public SKSize GetSize()
    {
        return Size;
    }

    public void CopyBitmapTo(SKBitmap targetBitmap)
    {
        var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);

        if (sprite == null || targetBitmap == null)
            return;
        var count = sprite.Bitmap.ByteCount;
        targetBitmap.CopyFrom(sprite.Bitmap);
    }

    public override void RenderRecursive(SKCanvas canvas, ViewPort vp)
    {
        _adornerTransform = canvas.TotalMatrix;
        base.RenderRecursive(canvas, vp);
    }

    public override void OnDraw(SKCanvas canvas, ViewPort vp)
    {
        if (EditMode && vp.Settings.RenderAdorners)
        {
            base.OnDraw(canvas, vp);
            DrawBoundingBox(canvas, vp, 1, SKColors.Gray);
        }

        //RENDER SOLID BACKGROUND
        if (UseBackgroundColor && BackgroundColor != default)
        {
            using (var paint = canvas.GetSolidFillPaint(BackgroundColor))
                //canvas.Clear(BackgroundColor);
                canvas.DrawRect(0, 0, Size.Width, Size.Height, paint);

        }

        var localViewport = new ViewPort((int)Size.Width, (int)Size.Height);


        //RENDER ONION SKINS
        if (OnionSkinSettings.IsEnabled)
        {
            for (var i = 0; i < Nodes.Count; i++)
            {
                var layer = (Layer)Nodes[i];
                var frameIndex = _currentFrameIndex - 1;
                if (frameIndex < 0)
                {
                    frameIndex += GetFramesCount();
                }
                layer.RenderFrame(frameIndex, canvas, vp, 0.3f);
            }
        }

        //var mt = default(SKMatrix);
        //SKMatrix.Concat(ref mt, canvas.TotalMatrix, Transform);
        //canvas.SetMatrix(mt);

        for (var i = 0; i < Nodes.Count; i++)
        {
            //canvas.Save();
            if (Nodes[i].IsVisible)
                Nodes[i].RenderRecursive(canvas, localViewport);

            if (SelectedLayer == Nodes[i])
            {
                //base.RenderAdorner(canvas, vp, _adornerTransform);
            }

            //canvas.Restore();
        }
    }
    
    public void AddEmptyFrame(SKSize size = default)
    {
        if (Nodes.Any())
        {
            foreach (var layer in Layers)
            {
                var newFrameIndex = layer.AddEmptyFrame();
                layer.SetFrame(newFrameIndex);
            }
        }
        else
        {
            AddLayer(size);
        }
    }

    public void UpdateLayerFrameFromBitmap(int frameIndex, int layerIndex, SKBitmap sourceBitmap)
    {
        var layer = Layers.ToArray()[layerIndex];
        layer.EnsureFrameHasUniqueSprite(frameIndex);
        layer.SetData(frameIndex, sourceBitmap.Bytes);
    }

    public void Clear()
    {
        Nodes.Clear();
    }

    public void EraseBitmap()
    {
        SelectedLayer?.ClearFrame(CurrentFrameIndex);
    }

    public void SetSize(SKSize size)
    {
        Resize(size);
    }

    void IImportTarget.AddLayer(LayerPropertiesInfo layerPropertiesInfo, SKSize size)
    {
        var layer = this.AddLayer(size);
        layer.Opacity = layerPropertiesInfo.Opacity;
        layer.BlendMode = layerPropertiesInfo.BlendMode;
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
        layer.CurrentFrameIndex = this.CurrentFrameIndex;

        return layer;
    }

    private string GenerateLayerName(Layer layer)
    {
        return "Layer " + layer.Index.ToString("000");
    }

    public SKBitmap GetFramePreview(int frameIndex, float scale = 1, bool useBackgroundColor = false)
    {
        var bitmap = new SKBitmap(new SKImageInfo((int)(Size.Width * scale), (int)(Size.Height * scale), SKColorType.Bgra8888));
        RenderFramePreview(frameIndex, ref bitmap, scale, useBackgroundColor);
        return bitmap;
    }

    public void RenderFramePreview(int frameIndex, ref SKBitmap targetBitmap, float scale = 1f, bool useBackgroundColor = false)
    {
        var vp = new ViewPort((int)(targetBitmap.Width), (int)(targetBitmap.Height));
        vp.Settings.RenderAdorners = false;

        if (Math.Abs(scale - 1f) > 0.1)
        {
            vp.ShowArea(GetBoundingBox());
        }

        RenderFramePreview(frameIndex, ref targetBitmap, vp, useBackgroundColor);
    }

    public void RenderFramePreview(int frameIndex, ref SKBitmap targetBitmap, ViewPort vp, bool useBackgroundColor = false)
    {
        using (var canvas = new SKCanvas(targetBitmap))
        {
            canvas.Clear(useBackgroundColor ? BackgroundColor : SKColor.Empty);

            foreach (var layer in Layers)
            {
                if (layer.IsVisible)
                    layer.RenderFrame(frameIndex, canvas, vp);
            }
            canvas.Flush();
        }
    }


    public int GetFramesCount()
    {
        return Layers.FirstOrDefault()?.FrameCount ?? 0;
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
        SelectLayer(newSelectedLayer);
    }

    public SKNode DuplicateLayer(Layer layer)
    {
        var layerCopy = layer.Clone();
        this.Nodes.Add(layerCopy);
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

    public void SendLayerBackward(Layer layer)
    {
        var node = layer;
        var parent = node.Parent;
        var index = node.Index;

        var prevNodeIndex = index - 1;
        if (prevNodeIndex < 0)
        {
            return;
        }

        var prevNode = parent.Nodes[prevNodeIndex];
        node.RemoveFromParent();

        var newIndex = prevNode.Index;
        parent.Nodes.Insert(newIndex, node);
    }

    public void BringLayerForward(Layer layer)
    {
        var node = layer;
        if (!CanBringLayerForward(node))
            return;

        var parent = node.Parent;
        var index = node.Index;

        if (node.Index >= parent.Nodes.Count)
            return;

        var nextNodeIndex = index + 1;
        var nextNode = parent.Nodes[nextNodeIndex];
        node.RemoveFromParent();

        var newIndex = nextNode.Index + 1;
        parent.Nodes.Insert(newIndex, node);
    }
    public bool CanBringLayerForward(SKNode node)
    {
        return node.Index + 1 < node.Parent.Nodes.Count;
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
        if(!cancelRequestedAction)
            FlushRequestedAction?.Invoke();
        SelectedLayerIndex = layer.Index;
    }

    public void SetFrameIndex(int index) => SetFrameIndex(index, false);
    public void SetFrameIndex(int index, bool cancelRequestedAction)
    {
        //CurrentFrameIndex = index;
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

    public override void Crop(SKRect targetBounds)
    {
        this.Size = targetBounds.Size;
        foreach (var layer in Layers) layer.Crop(targetBounds);
    }

    public void SetEditMode(bool enabled)
    {
        this.EditMode = enabled;
    }

    public IDrawingTarget GetDrawingTarget()
    {
        return this;
        //if (SelectedLayer == null)
        //    return null;

        //SelectedLayer.EnsureFrameHasUniqueSprite(CurrentFrameIndex);
        //var sprite = SelectedLayer.GetSpriteByFrame(CurrentFrameIndex);
        //return sprite;
    }
}