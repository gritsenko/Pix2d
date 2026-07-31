using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Pix2d.CommonNodes;
using Pix2d.Effects;
using SkiaNodes;
using SkiaNodes.Serialization;
using SkiaSharp;

namespace Pix2d.Project;

/// <summary>
/// Single source of truth for the <c>.pix2d</c> / autosave scene-tree format: the current version,
/// the stable <c>$type</c> key registrations, and the migration pipeline. Both the file unpacker
/// (<see cref="ProjectUnpacker"/>) and the autosave store deserialize through
/// <see cref="DeserializeScene"/> so versioning and migration live in exactly one place.
///
/// <para><b>Versioning model.</b> The shape shipped when versioning was introduced is the
/// <see cref="BaselineVersion"/> (1). Archives without a <c>manifest.json</c> (every file written by
/// an older build) are read as the baseline. Differences that a binder can absorb — the <c>$type</c>
/// discriminator style, renamed/misassembly-stamped types — are handled by
/// <see cref="NodeTypeRegistry"/> and need no migration. Structural changes that a binder cannot
/// absorb are handled by <see cref="ISceneJsonMigration"/> steps, which is why the current version is
/// now 2: <see cref="Migrations.UnwrapArtboardNodeMigration"/> flattens the removed
/// <c>ArtboardNode</c> container that old files nested around each sprite.</para>
/// </summary>
public static class ProjectFormat
{
    /// <summary>Format version written into new files and used as the migration target.</summary>
    public const int CurrentVersion = 2;

    /// <summary>Version assumed for archives that predate the <c>manifest.json</c> entry.</summary>
    public const int BaselineVersion = 1;

    // Ordered ascending by FromVersion; the runner applies each step from the file version up to
    // CurrentVersion. Shape-detecting migrations are no-ops on documents that don't need them, so
    // running the whole chain on a baseline file is safe.
    private static readonly List<ISceneJsonMigration> _migrations = new()
    {
        new Migrations.UnwrapArtboardNodeMigration(), // 1 -> 2
    };

    private static readonly object _initGate = new();
    private static bool _initialized;

    /// <summary>
    /// Wires up serialization for the product node types: sets the assemblies the binder scans for
    /// full-name fallback and registers stable <c>$type</c> keys + legacy aliases. Idempotent and
    /// thread-safe; call once during bootstrap (and from any standalone tool that loads projects).
    /// </summary>
    public static void EnsureInitialized(Assembly[] extraAssemblies)
    {
        lock (_initGate)
        {
            if (_initialized)
                return;

            NodeSerializer.ExtraAssemblies = extraAssemblies;

            // Stable keys: the on-disk $type for these types is frozen to these strings. Renaming or
            // moving any of the backing classes no longer changes the format. DO NOT change a key
            // once shipped — that would orphan every file written with it.
            NodeTypeRegistry.Register("Sprite", typeof(Pix2dSprite));
            NodeTypeRegistry.Register("Layer", typeof(Pix2dSprite.Layer));
            NodeTypeRegistry.Register("SpriteNode", typeof(SpriteNode));
            NodeTypeRegistry.Register("Bitmap", typeof(BitmapNode));
            NodeTypeRegistry.Register("Text", typeof(TextNode));
            NodeTypeRegistry.Register("Rectangle", typeof(RectangleNode));

            NodeTypeRegistry.Register("PixelShadowEffect", typeof(PixelShadowEffect));
            NodeTypeRegistry.Register("PixelGlowEffect", typeof(PixelGlowEffect));
            NodeTypeRegistry.Register("PixelBlurEffect", typeof(PixelBlurEffect));
            NodeTypeRegistry.Register("OutlineEffect", typeof(OutlineEffect));
            NodeTypeRegistry.Register("GrayscaleEffect", typeof(GrayscaleEffect));
            NodeTypeRegistry.Register("ColorOverlayEffect", typeof(ColorOverlayEffect));
            NodeTypeRegistry.Register("ImageAdjustEffect", typeof(ImageAdjustEffect));

            // Note: the removed ArtboardNode type is intentionally NOT aliased to Pix2dSprite — it was a
            // *container* wrapping a sprite, not a sprite. UnwrapArtboardNodeMigration flattens it (v1->v2)
            // so the inner sprite (and its layers/pixels) survive; aliasing would silently lose them.

            _initialized = true;
        }
    }

    /// <summary>
    /// Deserializes a scene document, first upgrading it from <paramref name="fileVersion"/> to
    /// <see cref="CurrentVersion"/> through the migration pipeline, then running the
    /// <see cref="SceneIntegrity"/> repair pass. A version newer than supported is loaded best-effort
    /// (unknown fields are ignored; unknown node types are skipped by the binder).
    /// </summary>
    public static SKNode DeserializeScene(string projectJson, int fileVersion, IDictionary<string, SKBitmap> images)
    {
        if (fileVersion > CurrentVersion)
            NodeTypeRegistry.Warn(
                $"[ProjectFormat] File format v{fileVersion} is newer than supported v{CurrentVersion}; loading best-effort.");
        else if (fileVersion < CurrentVersion)
            projectJson = ApplyMigrations(projectJson, fileVersion);

        var scene = NodeSerializer.Deserialize<SKNode>(projectJson, images);

        // Migrations fix known *shapes*; this fixes values the editor cannot work with regardless of
        // version (today: a degenerate canvas size, which makes every stroke throw). Runs on every load
        // path — project file, autosave restore, CLI, format tests — because they all come through here.
        return SceneIntegrity.Repair(scene);
    }

    private static string ApplyMigrations(string projectJson, int fileVersion)
    {
        if (_migrations.Count == 0)
            return projectJson; // dormant pipeline — nothing to do

        var root = JObject.Parse(projectJson);
        for (var v = fileVersion; v < CurrentVersion; v++)
        {
            var migration = _migrations.FirstOrDefault(m => m.FromVersion == v)
                ?? throw new System.InvalidOperationException(
                    $"No migration registered from format v{v} to v{v + 1}.");

            root = migration.Migrate(root) ?? root;
            NodeTypeRegistry.Warn($"[ProjectFormat] Migrated scene v{v} -> v{v + 1}.");
        }

        return root.ToString();
    }
}
