using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;

namespace Pix2d.CommonNodes;

/// <summary>Playback order of an animation tag's frame range. Values match Aseprite's own directions.</summary>
public enum SpriteAnimationDirection
{
    Forward = 0,
    Reverse = 1,
    PingPong = 2,
    PingPongReverse = 3
}

/// <summary>
/// A named animation range inside one sprite's timeline — "idle" = frames 0..3, "run" = 4..11.
/// Exported as an Aseprite <c>meta.frameTags</c> entry, which is what lets an engine importer pull a
/// single animation out of a shared sheet.
///
/// <para><b>Invariant</b> (held by <see cref="Pix2dSprite"/>'s shift helpers, enforced again on load by
/// <c>SceneIntegrity</c>): <c>0 &lt;= From &lt;= To &lt; GetFramesCount()</c>. Both ends are inclusive.</para>
/// </summary>
public class SpriteAnimationTag
{
    public string Name { get; set; } = "";

    /// <summary>First frame of the range, inclusive.</summary>
    public int From { get; set; }

    /// <summary>Last frame of the range, inclusive.</summary>
    public int To { get; set; }

    public SpriteAnimationDirection Direction { get; set; } = SpriteAnimationDirection.Forward;

    /// <summary>Frame count covered by the range.</summary>
    public int Length => To - From + 1;

    public bool Covers(int frameIndex) => frameIndex >= From && frameIndex <= To;

    public SpriteAnimationTag Copy() => new() { Name = Name, From = From, To = To, Direction = Direction };

    /// <summary>The Aseprite JSON spelling of <see cref="Direction"/>.</summary>
    public string GetDirectionKey() => Direction switch
    {
        SpriteAnimationDirection.Reverse => "reverse",
        SpriteAnimationDirection.PingPong => "pingpong",
        SpriteAnimationDirection.PingPongReverse => "pingpong_reverse",
        _ => "forward"
    };
}

/// <summary>
/// 9-slice margins in unscaled canvas pixels, measured inward from each edge. Exported as the
/// <c>center</c> rect of an Aseprite slice key: <c>(Left, Top, W-Left-Right, H-Top-Bottom)</c>.
/// </summary>
public class NineSliceMargins
{
    public int Left { get; set; }
    public int Top { get; set; }
    public int Right { get; set; }
    public int Bottom { get; set; }

    public NineSliceMargins Copy() => new() { Left = Left, Top = Top, Right = Right, Bottom = Bottom };

    /// <summary>True when the margins leave a non-empty centre rect inside a canvas of this size.</summary>
    public bool FitsIn(float width, float height)
        => Left >= 0 && Top >= 0 && Right >= 0 && Bottom >= 0
           && Left + Right < width && Top + Bottom < height;
}

/// <summary>
/// A deep copy of every piece of animation metadata on one sprite, used by
/// <c>EditAnimationMetaOperation</c> and by the frame add/duplicate/delete/reorder operations to make
/// undo exact.
///
/// <para><b>Why a snapshot and not an inverse shift.</b> Deleting a frame is not an invertible
/// transform of the metadata: a single-frame tag whose only frame is deleted is dropped entirely, and
/// that frame's duration override is gone with it. Recomputing either on undo is impossible, and
/// hand-written inverse index arithmetic is exactly the kind of code that produced the
/// <c>ArgumentOutOfRangeException</c> family already documented in the timeline operations. Capturing
/// before the mutation and restoring wholesale is both shorter and total.</para>
/// </summary>
public readonly struct SpriteAnimationMetaSnapshot
{
    private readonly List<SpriteAnimationTag>? _tags;
    private readonly List<int>? _durations;
    private readonly SKPoint? _pivot;
    private readonly NineSliceMargins? _nineSlice;

    private SpriteAnimationMetaSnapshot(
        List<SpriteAnimationTag>? tags, List<int>? durations, SKPoint? pivot, NineSliceMargins? nineSlice)
    {
        _tags = tags;
        _durations = durations;
        _pivot = pivot;
        _nineSlice = nineSlice;
    }

    public static SpriteAnimationMetaSnapshot Capture(Pix2dSprite sprite) =>
        new(sprite.AnimationTags?.Select(t => t.Copy()).ToList(),
            sprite.FrameDurations?.ToList(),
            sprite.ExportPivot,
            sprite.NineSlice?.Copy());

    public void Restore(Pix2dSprite sprite)
    {
        sprite.AnimationTags = _tags?.Select(t => t.Copy()).ToList();
        sprite.FrameDurations = _durations?.ToList();
        sprite.ExportPivot = _pivot;
        sprite.NineSlice = _nineSlice?.Copy();
    }
}
