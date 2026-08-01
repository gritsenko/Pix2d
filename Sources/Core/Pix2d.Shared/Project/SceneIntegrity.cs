using System;
using System.Linq;
using Pix2d.CommonNodes;
using Pix2d.Primitives;
using SkiaNodes;
using SkiaNodes.Serialization;
using SkiaSharp;

namespace Pix2d.Project;

/// <summary>
/// Post-deserialization repair pass over a loaded scene tree. Runs from
/// <see cref="ProjectFormat.DeserializeScene"/>, so every path that reads a scene — project files,
/// workspace autosave restore, the CLI and the format-test harness — is covered by one implementation.
///
/// <para>Distinct from an <see cref="ISceneJsonMigration"/>: migrations translate a known older *shape*
/// into the current one, while this pass fixes documents that are structurally current but carry values
/// the editor cannot work with. Today that is exactly one class of defect — a sprite or layer whose
/// canvas size is degenerate (0, negative or NaN). Such a sprite loads fine and renders as nothing, then
/// throws <c>"Bitmap is null"</c> on the first pointer-down, which is what a user meets as "the app
/// errors every time I try to draw" (appstat, 3.10.0, <c>app_context: canvas=0x0</c>).</para>
///
/// <para>Repair is content-preserving and never destructive: the size is recovered from the frame
/// bitmaps that are already in the file, so the artwork comes back with it. The pass is silent on a
/// healthy document and warns (once per repaired node) otherwise.</para>
/// </summary>
public static class SceneIntegrity
{
    /// <summary>
    /// Canvas used when a degenerate sprite carries no pixels to recover a size from — matches the
    /// editor's own new-project fallback so the artboard lands at a size the user recognises.
    /// </summary>
    private static readonly SKSize FallbackSpriteSize = new(64, 64);

    /// <summary>
    /// Repairs <paramref name="scene"/> in place and returns it, so callers can chain it onto a
    /// deserialize call. Safe to run on any tree, including one with no sprites.
    /// </summary>
    public static SKNode Repair(SKNode scene)
    {
        foreach (var sprite in EnumerateTree(scene).OfType<Pix2dSprite>())
            RepairSprite(sprite);

        return scene;
    }

    private static void RepairSprite(Pix2dSprite sprite)
    {
        var layers = sprite.Layers.ToArray();

        if (CanvasSize.IsDegenerate(sprite.Size))
        {
            var recovered = RecoverSize(sprite, layers);
            NodeTypeRegistry.Warn(
                $"[SceneIntegrity] Sprite '{sprite.Name}' loaded with a degenerate canvas " +
                $"({sprite.Size.Width}x{sprite.Size.Height}); repaired to {recovered.Width}x{recovered.Height}.");

            // Assign Size directly rather than going through Resize/Crop: the frame bitmaps already hold
            // the real pixels at the recovered dimensions, so this only re-syncs the container that lost
            // its size. Resizing would rescale — i.e. destroy — those pixels.
            sprite.Size = recovered;
        }

        foreach (var layer in layers)
        {
            if (CanvasSize.IsDegenerate(layer.Size))
                layer.Size = sprite.Size;
        }

        RepairAnimationMeta(sprite);
    }

    /// <summary>
    /// Brings the index-keyed animation metadata back inside the sprite's actual frame range. The
    /// editor's own operations keep it consistent, but a file can still arrive inconsistent — written by
    /// an older/newer build, hand-edited, or saved from a session where an operation bailed out. Doing
    /// it here once means the timeline UI and the sheet exporter can trust the invariant instead of each
    /// re-deriving it.
    /// </summary>
    private static void RepairAnimationMeta(Pix2dSprite sprite)
    {
        var tagsBefore = sprite.AnimationTags?.Count ?? 0;

        // Drop unnamed tags first — a nameless tag has no meaning in exported metadata (it becomes an
        // empty frameTags name) and cannot be selected with the CLI's --tag.
        sprite.AnimationTags?.RemoveAll(t => string.IsNullOrWhiteSpace(t.Name));
        sprite.NormalizeAnimationTags();

        var tagsAfter = sprite.AnimationTags?.Count ?? 0;
        if (tagsAfter != tagsBefore)
            NodeTypeRegistry.Warn(
                $"[SceneIntegrity] Sprite '{sprite.Name}': dropped {tagsBefore - tagsAfter} animation tag(s) " +
                "that were unnamed or no longer addressed any frame.");

        if (sprite.FrameDurations == null)
            return;

        // Durations are keyed by frame index; anything past the end is already ignored by
        // GetFrameDurationMs, but trimming keeps the document honest and the contract snapshot stable.
        var frameCount = sprite.GetFramesCount();
        if (sprite.FrameDurations.Count > frameCount)
            sprite.FrameDurations.RemoveRange(frameCount, sprite.FrameDurations.Count - frameCount);

        for (var i = 0; i < sprite.FrameDurations.Count; i++)
        {
            var value = sprite.FrameDurations[i];
            if (value != 0)
                sprite.FrameDurations[i] = Math.Clamp(value, 1, 60000);
        }

        if (sprite.FrameDurations.All(d => d <= 0))
            sprite.FrameDurations = null;
    }

    /// <summary>
    /// Best size for a sprite that lost its own: the largest frame bitmap it still holds (frames of one
    /// sprite share a canvas, so any of them is authoritative — max() just tolerates a partially damaged
    /// document), else the largest surviving layer size, else the editor's default canvas.
    /// </summary>
    private static SKSize RecoverSize(Pix2dSprite sprite, Pix2dSprite.Layer[] layers)
    {
        var width = 0f;
        var height = 0f;

        foreach (var bitmapNode in layers.SelectMany(l => l.Nodes.OfType<BitmapNode>()))
        {
            var bitmap = bitmapNode.Bitmap;
            if (bitmap == null)
                continue;

            width = Math.Max(width, bitmap.Width);
            height = Math.Max(height, bitmap.Height);
        }

        if (CanvasSize.IsDegenerate(width, height))
        {
            foreach (var layer in layers)
            {
                width = Math.Max(width, layer.Size.Width);
                height = Math.Max(height, layer.Size.Height);
            }
        }

        return CanvasSize.IsDegenerate(width, height) ? FallbackSpriteSize : new SKSize(width, height);
    }

    private static IEnumerable<SKNode> EnumerateTree(SKNode node)
    {
        yield return node;

        foreach (var child in node.Nodes)
        foreach (var descendant in EnumerateTree(child))
            yield return descendant;
    }
}
