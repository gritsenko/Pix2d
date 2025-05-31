namespace Pix2d.Modules.Sprite.Editors;

public class FramesChangedEventArgs
{
    public readonly FramesChangedType ChangeType;
    public readonly int[] AffectedIndexes;
    public FramesChangedEventArgs(FramesChangedType changeType, int[] affectedIndexes)
    {
            ChangeType = changeType;
            AffectedIndexes = affectedIndexes;
        }
}