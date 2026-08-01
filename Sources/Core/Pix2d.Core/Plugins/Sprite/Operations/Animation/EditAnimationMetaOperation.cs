using Pix2d.CommonNodes;
using Pix2d.Operations;
using SkiaNodes;
using SkiaNodes.Common;

namespace Pix2d.Plugins.Sprite.Operations;

/// <summary>
/// One undo step for any edit to a sprite's animation metadata — creating / renaming / re-ranging /
/// deleting a tag, setting or clearing a frame duration, moving the export pivot, changing 9-slice
/// margins. Every authoring gesture in the animation-properties UI funnels through this, so one gesture
/// is one undo step.
///
/// <para>Modelled on <see cref="ChangeNodePropertyOperationBase{TValue}"/>'s capture-mutate-commit shape
/// (as used by <c>SpriteEditor.ToggleLayerVisible</c>): construct it, mutate the sprite, call
/// <see cref="SetFinalData"/>, push it. Deliberately <b>not</b> an <c>ISpriteEditorOperation</c> — no
/// pixels change, so frame/layer thumbnails must not be invalidated; the timeline refreshes off this
/// operation's own type instead.</para>
/// </summary>
public class EditAnimationMetaOperation : EditOperationBase
{
    private readonly Pix2dSprite _sprite;
    private readonly SpriteAnimationMetaSnapshot _initial;
    private SpriteAnimationMetaSnapshot _final;

    public override bool AffectsNodeStructure => false;

    public EditAnimationMetaOperation(Pix2dSprite sprite)
    {
        _sprite = sprite;
        _initial = SpriteAnimationMetaSnapshot.Capture(sprite);
        _final = _initial;
    }

    /// <summary>Captures the post-edit state. Call after mutating the sprite, before pushing.</summary>
    public void SetFinalData() => _final = SpriteAnimationMetaSnapshot.Capture(_sprite);

    public override void OnPerform() => _final.Restore(_sprite);

    public override void OnPerformUndo() => _initial.Restore(_sprite);

    public override IEnumerable<SKNode> GetEditedNodes() => _sprite.Yield();
}
