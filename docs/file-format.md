# Pix2d project file format (`.pix2d`)

This document describes the on-disk format of Pix2d projects and the versioning / migration
machinery that keeps old files loadable. It is the reference for agents and third-party tooling, and
the basis for future importers/exporters (roadmap **H1.2**).

> When this document and the code disagree, the code is right. The canonical implementation lives in
> [`ProjectFormat`](../Sources/Core/Pix2d.Shared/Project/ProjectFormat.cs),
> [`ProjectPacker`](../Sources/Core/Pix2d.Shared/Project/ProjectPacker.cs) /
> [`ProjectUnpacker`](../Sources/Core/Pix2d.Shared/Project/ProjectUnpacker.cs), and
> [`NodeSerializer`](../Sources/Core/SkiaNodes/Serialization/NodeSerializer.cs).

## Container layout

A `.pix2d` file is a **ZIP archive** (no compression on binary entries) with these entries:

| Entry | Purpose |
| --- | --- |
| `manifest.json` | Format metadata. `{"formatVersion": <int>}`. Drives migration on load. |
| `project.json` | The serialized scene node tree (see below). |
| `<guid>.png` | One PNG per bitmap frame. Referenced from `project.json` by file name. |
| `__project_thumbnail.jpg` | Composite preview of all artboards, for the file browser / recent list. |

The scene bitmaps are **not** embedded in `project.json`; each `SpriteNode` stores an `SKBitmapRef`
whose `id` is the PNG entry name, and the images travel as separate ZIP entries. This keeps the JSON
small and lets the packer store PNGs uncompressed (they are already compressed).

There is also a **folder layout** (unpacked project) produced by
`ProjectPacker.WriteProjectAsync(IWriteDestinationFolder, …)`: `project.pix2d.json` + `manifest.json`
+ a `Resources/` folder of PNGs. Same schema, different packaging.

### Legacy entries you may encounter

Older files can contain `preview.pngx` (a superseded thumbnail entry) instead of / in addition to
`__project_thumbnail.jpg`. It is ignored on load.

## `project.json` — the node tree

`project.json` is the JSON serialization (Newtonsoft.Json) of an `SKNode` tree. The modern shape is:

```
Scene (SKNode)
└─ Pix2dSprite            // an artboard; one per sprite, several allowed per scene
   └─ Pix2dSprite+Layer   // a layer; holds a frame table
      └─ SpriteNode       // a frame bitmap (bitmap: { $type: "BitmapRef", id: "<guid>.png" })
```

Serialization rules (`NodeSerializer` / `WriteOnlyPropertiesContractResolver`):

- **Property naming** is camelCase.
- A property is serialized if it has a setter (plus the `Children`/`nodes` collection). This is an
  *implicit* contract — see the caveat in [Known quirks](#known-quirks-and-follow-ups).
- **Polymorphism** uses a `$type` discriminator, but the value is a **stable key**, not the CLR type
  name — see the next section.
- `SKBitmap`, `SKColor`, `SKSize` are handled by dedicated converters.

## Stable `$type` keys ([`NodeTypeRegistry`](../Sources/Core/SkiaNodes/Serialization/NodeTypeRegistry.cs))

Before format hardening, `$type` held the CLR `Type.FullName` (namespace + type name). That coupled
the on-disk format to code internals: renaming a class, moving it between namespaces/assemblies, or
removing it silently broke every older file. (A real casualty: `Pix2d.CommonNodes.ArtboardNode`,
which was removed — see the migration below.)

Now the writer emits a **stable key** registered in `NodeTypeRegistry`, decoupling the format from
the class name. Refactoring a registered type no longer changes what is written.

Current keys (registered in `ProjectFormat.EnsureInitialized` and the `NodeTypeRegistry` static ctor):

| Stable key | Runtime type |
| --- | --- |
| `Sprite` | `Pix2d.CommonNodes.Pix2dSprite` |
| `Layer` | `Pix2d.CommonNodes.Pix2dSprite+Layer` |
| `SpriteNode` | `Pix2d.CommonNodes.SpriteNode` |
| `Bitmap` | `Pix2d.CommonNodes.BitmapNode` |
| `Text` | `Pix2d.CommonNodes.TextNode` |
| `Rectangle` | `Pix2d.CommonNodes.RectangleNode` |
| `PixelShadowEffect` … `ImageAdjustEffect` | the `Pix2d.Effects.*Effect` family |
| `BitmapRef`, `Root`, `Group` | the SkiaNodes value/base types |

**Rules for keys:**

- **Never change a key once shipped** — that orphans every file written with it.
- **Add a key** for any new persisted node/value type. An unregistered type still serializes (by
  full-name, with a warning) but is *not* refactor-proof.

**Reading** resolves a discriminator in this order (`TypeNameAssemblyExcludingSerializationBinder`):
1. stable key or declared legacy alias (`NodeTypeRegistry.TryResolve`);
2. full-name in a known assembly — the SkiaNodes assembly is always scanned, and the `, Assembly`
   hint is ignored, so a type stamped with the wrong/old assembly still resolves;
3. `Type.GetType` (BCL / assembly-qualified);
4. otherwise → `UnknownNodeTypeException`, which the deserializer catches to **skip that node** and
   keep loading the rest of the scene (graceful degradation instead of a hard failure).

## Versioning & migrations ([`ProjectFormat`](../Sources/Core/Pix2d.Shared/Project/ProjectFormat.cs))

- `manifest.json`'s `formatVersion` records the schema a file was written against.
- `ProjectFormat.CurrentVersion` is the version new files are written with (**currently 2**).
- Files **without** a `manifest.json` (everything written before this feature) are read as
  `BaselineVersion` (**1**).
- On load, `ProjectFormat.DeserializeScene` upgrades the document from its version to
  `CurrentVersion` by running the `ISceneJsonMigration` chain over the parsed `JObject` **before**
  deserializing into nodes. Old files are **never rewritten in place** — migration happens in memory
  on each load until the file is re-saved.

### Adding a migration

1. Bump `ProjectFormat.CurrentVersion`.
2. Implement [`ISceneJsonMigration`](../Sources/Core/Pix2d.Shared/Project/ISceneJsonMigration.cs)
   with `FromVersion = <previous current>`; transform the `JObject` and return it.
3. Register the instance in `ProjectFormat._migrations` (ascending by `FromVersion`).
4. Make it **shape-detecting / idempotent**: it runs on every baseline (unversioned) file, so it must
   be a no-op when the document does not need it.
5. Add a corpus file that exercises it and run the harness (below).

Migrations operate on JSON only — they must **not** reference current runtime node classes (those may
change again), only the JSON shape of their own version.

### Shipped migrations

- **v1 → v2 — [`UnwrapArtboardNodeMigration`](../Sources/Core/Pix2d.Shared/Project/Migrations/UnwrapArtboardNodeMigration.cs).**
  Pre-3.x files nested an extra container: `Scene → ArtboardNode → Pix2dSprite → Layer[]`. The
  artboard role was later merged into `Pix2dSprite`, and `ArtboardNode` was removed. The migration
  replaces each `ArtboardNode` with the `Pix2dSprite` it wrapped (carrying the artboard's name and
  grid settings), yielding the modern `Scene → Pix2dSprite → Layer[]` shape.

## Autosave / crash recovery

The incremental autosave store
([`IncrementalSessionStore`](../Sources/Core/Pix2d.Core/Services/AutoSave/IncrementalSessionStore.cs))
serializes the same node tree to a session `scene.json` and records the scene format version in its
`SessionManifest` (`sfv` field — distinct from `v`, which versions the manifest's own schema). It
loads through the same `ProjectFormat.DeserializeScene`, so a recovered scene is migrated exactly like
a `.pix2d` file.

On launch the workspace is **silently restored** (every previously open tab comes back, browser-style).
When the previous run ended abnormally — a crash or OS kill rather than a graceful close —
`AutoSaveService` additionally raises a non-blocking banner ([`RecoveryNoticeView`](../Sources/Core/Pix2d.Core/UI/RecoveryNoticeView.cs))
telling the user their work was recovered. The clean-vs-unclean verdict is not a new marker: it reuses
`ICrashReportService.PreviousShutdownWasClean`, captured at startup from the same `CleanExitRequested`
signal the crash-report path already maintains (set by `MarkCleanExit()` on graceful shutdown).

## Backward-compatibility corpus test

[`Sources/Tools/Pix2d.FormatTests`](../Sources/Tools/Pix2d.FormatTests) loads every `.pix2d` in a
corpus folder (default: repo-root [`TestImages/`](../TestImages)) through the real
`ProjectUnpacker` → `ProjectFormat` path, then asserts structure and does a **save→reload round-trip**
(re-serialize with stable keys, reload at the current version, confirm counts are unchanged). Run it:

```bash
dotnet run --project Sources/Tools/Pix2d.FormatTests            # uses TestImages/
dotnet run --project Sources/Tools/Pix2d.FormatTests <corpusDir>
```

Exit code is non-zero if any loadable file fails. Structurally broken archives (unreadable ZIP / no
`project.json`) are reported as `CORRUPT` and ignored (they are not format-compatibility failures) —
`TestImages/ptvRightTemplate.pix2d` is one, kept deliberately as a corrupt-input sample. The project
is intentionally **not** part of `Pix2d.slnx`, so it does not affect the multi-platform solution build
or CI (wiring it into CI is a follow-up, gated on a real test project — roadmap Track Q).

### Serialization contract snapshot

The harness also pins the **serialized property set per persisted type** in a checked-in
[`format-contract.json`](../Sources/Tools/Pix2d.FormatTests/format-contract.json) and asserts the live
set matches it. This makes the format's property surface explicit and enforced: adding or removing a
settable property on a node (which the implicit `WriteOnlyPropertiesContractResolver` heuristic would
otherwise silently start/stop persisting) shows up as contract DRIFT. When a change is intentional,
add a migration if it breaks old files, then regenerate the snapshot:

```bash
dotnet run --project Sources/Tools/Pix2d.FormatTests -- --update-contract
```

The snapshot is the explicit allow-list; it does not *prune* what is written (that is a separate,
future step — see below).

## Known quirks and follow-ups

- **Implicit property contract, now guarded.** `WriteOnlyPropertiesContractResolver` still serializes
  any property with a setter (including *private* setters, and get-only auto-properties), so the
  persisted set is defined by code rather than an opt-in list. That set is now **pinned and enforced**
  by the contract snapshot (above), so it can no longer drift silently.
- **Pruning is `[JsonIgnore]`, not a migration.** Dropping a property from the format is
  reader-tolerant in both directions (old files carry the field → ignored on read; new files omit it →
  old code defaults it), so pruning needs **no version bump / migration** — only a contract-snapshot
  regeneration. Pruned so far (all verified behaviour-preserving against the corpus):
  - `SKNode.IsDirty` — transient render flag; its private setter was being persisted, and a saved
    `false` could skip a node's first paint.
  - `BitmapNode.FlushRequestedAction` — a runtime delegate with no meaningful serialized form.
  - `SKNode.IsInteractive` — runtime hit-test flag; only ever set on adorner/overlay nodes (never
    persisted document nodes, which were `false` in all 1402 corpus occurrences).
  - `NodeDesignerState.IsSelected` / `IsExpanded` — editor selection and tree-expansion UI state.

  Kept as genuine document state: `NodeDesignerState.IsLocked`, `LockAspect`, `ShowChildrenInTree`,
  and `ExportSettings`. The contract guard now also snapshots the inline value objects
  (`NodeDesignerState`, `NodeExportSettings`, `OnionSkinSettings`, keyed `~…`) so their fields get the
  same drift protection as node properties.
- **Double parse on legacy load.** A baseline file is parsed once as a `JObject` (for migration) and
  again into nodes. The cost disappears once the file is re-saved (new files write `CurrentVersion`
  and skip migration).
- **Corrupt corpus file.** `TestImages/ptvRightTemplate.pix2d` has a broken ZIP central directory and
  cannot be opened by any build; it is retained only as a corrupt-input sample.
