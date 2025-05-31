using System;
using System.Collections.Generic;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;

namespace Pix2d.Plugins.Sprite.Operations;

public class EditFrameOperation : EditOperationBase
{
    private readonly int _frameIndex;
    private readonly Pix2dSprite _sprite;
    private readonly Dictionary<Guid, byte[]> _initialData;
    private Dictionary<Guid, byte[]> _finalData;

    public EditFrameOperation(Pix2dSprite targetSprite)
    {
        _frameIndex = targetSprite.CurrentFrameIndex;
        _sprite = targetSprite;
        _initialData = LayersData();
    }

    private Dictionary<Guid, byte[]> LayersData()
    {
        var data = new Dictionary<Guid, byte[]>();
        foreach (var layer in _sprite.Layers)
        {
            var bytes = layer.GetSpriteByFrame(_frameIndex)?.GetData();
            if (bytes != null)
            {
                data[layer.Id] = bytes;
            }
        }

        return data;
    }

    public void SetFinalData()
    {
        _finalData = LayersData();
    }

    public override void OnPerform()
    {
        SetLayersData(_finalData);
    }

    public override void OnPerformUndo()
    {
        SetLayersData(_initialData);
    }

    private void SetLayersData(IReadOnlyDictionary<Guid, byte[]> layersData)
    {
        foreach (var layer in _sprite.Layers)
        {
            if (layersData.TryGetValue(layer.Id, out var value))
            {
                layer.SetData(_frameIndex, value);
            }
            else
            {
                layer.ClearFrame(_frameIndex);
            }
        }
    }

    public override IEnumerable<SKNode> GetEditedNodes()
    {
        return _sprite.Layers.Select(x => x.GetSpriteByFrame(_frameIndex));
    }
}