namespace Pix2d.Abstract.Operations;

public interface ISpriteEditorOperation : IEditOperation
{
    HashSet<int> AffectedFrameIndexes { get; }
    HashSet<int> AffectedLayerIndexes { get; }
}

public class AffectedNodeIndexes : HashSet<int>
{
    public static AffectedNodeIndexes All = new AffectedNodeIndexes(){};
    public AffectedNodeIndexes()
    {
    }
}