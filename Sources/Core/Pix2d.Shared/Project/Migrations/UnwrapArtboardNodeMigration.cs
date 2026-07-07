using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Pix2d.Project.Migrations;

/// <summary>
/// Format migration v1 → v2: unwraps the removed <c>Pix2d.CommonNodes.ArtboardNode</c> container.
///
/// <para>Pre-3.x files nested an extra level: <c>Scene → ArtboardNode → Pix2dSprite → Layer[]</c>.
/// The artboard was later merged into <see cref="!:Pix2dSprite"/> itself, so the modern shape is
/// <c>Scene → Pix2dSprite → Layer[]</c> and <c>ArtboardNode</c> no longer exists as a type — files
/// containing it fail to load. This migration replaces each <c>ArtboardNode</c> with the
/// <c>Pix2dSprite</c>(s) it wrapped, carrying over the artboard's name and grid settings so nothing
/// visible changes for the user.</para>
///
/// It is shape-detecting and idempotent: a document with no <c>ArtboardNode</c> passes through
/// unchanged, so it is safe to run on every v1 (including unversioned) document.
/// </summary>
public sealed class UnwrapArtboardNodeMigration : ISceneJsonMigration
{
    private const string ArtboardTypeName = "Pix2d.CommonNodes.ArtboardNode";
    private const string SpriteTypeName = "Pix2d.CommonNodes.Pix2dSprite";

    public int FromVersion => 1;

    public JObject Migrate(JObject sceneRoot)
    {
        TransformContainer(sceneRoot);
        return sceneRoot;
    }

    // Rewrites a container node's "nodes" array in place, replacing any ArtboardNode child with the
    // sprites it contained, then recurses into the resulting children.
    private static void TransformContainer(JObject container)
    {
        if (container["nodes"] is not JArray children)
            return;

        var rewritten = new JArray();
        foreach (var child in children.OfType<JObject>())
        {
            if (IsType(child, ArtboardTypeName))
            {
                foreach (var sprite in InnerSprites(child))
                {
                    CarryArtboardProperties(child, sprite);
                    rewritten.Add(sprite);
                }
                // An ArtboardNode with no sprite child carried nothing representable — dropped.
            }
            else
            {
                rewritten.Add(child);
            }
        }

        container["nodes"] = rewritten;

        foreach (var child in rewritten.OfType<JObject>())
            TransformContainer(child);
    }

    private static IEnumerable<JObject> InnerSprites(JObject artboard) =>
        artboard["nodes"] is JArray inner
            ? inner.OfType<JObject>().Where(n => IsType(n, SpriteTypeName))
            : Enumerable.Empty<JObject>();

    private static void CarryArtboardProperties(JObject artboard, JObject sprite)
    {
        // The artboard held the user-facing name; the inner sprite's name was historically blank.
        if (string.IsNullOrEmpty((string?)sprite["name"]) && artboard["name"] != null)
            sprite["name"] = artboard["name"];

        // Grid settings live on the sprite in the modern model — only fill if absent.
        foreach (var prop in new[] { "gridCellSize", "showGrid" })
            if (sprite[prop] == null && artboard[prop] != null)
                sprite[prop] = artboard[prop];
    }

    // Matches the CLR type-name part of a $type discriminator, ignoring any ", Assembly" suffix.
    private static bool IsType(JObject node, string clrTypeName)
    {
        var discriminator = (string?)node["$type"];
        if (discriminator == null)
            return false;

        var commaIndex = discriminator.IndexOf(',');
        var typePart = (commaIndex >= 0 ? discriminator[..commaIndex] : discriminator).Trim();
        return typePart == clrTypeName;
    }
}
