namespace Pix2d.Abstract.Operations;

public interface ISpriteEditorOperation : IEditOperation
{
    HashSet<int> AffectedFrameIndexes { get; }
    HashSet<int> AffectedLayerIndexes { get; }
}