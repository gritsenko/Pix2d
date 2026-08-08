# Pix2d sprite-sheet export format

This document describes Pix2d's **sprite-sheet export v2** — the packed PNG plus the JSON metadata
sidecar — and the packing options behind it. It is the reference for engine importers and for the
headless CLI / MCP server (roadmap **H2.2**, **E.3**).

> When this document and the code disagree, the code is right. The canonical implementation lives in
> [`SpriteSheetBuilder`](../Sources/Core/Pix2d.Shared/Export/Sheet/SpriteSheetBuilder.cs) and the
> metadata emitters under
> [`Export/Sheet/Metadata/`](../Sources/Core/Pix2d.Shared/Export/Sheet/Metadata/). The in-app entry
> point is [`SpriteSheetExporter`](../Sources/Core/Pix2d.Core/Plugins/ImageFormats/PngFormat/Exporters/SpriteSheetExporter.cs)
> (Export dialog → **Sprite sheet (PNG + JSON)**).

## What gets written

A sprite-sheet export produces **two files that share a base name**:

| File | Purpose |
| --- | --- |
| `<name>.png` | The packed sheet image (all animation frames of the active artboard). |
| `<name>.json` | Metadata sidecar (default: Aseprite `--data` shape). Omitted if metadata format is **None**. |

On desktop the sidecar is written next to the chosen PNG automatically. On mobile/web (where the
picked file has no usable sibling path) a second save prompt asks where to put the sidecar.

## Packing options

All options are pure data ([`SpriteSheetOptions`](../Sources/Core/Pix2d.Shared/Export/Sheet/SpriteSheetModels.cs))
and flow unchanged through both the Export dialog and the future CLI.

| Option | Meaning |
| --- | --- |
| **Packing** | `Grid` — uniform cells, `Max columns` wide. `Tight` — shelf-packed (frames may differ in size once trimmed); minimises wasted space. |
| **Max columns** | Columns in `Grid` mode. Ignored for `Tight`. |
| **Spacing** | Transparent gutter, in output pixels, between adjacent frames. |
| **Trim transparent borders** | Crop each frame to its opaque bounding box. The trim offset is recorded in the metadata (`spriteSourceSize`), so importers reconstruct the original frame. A fully-transparent frame trims to 1×1. |
| **Power-of-two size** | Round the final sheet width/height up to the next power of two. |
| **Image scale** | The Export dialog's shared scale slider (1–20×). Frames are rendered at this scale with nearest-neighbour sampling; all metadata rects are in scaled (output) pixels. `duration` is in ms and is scale-independent. |

Frames are rendered headlessly on CPU Skia (no window), so the same output is produced in-app, in the
scenario harness, and — in a later increment — from the CLI.

## Metadata: Aseprite `--data` JSON

The default sidecar mirrors [Aseprite's `--data` JSON](https://www.aseprite.org/docs/cli/#data) so
importers written against Aseprite output (Godot, Unity, Phaser, custom loaders) read Pix2d sheets
unchanged. Property names/casing are the contract.

`frames` is a **hash** by default (Aseprite's default output). Each key follows Aseprite's
`{title} {frame}` convention (e.g. `"hero 0"`) and is opaque to compliant importers. A `json-array`
form (frames as an array, each with a `filename`) is available for importers that need ordering.

```json
{
  "frames": {
    "hero 0": {
      "frame": { "x": 0, "y": 0, "w": 64, "h": 64 },
      "rotated": false,
      "trimmed": false,
      "spriteSourceSize": { "x": 0, "y": 0, "w": 64, "h": 64 },
      "sourceSize": { "w": 64, "h": 64 },
      "duration": 67
    },
    "hero 1": {
      "frame": { "x": 64, "y": 0, "w": 64, "h": 64 },
      "rotated": false,
      "trimmed": false,
      "spriteSourceSize": { "x": 0, "y": 0, "w": 64, "h": 64 },
      "sourceSize": { "w": 64, "h": 64 },
      "duration": 67
    }
  },
  "meta": {
    "app": "https://pix2d.com/",
    "version": "3.9.0",
    "image": "hero.png",
    "format": "RGBA8888",
    "size": { "w": 128, "h": 64 },
    "scale": "1",
    "frameTags": [],
    "layers": [
      { "name": "Layer 1", "opacity": 255, "blendMode": "normal" }
    ],
    "slices": []
  }
}
```

### Field reference

| Field | Notes |
| --- | --- |
| `frame` | Placement of the frame on the sheet, in output pixels. |
| `rotated` | Always `false` in v2 (frame rotation is not supported yet). |
| `trimmed` | `true` when `Trim` was on **and** the frame's opaque bounds are smaller than the full canvas. |
| `spriteSourceSize` | The trimmed content's rect within the original frame: `{x,y}` is the trim offset, `{w,h}` the trimmed size. Equals the full frame when not trimmed. |
| `sourceSize` | The original (scaled) frame/canvas size. |
| `duration` | Frame duration in **milliseconds**. Currently uniform — `round(1000 / fps)` from the sprite's frame rate. Per-frame durations arrive with the animation-metadata model (see below). |
| `meta.app` | Always `https://pix2d.com/`. |
| `meta.version` | The Pix2D version that produced the file. |
| `meta.image` | The PNG file name the sidecar pairs with. |
| `meta.format` | Always `RGBA8888`. |
| `meta.size` | The packed sheet dimensions. |
| `meta.scale` | Emitted as a **string** (Aseprite quirk that strict parsers depend on). |
| `meta.frameTags` | Named animation ranges: `{ name, from, to, direction, color? }`. `direction` ∈ `forward \| reverse \| pingpong \| pingpong_reverse`. |
| `meta.layers` | `{ name, opacity (0–255), blendMode }` per layer. Omitted when there are no layers. |
| `meta.slices` | Pivot and 9-slice data, using Aseprite's slice mechanism: a slice key's `pivot` is the anchor; its `center` rect is the 9-slice inner rect. Empty when neither is set. |
| `animations` | Optional top-level Pixi/Phaser convenience map (`tag name → frame keys`). Emitted only when the sprite has tags. Aseprite-strict importers ignore it. |

## Engine presets

Beyond the Aseprite JSON above, the same `PackedSheet` can be written straight into three engines' own
formats. They are sibling [`ISheetMetadataEmitter`](../Sources/Core/Pix2d.Shared/Export/Sheet/Metadata/ISheetMetadataEmitter.cs)
implementations registered in [`SheetMetadataEmitters`](../Sources/Core/Pix2d.Shared/Export/Sheet/Metadata/SheetMetadataEmitters.cs),
so the Export dialog's **Metadata** dropdown and the CLI's `--format` enumerate exactly the same list —
adding a preset touches neither the UI nor the CLI.

| `--format` | Sidecar | What it is |
|---|---|---|
| `aseprite` *(default)* | `<sheet>.json` | The JSON documented above. The most portable option and the only one carrying everything (durations, pivot, 9-slice, layers). |
| `godot` | `<sheet>.tres` | A Godot 4 `SpriteFrames` resource, assignable to an `AnimatedSprite2D` with no import step. |
| `unity` | `<sheet>.png.meta` | Unity's own importer sidecar, with the sheet pre-sliced. |
| `libgdx` | `<sheet>.atlas` | A libGDX `TextureAtlas` descriptor read by `new TextureAtlas(...)`. |

In every preset each animation **tag** becomes one animation; a sprite with no tags exports as a single
animation over the whole sheet (`default` in Godot, the sprite name in libGDX and Unity).

**Godot** (`.tres`) declares one `AtlasTexture` sub-resource per referenced frame over one `ext_resource`
pointing at `res://<sheet>.png` — correct when the pair lands at the project root, and a one-line edit (or
Godot's *Fix Dependencies* dialog) otherwise, since the exporter cannot know the project tree. Trimmed
frames keep their original footprint through `AtlasTexture.margin`. Godot stores a frame's `duration` as a
*multiplier* of the animation `speed`, so a frame running at the sprite's own rate is exactly `1.0` and a
per-frame override scales from there. `SpriteFrames` has only a `loop` flag, so `reverse`/`pingpong` tags
export as ordinary looping animations.

**Unity** (`.png.meta`) is not a description of the sheet — it *is* Unity's serialized `TextureImporter`
state, so dropping the PNG plus this file into `Assets/` yields a texture that is already sliced
(`spriteMode: Multiple`, one sprite per frame named `<tag>_<index>`), already point-filtered and already
uncompressed. Sprite rects are written in Unity's **bottom-up** texture space, and 9-slice margins map onto
`border` as `(left, bottom, right, top)`; on a trimmed frame the border is re-based onto the trimmed rect,
and dropped altogether when it no longer fits. The `guid`, `spriteID` and `internalID` values are derived
deterministically from the sprite and frame names, so re-exporting over an existing asset keeps every scene
and prefab reference intact instead of orphaning them. The flip side of that determinism: the texture `guid`
is keyed on the **image file name alone**, so two different sheets both exported under the picker's default
name and imported into one Unity project collide — Unity regenerates one, and the next re-export flips it
back. Give each sheet a distinct file name. Animation *timing* is not expressible in a `.meta`
(it describes a texture, not clips) — tags survive as the sprite naming, so pair it with the Aseprite JSON
if a tool needs durations.

**libGDX** (`.atlas`) leans on the format's name+index convention: each tag becomes a region *name*
repeated once per frame with `index` counting within that tag, which is exactly what
`new Animation<>(frameDuration, atlas.findRegions("run"))` consumes. `offset` is measured from the
**bottom-left** of the original frame, per the format. Per-frame durations and pivot/9-slice have no
representation in an atlas descriptor and are dropped (pass `1f / fps` in code; libGDX reads nine-patches
from `.9.png` split pixels, not from the descriptor). Written in the spaced pre-1.9.9 form, which every
libGDX version's parser accepts.

Where the presets differ on **frames no tag covers**: Unity and libGDX both emit them, grouped under the
sprite name, because a plain region is exactly what those formats represent and a frame with no entry would
be permanently unaddressable (unsliceable in Unity, invisible to `findRegion`). Godot deliberately drops
them — a `SpriteFrames` resource has no concept of a free-standing frame, so an orphan would only be noise.

## Headless CLI

The same sheet engine is exposed as a command-line tool, [`Sources/Tools/Pix2d.Cli`](../Sources/Tools/Pix2d.Cli)
(assembly `pix2d`), for CI pipelines and agents. It references only `Pix2d.Shared` (no Avalonia, no
display), so it runs on a bare headless runner, and it calls the **same** `SpriteSheetBuilder` +
metadata emitters as the in-app exporter — CLI and GUI output are identical.

```
pix2d export <project.pix2d> --spritesheet <out.png> [--data <out.json>] [options]
pix2d list   <project.pix2d>
pix2d --version | --help
```

`export` options: `--data <path>` (write a metadata sidecar), `--format <id>` (default `aseprite`),
`--sheet-type grid|tight` (default `grid`), `--columns <n>`, `--padding <n>`, `--trim`, `--pot`,
`--scale <n>`, `--artboard <name|index>` (default: the first artboard).

```bash
# grid sheet + Aseprite JSON
pix2d export hero.pix2d --spritesheet hero.png --data hero.json

# straight into an engine (name --data with the preset's own extension)
pix2d export hero.pix2d --spritesheet hero.png --data hero.tres      --format godot
pix2d export hero.pix2d --spritesheet hero.png --data hero.png.meta  --format unity
pix2d export hero.pix2d --spritesheet hero.png --data hero.atlas     --format libgdx

# tightly-packed, trimmed, power-of-two, 2× scale
pix2d export hero.pix2d --spritesheet hero.png --sheet-type tight --trim --pot --scale 2

# inspect artboards (pure JSON on stdout — pipeable)
pix2d list hero.pix2d | jq '.artboards'
```

`list` prints artboards (index, name, size, layers, frames, fps) as JSON on **stdout**; load-time
diagnostics go to stderr, so the stdout payload stays machine-readable. Exit codes: `0` ok, `1` runtime
error, `2` bad arguments / file not found.

### Getting the binary

Every release ships the CLI as a **self-contained** archive per platform, attached to the
[GitHub release](https://github.com/gritsenko/Pix2d/releases/latest) — no .NET install needed on the target
machine:

| Asset | Platform |
|---|---|
| `pix2d_cli_win-x64-<version>.zip` | Windows x64 |
| `pix2d_cli_linux-x64-<version>.tar.gz` | Linux x64 |
| `pix2d_cli_osx-x64-<version>.tar.gz` | macOS Intel |
| `pix2d_cli_osx-arm64-<version>.tar.gz` | macOS Apple Silicon |

Each archive holds the `pix2d` executable **plus `libSkiaSharp`** next to it — the native library is not
folded into the single file (that would need self-extraction to a temp directory on every run), so extract
the whole archive rather than lifting out the executable alone.

```bash
# Linux/macOS: drop it into a CI job
curl -sSL -o pix2d.tar.gz \
  https://github.com/gritsenko/Pix2d/releases/latest/download/pix2d_cli_linux-x64-<version>.tar.gz
mkdir -p pix2d && tar -xzf pix2d.tar.gz -C pix2d
./pix2d/pix2d export hero.pix2d --spritesheet hero.png --data hero.json
```

The publishing job is `build_cli` in [`release-publish.yml`](../.github/workflows/release-publish.yml): one
Linux runner cross-publishes all four platforms (the tool has no Avalonia or display dependency, so it needs
no per-OS runner), smoke-tests the Linux build by actually exporting a corpus project, and attaches the
archives to the release. It is deliberately **not trimmed** — the `.pix2d` loader resolves node types
reflectively, which a trimmer cannot see.

To run from source instead: `dotnet run --project Sources/Tools/Pix2d.Cli -- …`. The project stays out of
`Pix2d.slnx` on purpose, so CI targets its csproj directly.

The CLI is also the foundation for the MCP server (roadmap **E.3**).

## Animation metadata

`frameTags`, per-frame `duration`, `slices` (pivot / 9-slice) and the `animations` map carry real
document data as of H2.2 PR-3. All of it is optional — a sprite that has none exports exactly the
empty/uniform defaults it always did.

| JSON | Model | Authored in |
|---|---|---|
| `meta.frameTags[]` (`name`, `from`, `to`, `direction`) | `Pix2dSprite.AnimationTags` | Animation properties popup (timeline → ⚙) |
| `frames[*].duration` | `Pix2dSprite.FrameDurations[i]`, falling back to `1000 / FrameRate` | same popup, per selected frame |
| `meta.slices[0].keys[0].pivot` | `Pix2dSprite.ExportPivot` (unscaled canvas px) | same popup, **Export anchors** |
| `meta.slices[0].keys[0].center` | `Pix2dSprite.NineSlice` margins → `(L, T, W−L−R, H−T−B)` | same popup, **Export anchors** |

`direction` uses Aseprite's spelling: `forward`, `reverse`, `pingpong`, `pingpong_reverse`. Tag ranges
are inclusive at both ends, and the editor keeps them aligned as frames are inserted / deleted /
reordered; a range that no longer addresses a frame is dropped on load by `SceneIntegrity` rather than
being silently clamped onto unrelated frames.

### Exporting a single tag

`pix2d export … --tag run` packs only that tag's frames. The sheet is **re-based to frame 0** — frame
keys run `name 0 … name n-1` and the single emitted tag spans `0 … n-1` — which is what Aseprite's own
`--tag` export produces, so an importer sees a self-contained animation. Per-frame durations still
follow their source frames. `pix2d list` prints each artboard's tag names (plus
`defaultFrameDurationMs`), so a pipeline can discover them without opening the project.

## Roadmap

- **Shipped:** grid + tight packing, trim, power-of-two, Aseprite-compatible JSON, the headless CLI
  (`pix2d export … / list`), and the animation-metadata model (tags, per-frame durations, pivot,
  9-slice) with `--tag` filtering.
- **Next:** engine presets (Godot `SpriteFrames` `.tres`, Unity meta, libGDX atlas) as sibling emitters
  over the same packed result; frame rotation in the packer; sheet-per-tag in the in-app Export dialog.

See the roadmap ([`docs/ROADMAP.md`](ROADMAP.md), **H2.2**) for the full plan.
