# Multiple artboards & multiple-project tabs — implementation plan

Status board for a two-part feature:

- **Part A — multiple sprites (artboards) on one scene** — ✅ implemented (commit on branch
  `feature/multiple-artboards-and-project-tabs`). Needs interactive QA.
- **Part B — multiple open projects with a desktop tab bar** — ✅ implemented (all phases B0–B9, see
  "Part B — implementation notes" below). Desktop + Browser heads build clean; smoke-tested: app boots,
  session recovery works, the tab strip renders with dirty marker / close / "+" buttons. Needs interactive QA.

Locked product decisions (agreed with the owner):
- Artboards first, then project tabs (mostly independent).
- Artboard switch is **by click** (pointer-down): clicking another artboard activates it; the same stroke
  then draws into it. No hover-switching.
- Project tab bar is a **dedicated strip** in the empty `Row 0` of `MainView`'s `UiGrid` (no window-chrome
  rework). Desktop only for now.
- Multi-project autosave/crash-recovery in v1 covers **only the active tab** (documented limitation).

---

## Architecture facts (verified in code)

Scene graph: `RootNode` → `Scene` (SKNode) → `Pix2dSprite` (`DrawingContainerBaseNode`/`IContainerNode`) →
`Layer[]` → `SpriteNode` (per-frame bitmaps).

- `NodeSerializer` serializes the whole tree and `SKNode.Position` persists → several sprites already
  round-trip in `.pix2d` with no serializer changes.
- `ViewPortService.ShowAll()` / `GetSceneBounds()` fit the union via `scene.GetBoundingBoxWithContent()`.
- `SelectionService.GetContainer(SKPoint)` returns the artboard under a world point.
- `SceneManager.SetScene()` does **not** unload the previous scene (good for tab switching; unload only on
  tab close).
- **`CurrentEditedNode` is the single source of truth for the drawing target.** `DrawingService.UpdateDrawingTarget()`
  re-reads it and reparents the shared `DrawingLayerNode` under the active sprite's adorner layer.
- `EditService.RequestEdit` cycles `CurrentNodeEditor` (null → SpriteEditor); this is what makes the
  Layers/Timeline panels reload — relied on instead of subscribing panels to editor events.
- Autosave restore (`IncrementalSessionStore.LoadSceneAsync`) is **key-based**: PNGs are matched to nodes by
  `SpriteNode.Id`, not by (layer, frame). `DirtySet` carries only `(int Layer, int Frame)` and is NOT
  sprite-qualified.

For Part B specifically:
- `AppState.LoadedProjects` (`List<ProjectState>`) exists but is unused — the intended skeleton.
  `CurrentProject` is observable via `Get/Set`.
- `ProjectState` is already self-contained: `File`, `SceneNode`, `CurrentEditedNode`, `CurrentNodeEditor`,
  `FrameEditorNode`, `Selection`, `CurrentContextType`, `ViewPortState`, `HasUnsavedChanges`.
- **Undo/redo is a global singleton** `OperationService` (one stack; `Clear()` on `ProjectLoadedMessage`).
  Registered at `Pix2dBootstrapperDI.cs:75`, before `EditService` (`:88`).
- **`WatchFor` binds to the state INSTANCE captured at subscription time** → services/views subscribed to
  `AppState.CurrentProject.*` go stale after a project switch (top Part B risk).
- `EditService.FrameEditorNode` is created once in the ctor on the startup project.
- ViewPort Pan/Zoom are not stored per-project (`ViewPortState` has only `ShowGrid`/`GridSpacing`).
- `MainView` `UiGrid` rows = `"Auto, Auto, *, Auto, Auto"`; `Row 0` is empty, `TopBarView` is on `Row 1`.

---

## Part A — DONE (multiple artboards)

Implemented across these files (see the feature commit for the diff):

- [IEditService.cs](../Sources/Core/Pix2d.Shared/Abstract/Services/IEditService.cs) — `ActivateArtboard(Pix2dSprite)`, `AddArtboard(SKSize)`.
- [EditService.cs](../Sources/Core/Pix2d.Core/Services/EditService.cs) — implementations; `OnProjectLoadedMessage` activates the first sprite via `OfType<Pix2dSprite>()`; handles `ActivateArtboardRequestedMessage`; A7 guard re-activates a survivor after undo/redo removes the active artboard. Now depends on `IOperationService`.
- [ActivateArtboardRequestedMessage.cs](../Sources/Core/Pix2d.Shared/Messages/ActivateArtboardRequestedMessage.cs) — new message (tool → EditService, avoids DI cycle / threading IEditService through 8 tool ctors).
- [PixelBrushToolBase.cs](../Sources/Core/Pix2d.Core/Plugins/Drawing/Tools/PixelBrushToolBase.cs) — on pointer-down outside the current drawing layer, resolves the sprite under the cursor and sends the activate message; dead hover-switch block removed.
- [SpriteEditCommands.cs](../Sources/Core/Pix2d.Core/Plugins/Sprite/Commands/SpriteEditCommands.cs) — `AddArtboard` command (Ctrl+Alt+N).
- [TopBarView.cs](../Sources/Core/Pix2d.Core/UI/TopBarView.cs) — "Artboard" button.
- [Pix2dSprite.cs](../Sources/Core/Pix2d.Shared/CommonNodes/Pix2dSprite.cs) — `OnDraw` override draws an active-artboard highlight border when `EditMode && vp.Settings.RenderAdorners`.
- [UiThreadSnapshotProvider.cs](../Sources/Core/Pix2d.Core/Services/AutoSave/UiThreadSnapshotProvider.cs) — snapshots all sprites; full re-snapshot when structure changed or >1 sprite (single-sprite incremental path preserved).
- [ProjectPacker.cs](../Sources/Core/Pix2d.Shared/Project/ProjectPacker.cs) — composite thumbnail across artboards via `RenderToBitmap`.
- [NewSceneFactory.cs](../Sources/Core/Pix2d.Core/Services/Project/NewSceneFactory.cs) — robust `OfType<Pix2dSprite>().First()`.

**Build:** Desktop head builds clean; app boots, recovers session, autosave runs.

**Interactive QA still needed:**
1. "Artboard" button / Ctrl+Alt+N → new artboard appears to the right, becomes active (highlight border);
   Undo removes it.
2. Two artboards: clicking the second makes it active; Layers/Timeline switch to its layers/frames; drawing
   lands in it.
3. Save a 2-artboard project → reopen: both present at correct positions; thumbnail is a composite.
4. Kill the process mid-edit of the second artboard → autosave recovery contains both artboards' content.
5. Single-sprite regression: new project / PNG import / export unchanged.

---

## Part A — batch export (DONE)

Multiple artboards make single-artboard export the wrong default, so the Export dialog gained a scope and
`IExportService` gained a destination rule. **Destination selection now lives in the service, not in the
exporters** — that inversion is what lets one exporter serve both a Save dialog and an N-artboard batch.

- [ExportItem.cs](../Sources/Core/Pix2d.Shared/Abstract/Export/ExportItem.cs) — `ExportItem(Name, Nodes)`
  (one per artboard) + `ExportScope { SelectedSprites, AllSprites }`.
- [ExportFileNames.cs](../Sources/Core/Pix2d.Shared/Export/ExportFileNames.cs) — the only place a
  user-typed artboard name is turned into a file name (invalid chars, whitespace runs, trailing dots that
  Windows silently strips, 96-char cap).
- [IExporter.cs](../Sources/Core/Pix2d.Shared/Abstract/Export/IExporter.cs) — `IExporter` is now pure
  identity (`Title`/`SupportedExtensions`/`MimeType`); the destination-blind `ExportAsync` is gone. Three
  capability interfaces say how an exporter can *write*: `IStreamExporter` (the service writes the stream),
  `IFilePickerExporter` (owns a Save dialog — **not** implemented by the PNG sequence, which is exactly what
  keeps it on the folder path), and the new **`IBatchExporter`** (receives a folder;
  `NeedsOwnFolderPerItem` = true for the frame sequence, whose own file names would collide, false for the
  sheet, whose PNG + sidecar are both derived from the base name). The dead `IFolderPickerExporter` is gone.
- [ExportService.cs](../Sources/Core/Pix2d.Core/Services/ExportService.cs) — `GetExportItems(scope)`
  (General-context node selection → artboards in **scene** order, falling back to `CurrentEditedNode`, so
  Sprite-context export is unchanged) and `ExportItemsAsync(items, scale, exporter)`, which routes: **one
  item + `IFilePickerExporter` → Save dialog seeded with the artboard's name; otherwise → one folder
  picker**, then writes each item (`IBatchExporter` if implemented, else the `IStreamExporter` output).
  Naming: artboard name whenever the scene has >1 artboard; a single-artboard project uses the saved
  project's file name (the project *is* the artwork), then the artboard name, then `untitled`.
- [ExportView.cs](../Sources/Core/Pix2d.Core/UI/Export/ExportView.cs) — **Sprites to export** dropdown
  (`Selected sprites (n)` / `All sprites (n)`, hidden for a single-artboard scene), plus a **master/detail
  artboard list** for batches (below). Output reports `n sprites · n files · ~total`. The "selected frame
  will be exported" hint hides in batch mode, and the playback bar hides whenever a *non-active* artboard is
  previewed — the scrub drives `SpriteEditor`, which only ever points at the edited artboard, so **every
  other artboard exports at its own current frame**.
- [ExportListItem.cs](../Sources/Core/Pix2d.Core/UI/Export/ExportListItem.cs) — one list row: thumbnail, the
  base name the output will be written under, and `w × h px · n files · ~size`.

### Batch preview: master/detail, same rule as the app menu

A batch has no single output to preview, so the preview pane becomes a master/detail pair modelled on
[`MainMenuView`](../Sources/Core/Pix2d.Core/UI/MainMenu/MainMenuView.cs):

- **Wide** — preview left, artboard list right (240 px); both always visible, no Back button. Clicking a row
  just re-targets the preview.
- **Narrow** — the list is the landing view and spans the pane; picking an artboard covers it with the
  preview plus a **← Back** button. The list is declared *first* in the grid so the (opaque) preview covers
  it when they share the cell.
- Column placement comes from `Style<T>` blocks keyed on `VisualStates.Narrow()` (`PreviewDetailName` /
  `PreviewListName`); **visibility is computed in the view-model**, not in styles — a style setter loses to a
  binding, so mixing the two silently breaks. `UpdateMasterDetailVisibility()` reads
  `UiState.VisualState` (watched, so a resize re-evaluates it) together with `IsBatchExport` and whether the
  user has drilled in.

Row metrics are the **real** exporter output, which means rendering every artboard — so `MeasureItemsAsync`
walks the rows one at a time, yielding to the dispatcher (`DispatcherPriority.Background`) between them: rows
fill in progressively, the dialog stays responsive on a 23-artboard scene, and the Output line carries `n/N
… ` until the last one lands. A new schedule (scale, exporter option, scope) cancels the run in flight, and
closing the dialog cancels it too. Thumbnails are scale-independent and rendered at most 64 px, so changing
the export scale re-measures without re-rendering them. This measurement path is shared by the single-item
case, so the one-artboard Output string is produced by the same code as the batch summary.
- [AvaloniaFolder.cs](../Sources/Core/Pix2d.Core/Common/FileSystem/AvaloniaFolder.cs) —
  `GetSubfolder`/`GetSubfolderAsync` implemented (were `NotImplementedException`); the async form goes
  through the storage provider so it also works where the picked folder has no filesystem path.

Re-export into the same folder **overwrites** — that's what makes it re-runnable — but confirms first:
exactly ("*n* file(s) will be overwritten") when the produced names are known up front, conservatively on a
non-empty folder for exporters that name their own files (`Yes/No` via `IDialogService`).

Fixed alongside: `SpritePngSequenceExporter.Export` was `async void` and called un-awaited (a failed
sequence export reported success and swallowed the error) — now an awaited `Task` that renders frames
lazily; `SvgImageExporter` became an `IStreamExporter`; `PngImageExporter` moved off the `"project"`
folder-memory context onto `"export"` like every other exporter.

**Covered by the headless harness** ([Pix2d.ScenarioTests](../Sources/Tools/Pix2d.ScenarioTests)) — a
`BatchExportScenario` drives the real `ExportItemsAsync` through a `HeadlessFileService` that answers both
pickers from a temp folder and records the suggested file name, so scope resolution, artboard naming, the
one-folder-prompt invariant, the declined-overwrite path, the sheet's per-artboard PNG+JSON and the
sequence's per-artboard subfolders are all asserted against files on disk.

**Interactive QA still needed:**
1. General context, Shift-click 2 of 3 artboards → Ctrl+E shows `Selected sprites (2)`; Save asks for a
   folder once and writes `<name>.png` per artboard.
2. Sprite context, 3 artboards → `Selected sprites (1)`; Save shows the **file** dialog pre-filled with the
   artboard name.
3. Single-artboard project → the scope dropdown is hidden; a saved project suggests its own file name.
4. `All sprites` + Png sequence → one subfolder per artboard, frames inside.
5. Re-export into the same folder → overwrite prompt; declining leaves the folder untouched.

---

## Part A — editing artboards as objects: the General context (DONE)

> **Superseded design.** The first implementation was a self-contained "edit sprite as object" mode that ran
> *inside* the Sprite context with its own Move / Resize / Crop state machine and a `SpriteActionsView`
> toolbar. It has been folded into the real **General (objects) edit context** — the mode's Move half is now
> ordinary General-context interaction, and only Resize / Crop remain as sub-modes. What follows describes
> the shipped behaviour.

Single-clicking an artboard's name label makes that artboard the active one (`IEditService.ActivateArtboard`,
same as clicking the artboard body). **Double-clicking the label enters the General context** with that
artboard selected — `IEditService.EditArtboardAsObject`, which keeps the sprite as the edit target (Layers /
Timeline / drawing target follow it) and only then flips `CurrentContextType`, because `ActivateArtboard`
alone always lands in Sprite. Ctrl+F12 (`GlobalCommands.SwitchToFullMode`, DEBUG) routes through the same
method. Going back is a double-click on an artboard's body (`RequestEdit`).

**Label decluttering.** Labels are drawn at a fixed *on-screen* size, so in world space they grow as the view
zooms out while the artboards do not — past some zoom the plaques bury each other and the artboards
themselves. [ArtboardLabelsLayer.cs](../Sources/Core/Pix2d.Shared/InteractiveNodes/ArtboardLabelsLayer.cs)
therefore re-runs a declutter pass every frame, in world units (the thresholds are *ratios*, so they hold at
any zoom):

- **Pinned labels always win** — the active artboard (`EditMode`), anything selected in General, and the
  artboard under the pointer. They are laid out first, so the survivor of a collision is the relevant one.
  Hover is fed from `SKInput.Current.PointerChanged` (the layer only receives pointer events over a *visible*
  label), and pinning by hover is what makes a hidden label reachable again: point at the artboard, its name
  reappears, click it.
- Any other label drops out once more than `MaxLabelOverlapShare` (15%) of **its own area** is covered by an
  already-placed label, or more than `MaxBodyIntrusionShare` (10%) of it lands on another artboard's body.
  Both shares are measured against the label, never against what it lands on — a plaque lying across the
  bottom edge of the row above reads as a mess whether that row is 16 px tall or 512, so a body-relative
  threshold silently let dense grids stay cluttered. In effect a label shows only while it (nearly) fits in
  the empty space above its own artboard: with the 16 px `ArtboardGap` that Arrange uses, a grid's lower rows
  lose their names until the zoom is high enough (~150%) for the gutter to hold the plaque, while the top row
  — which has nothing above it — keeps them at any zoom. The small tolerances keep a corner graze alive.
- Below `MinArtboardPx` (24 px on screen, either dimension) **nothing** is drawn for that artboard, pinned or
  not: the plaque is ~25 px tall, so at that zoom every label is bigger than the thing it names.
- Hit-testing goes through the same pass, so a hidden label is never a hidden click target
  (regression-covered in ScenarioTests: *"zoomed out, an unpinned artboard's label is dropped"*).

**Selecting, moving, deleting and arranging** are plain General-context interactions:

- [ObjectManipulationTool.cs](../Sources/Core/Pix2d.Core/Tools/ObjectManipulationTool.cs) — the context's
  default tool (arrow cursor). Click selects the top-most artboard, Shift+click toggles selection membership,
  press-and-drag selects *and* moves in one gesture, a drag on empty canvas rubber-band selects, hovering
  outlines the artboard under the cursor, and a double-click dives back into Sprite.
- The selection frame is the per-project `FrameEditorNode`, configured **move-only**
  (`AllowResize = false`, `AllowRotate = false`) in `EditService`: its generic thumbs commit a plain
  `TransformOperation`, which would change a `Pix2dSprite`'s `Size` without touching the layer bitmaps, and
  the pixel pipeline has no rotated-canvas concept. Each drag is one undoable `MoveOperation`.
  `PassShiftPressThrough = true` lets a Shift-press fall through the frame to the tool so Shift+click can
  *de*select (pixel-selection marquees keep the flag off).
- Commands: `Edit.Delete` (confirm dialog → one undoable `DeleteNodesOperation`, then re-targets a surviving
  artboard), `Edit.Arrange.Arrange` (dense `ceil(sqrt(n))`-column grid anchored at the selection's top-left,
  **grouped by shared name prefix**, one undo step — see below), `Edit.Arrange.BringForward` / `SendBackward`,
  `Edit.CancelSelection` (Esc).

**Arrange groups by name.** Asset sets are named in families ("icon-goal-gem", "icon-goal-ice",
"icon-star-empty"), so `IEditService.ArrangeSelectedObjects` packs family by family instead of in blind
reading order: [ArtboardNameGrouping.cs](../Sources/Core/Pix2d.Core/Services/ArtboardNameGrouping.cs) splits
names on `- _ . / space` and puts each artboard in the group of the **deepest prefix it shares with at least
one other selected artboard** (so "icon-goal" wins over the "icon" everything shares; an artboard whose
family has no second member falls back to the shallower group, and names sharing nothing land in a trailing
prefix-less bucket). Groups are laid out in alphabetical order of their first member — prefix-less bucket
last — each as its own row block wrapping at `ceil(sqrt(n))` columns, separated by a `3×` gutter
(`ArtboardGroupGap`) so the grouping reads on the canvas. Inside a group the order is natural name order
("frame 2" before "frame 10"), with canvas reading order breaking ties for equal / missing names. Still one
`MoveOperation` for the whole pass.

**Resize / Crop** remain a dedicated sub-mode because they change the canvas, owned by
[ArtboardObjectEditService.cs](../Sources/Core/Pix2d.Core/Services/ArtboardObjectEditService.cs)
(`IArtboardObjectEditService`) with two modes
([ArtboardObjectEditMode.cs](../Sources/Core/Pix2d.Shared/Primitives/ArtboardObjectEditMode.cs)):

- **Resize** — frame handles scale the pixel content (nearest-neighbour) to the new size on **Apply**
  ([ResizeArtboardScaleOperation.cs](../Sources/Core/Pix2d.Core/Plugins/Sprite/Operations/ResizeArtboardScaleOperation.cs),
  uses `Pix2dSprite.ResizeImage` — the *scaling* path, despite the name).
- **Crop** — frame handles change the canvas without scaling (trim / extend), committed on **Apply** via the
  existing `ResizeArtboardOperation` (`Pix2dSprite.Crop`).

A session is opened from the General action bar for the single selected artboard, **previews its result live**
while changing nothing until Apply (so one Ctrl+Z still reverts the whole gesture), and **ends** on
either Apply or Cancel — there is no lingering "Move" state to return to. Esc cancels
(`EditCommands.CancelSelection` checks `IArtboardObjectEditService.IsActive` first; Sprite context keeps its
own Esc in `SpriteEditCommands.Cancel`). Presses outside the frame are swallowed, so the session is left only
through the bar or Esc.

**Live result preview.** A moving rectangle says nothing about what the pixels will do, so each sub-mode draws
its own outcome (both are pure adorner painting — the document is written only by the Apply operation):

- **Resize** — a 1:1 snapshot of the artboard (`RenderToBitmap`, taken once at `Begin`) drawn stretched into
  the frame with nearest-neighbour sampling, over the same zoom-adaptive checkerboard the canvas paints
  ([CanvasCheckerboard.cs](../Sources/Core/Pix2d.Shared/CommonNodes/CanvasCheckerboard.cs), extracted from
  `DrawingContainerBaseNode` so preview and canvas can never drift apart). The overlay *replaces* the artboard
  for the session — otherwise a shrinking frame would leave the original showing around the preview — via
  `SKNode.IsRenderSuppressed`, a `[JsonIgnore]` runtime flag honoured by `SKNodeRenderer.RenderChildren`.
  Deliberately not `IsVisible`: that is document state, and a mid-session autosave would record an artboard
  that never paints. The flag is cleared in a `finally` around the Apply operation, on Cancel, and by the
  `ProjectLoadedMessage` / `ProjectActivatedMessage` guard that ends a session before the scene swaps under it.
  If the snapshot cannot be allocated (oversized canvas), it is logged and the session degrades to an
  outline-only frame instead of blanking the artboard.
- **Crop** — the sprite keeps painting itself and everything outside the frame is dimmed by a Photoshop-style
  crop shield, so the frame reads as "the part that survives".

`IArtboardObjectEditService.FrameRect` exposes the live world-space frame (what Apply would commit).

**Keyboard sizing + the proportional lock.** Handles are not a precise instrument, so
[ArtboardCanvasEditView.cs](../Sources/Core/Pix2d.Core/UI/ArtboardCanvasEditView.cs) also types into the same
preview-only frame: **Width / Height** boxes, a **lock** toggle, and — for Resize only, where the pixels are
actually scaled — a **Scale %** box relative to the artboard's size at session start
(`IArtboardObjectEditService.OriginalSize`). All of them go through `SetFrameSize`, which pins the frame's
top-left and sanitizes through [`CanvasSize`](../Sources/Core/Pix2d.Shared/Primitives/CanvasSize.cs) (the
boxes carry `Minimum`/`Maximum` too — a text box is exactly how the *"Unable to allocate pixels"* signature
was produced once already). The bar follows a handle drag via
[ArtboardObjectEditFrameChangedMessage.cs](../Sources/Core/Pix2d.Shared/Messages/ArtboardObjectEditFrameChangedMessage.cs),
and leaves the box being typed into alone while re-syncing the derived ones (`PushFrameSize`) — otherwise a
multi-digit number cannot be typed.

The lock (`IArtboardObjectEditService.KeepAspect` → `ArtboardObjectEditorNode.KeepAspect`) defaults **per
sub-mode**, reset by every `Begin`: **on for Resize** (scaling artwork non-uniformly is the exception),
**off for Crop** (an arbitrary region is the point). **Shift inverts** whatever is in force, read live from
`SKInput.GetModifiers()` on every move — the same convention as `SnappingService.IsAspectLocked` — so it can
be pressed or released mid-drag. A locked corner drag follows the axis the pointer took further (compared in
ratio-normalized terms) and pins the opposite corner; a locked **edge** drag scales the cross axis about the
frame's centre line, so the frame scales in place instead of drifting to one side. The ratio comes from the
frame at drag start, not from the artboard, so a locked drag after an unlocked one keeps what is on screen.
Because a locked gesture drives the *cross* axis too, the min-1px clamp lives in one `NormalizeFrame` helper
that checks both axes (the old per-handle clamp only guarded the dragged one).

**The object frame after Apply.** Ending a session runs `ResetFrame()` + `Invalidate()` on the selection: the
visible object frame is `MoveThumbNode`, which sizes itself from `NodesSelection.Frame` — a node kept across
`Invalidate()` calls because it carries a rotation that recomputed bounds cannot restore — so without the
reset a 64 px frame stayed around a 96 px artboard. `SelectionService` does the same on undo/redo; an applied
operation needed it too.

UI / wiring:
- [ObjectActionsBarView.cs](../Sources/Core/Pix2d.Core/UI/ObjectActionsBarView.cs) — the General action bar
  (Resize / Crop / Rename / Arrange / Up / Down / Delete), the artboard-level counterpart of the Sprite
  context's `ActionsBarView`. Buttons self-disable by selection shape: any selection for Delete and z-order,
  ≥2 artboards for Arrange, exactly one for Resize / Crop / Rename — `ExecuteCommandAsync` does **not** gate
  on `EditContextType`, so the view is the guardrail for click-invoked commands. Visibility follows the top
  bar's **Tools** toggle (`UiState.ShowExtraTools`) as well as the context, so one switch dismisses whichever
  action bar the current context shows.
- [ArtboardCanvasEditView.cs](../Sources/Core/Pix2d.Core/UI/ArtboardCanvasEditView.cs) — mode title +
  **Apply / Cancel** while a Resize/Crop session is open (replaces the old `SpriteActionsView`). The General
  action bar hides itself for the duration.
- All three top-center bars (`ActionsBarView`, `ObjectActionsBarView`, `ArtboardCanvasEditView`) share one
  slot in `MainView`'s overlay grid and are mutually exclusive; `ActionsBarView` is gated on
  `MainViewModel.ShowSpriteExtraTools` (the user's toggle **and** Sprite context) and `ObjectActionsBarView`
  on the same toggle **and** General context. `ArtboardCanvasEditView` deliberately ignores the toggle — its
  Apply / Cancel is the only way out of a canvas-edit session. The top bar swaps **Clear** (Sprite) for
  **Delete** (General), and that Delete stays available with the toggle off (context-gated only).
- View ↔ service is driven by [ArtboardObjectEditStateChangedMessage.cs](../Sources/Core/Pix2d.Shared/Messages/ArtboardObjectEditStateChangedMessage.cs)
  (raised on begin / end).
- [ArtboardObjectEditorNode.cs](../Sources/Core/Pix2d.Shared/InteractiveNodes/ArtboardObjectEditorNode.cs) —
  the frame overlay: corner/edge handles + size badge, the live result preview (stretched snapshot / crop
  shield, drawn under the chrome so handles and badge stay readable), a body blocker and a full-viewport
  backdrop that swallow presses so they never reach a drawing tool or the object tool. The old label-drag thumb is gone
  (moving is the General context's job); `ArtboardLabelsLayer.GetLabelRect(vp, sprite)` survives as the
  labels layer's own hit-test helper.

**Interactive QA (General context):**
1. Single-click a label → that artboard becomes active (highlight border + Layers/Timeline follow), context
   stays Sprite. Double-click a label → General context, artboard selected, object action bar appears, the
   toolbar shows only the arrow tool and no color/brush buttons.
2. Click / Shift+click / rubber-band select artboards; drag one → moves in the same gesture, one undo step.
3. Delete (key or button) → confirmation naming the object(s); confirm removes them, Ctrl+Z restores;
   deleting the active artboard re-targets a survivor.
4. Arrange → selection repacks into a dense grid with same-prefix artboards ("icon-goal-*") grouped into
   their own row blocks, one undo step. Toggling **Tools** off in the top bar hides the whole action bar.
5. Resize: drag a corner → the artboard itself stretches/squashes live (nearest-neighbour, on the checkerboard;
   shrinking vacates scene background, no ghost of the original left behind), keeping its proportions — the bar's
   lock starts on for Resize; holding **Shift** frees the ratio for that drag (and locks it when the toggle is
   off). Apply → content scales to the framed size, anchored at the opposite corner, and the object frame snaps
   to the new canvas; Undo reverts in one step and the frame follows back. Cancel/Esc discards, ends the session,
   and the artboard paints itself again.
5a. Resize by keyboard: type into **Width** (locked → Height follows, unlocked → it does not), or into
   **Scale %** (200 doubles the canvas, 50 halves it — always proportional). The frame's top-left stays put, the
   badge and the other boxes follow, and nothing reaches the document until Apply. A nonsense value (0, a huge
   typo) clamps instead of being applied.
6. Crop: drag handles → everything outside the frame dims (content still visible, so you can see what is being
   trimmed); proportions start **unlocked** here, and Shift (or the bar's toggle) locks them. Apply → canvas trims/extends with no scaling; kept content stays anchored and the object frame
   matches the new canvas. Undo reverts.
7. Rename → dialog renames the artboard; the label updates.
8. Double-click an artboard body → back to Sprite context for it, brush tool active.

---

## Part B — implementation notes (DONE)

Implemented across these files (deviations from the original plan called out inline):

- **B0** — [ViewPortState.cs](../Sources/Core/Pix2d.Shared/State/ViewPortState.cs): `Zoom` (0 = never framed
  → fallback `ShowAll()`) and `Pan`. [ProjectState.cs](../Sources/Core/Pix2d.Shared/State/ProjectState.cs):
  `Guid Id` (keys the per-project undo history). [IPlatformStuffService.cs](../Sources/Core/Pix2d.Shared/Abstract/Services/IPlatformStuffService.cs):
  `SupportsMultipleProjects` as a **default interface member returning false**, overridden to `true` only in
  the desktop [PlatformStuffService.cs](../Sources/Core/Pix2d.Core/Services/PlatformStuffService.cs) — Android/Browser
  services untouched.
- **B1** — [AppState.cs](../Sources/Core/Pix2d.Shared/State/AppState.cs): observable `ActiveProjectIndex`;
  `LoadedProjects` stays a plain `List`. New messages: `ProjectActivatedMessage(ProjectState)`,
  `ProjectsListChangedMessage`.
- **B2** — [ProjectActivationService.cs](../Sources/Core/Pix2d.Core/Services/Project/ProjectActivationService.cs)
  (`IProjectActivationService` in Shared). Two entry points: `ActivateProject(target)` for switching between
  loaded tabs (full sequence: save outgoing Pan/Zoom → stop editor / cancel drawing → drain + MarkAllDirty
  the autosave tracker → `SetActiveHistory` → set `CurrentProject` → `SetScene` → `RequestEdit` on the
  target's remembered `CurrentEditedNode` (falls back to first sprite) → restore Pan/Zoom or `ShowAll` →
  `ProjectActivatedMessage` + refresh), and `BeginNewProjectActivation(newProject)` — the deactivation half
  only, used by ProjectService before running the regular fresh-load path for a brand-new tab.
  SpriteEditor / IDrawingService / IProjectChangeTracker are resolved lazily via IServiceProvider.
- **B3** — [OperationService.cs](../Sources/Core/Pix2d.Core/Services/OperationService.cs): stacks +
  `_currentOperation` moved into a per-project `History`, `Dictionary<Guid, History>`,
  `SetActiveHistory(Guid)` / `RemoveHistory(Guid)` on `IOperationService`. **Deviation:** the disk cache did
  NOT need per-project namespacing — `CachedPayload` keys are GUID-based (`payload_<guid>`), so entries never
  collide; `Clear()` only skips `ClearAll()` when other histories exist.
- **B4** — [StateExtensions.cs](../Sources/Core/Pix2d.Shared/Abstract/State/StateExtensions.cs):
  `WatchForCurrentProject(...)` / `WatchForCurrentProjectViewPort(...)` re-bind on `CurrentProject` change and
  invoke the callback after a re-bind. Applied at all 11 stale sites (ToolBarView, ArtworkPreviewView,
  TimeLineView, AdditionalTopBarView, LayersView, BackgroundSelectorView, ViewPortRefreshService ×2,
  ToolService, SnappingService ×2). Also: TopBarView updates the undo counter and AnimationControlsView
  re-syncs onion-skin on `ProjectActivatedMessage`.
- **B5** — [EditService.cs](../Sources/Core/Pix2d.Core/Services/EditService.cs): `FrameEditorNode` created
  lazily per project (`??=` in the accessor), ctor init removed.
- **B6** — [ProjectService.cs](../Sources/Core/Pix2d.Core/Services/Project/ProjectService.cs): `OpenFilesAsync`
  and `CreateNewProjectAsync` branch on `SupportsMultipleProjects && CurrentProject.SceneNode != null` (the
  startup placeholder has no scene and keeps the replace path, so the first load never leaves a phantom empty
  tab). Opening a file already open in a tab just activates that tab. `EnsureCurrentProjectIsListed()` runs on
  every `ProjectLoadedMessage` — this is how the recovery path (which sends the message directly from
  AutoSaveService) gets its tab. Window title re-applied on `ProjectActivatedMessage`.
  [FileCommands.cs](../Sources/Core/Pix2d.Shared/Commands/FileCommands.cs): `NewTab` (Ctrl+T), `CloseTab`
  (Ctrl+W), both no-op unless `SupportsMultipleProjects`.
- **B7** — [ProjectTabsView.cs](../Sources/Core/Pix2d.Core/UI/ProjectTabsView.cs) in `UiGrid` Row 0 of
  [MainView.cs](../Sources/Core/Pix2d.Core/UI/MainView.cs). ListBox + `BulkAddObservableCollection` +
  `SelectedIndex` TwoWay + `_isSyncing`; tab = title + `•` dirty marker + ✕ button; trailing "+" button.
  Rebuild on `ProjectsListChangedMessage`, selection sync on `ProjectActivatedMessage`, dirty/title refresh on
  `ProjectLoadedMessage`/`ProjectSavedMessage`/`OperationInvokedMessage`. Hidden (and no subscriptions) when
  `!SupportsMultipleProjects`.
- **B8** — `CloseProjectAsync(ProjectState)` on `IProjectService`: dirty → activate + reuse
  `AskSaveCurrentProject`; remove from list; closing the active tab activates the right/last neighbor;
  closing the last tab creates a fresh blank 64×64 project; `RemoveHistory(p.Id)` + `SceneNode.Unload()` only
  AFTER the replacement scene is current.
- **B9 (superseded)** — the original "active tab only" autosave was replaced by full multi-tab session
  persistence, see the next section.

### Multi-tab autosave & workspace restore (v2, DONE)

Every open tab is persisted and the whole tab set is restored on launch. Verified end-to-end: a 2-tab
workspace round-trips through close → relaunch ("recovered workspace with 2 tab(s)" in the log, both
projects force-committed on close, active index preserved).

- [ProjectChangeTracker.cs](../Sources/Core/Pix2d.Core/Services/AutoSave/ProjectChangeTracker.cs) — dirty
  cells are bucketed **per project** (`Dictionary<Guid, Bucket>`, attributed to the active project at
  operation time). A tab switch no longer discards pending changes — the outgoing tab's bucket stays parked
  until the next tick. `IProjectChangeTracker` gained `Drain(Guid)`, `Reapply(Guid, DirtySet)`,
  `MarkAllDirty(Guid)`, `GetDirtyProjectIds()`, `Forget(Guid)`.
- [AutoSaveService.cs](../Sources/Core/Pix2d.Core/Services/AutoSave/AutoSaveService.cs) — one
  `IncrementalSessionStore` (work folder) **per open project**, keyed by `ProjectState.Id`
  (`ConcurrentDictionary`); folder name = project id for new tabs, the claimed folder's original id for
  restored ones. The tick drains every project's bucket and commits each into its own store; a project that
  has a scene but no store yet is marked all-dirty so it gets fully committed within one tick of appearing
  (covers freshly opened tabs with zero edits). `ForceSaveSync`/`ForceSaveAsync` flush ALL tabs (commit task
  owns snapshot disposal, so a timed-out wait can never dispose images under an in-flight commit).
- `workspace.json` at the sessions root ([WorkspaceManifest.cs](../Sources/Core/Pix2d.Shared/Project/AutoSave/WorkspaceManifest.cs))
  — ordered tab list (`sid` = session folder, `src` = backing file path, `dirty` = per-tab unsaved-changes
  flag) + active index. Rewritten atomically after every successful commit batch and on
  `ProjectActivatedMessage`/`ProjectsListChangedMessage`/`ProjectSavedMessage` (the last so a Ctrl+S clears the
  persisted dirty flag immediately). Desktop-gated (`SupportsMultipleProjects`); mobile/browser keep the legacy
  single-session behavior.
- Recovery (`TryRecoverAsync`) tries `TryRecoverWorkspaceAsync` first: claims each listed folder (skipping
  ones locked by another live instance), rebuilds each scene, restores `ProjectState.File` from `src` (via
  `NetFileSource`, when the file still exists), restores each tab's `HasUnsavedChanges` from the persisted
  `dirty` flag (so a tab that was clean on shutdown comes back clean; old manifests without the field default
  to dirty, preserving legacy behaviour), adds them to `LoadedProjects`, then activates the recorded active tab
  through `BeginNewProjectActivation` + the regular `ProjectLoadedMessage` pipeline. Falls back to the legacy
  most-recent-orphan recovery when there is no usable workspace file (migration path).
- Closing a tab calls `IAutoSaveService.DiscardProjectSessionAsync(projectId)` (new interface member): forgets
  the tracker bucket and deletes the session folder, so a deliberately closed tab is not resurrected.
- [Pix2dBootstrapperDI.cs](../Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs) `TryLoadStartupDocument`: on
  desktop, opening the app via a document (double-click in Explorer) now restores the workspace FIRST and
  opens the document on top as its own tab — this path previously skipped session load (and the autosave
  loop!) entirely. Mobile keeps the old direct-open behavior.

Known quirks:
- Restored tabs are always marked dirty (`•`) — the session content may be ahead of the backing file, so the
  save prompt on close errs on the safe side (same as the legacy single-session recovery).
- Two app instances share one `workspace.json` — last writer wins; locked session folders are never stolen,
  so no data is lost, but only one instance's tab set is restored next launch.
- Per-tab viewport zoom/pan is kept in memory across switches but not persisted across restarts (restored
  tabs are framed with `ShowAll`).
- Session folders of crashed runs that are not referenced by `workspace.json` are never garbage-collected
  (pre-existing behavior).

### Part B interactive QA checklist
1. "+" button / Ctrl+T → new "New project" tab appears and becomes active; Ctrl+W closes it.
2. Open 2–3 projects (menu Open / drag-drop / MRU) → each opens in its own tab; switching is instant; each
   keeps its own scene, zoom/pan, active artboard, layers/timeline content.
3. Edit in A, switch to B, Undo → rolls back B only; switch back to A → its history intact; undo counter in
   the top bar matches the active tab.
4. Dirty tab shows `•`; closing a dirty tab prompts to save; closing the last tab leaves a fresh blank project.
5. Open the same file twice → second open just activates the existing tab.
6. Open 2–3 tabs, edit in several, kill the process → relaunch restores ALL tabs with their content, the
   previously active tab is active again.
7. Close a tab, exit, relaunch → the closed tab does NOT come back.
8. Double-click a .pix2d file in Explorer → previous tabs are restored AND the file opens as a new active tab.
9. Browser/Android heads: no tab strip, single-project replace behavior unchanged.

---

## Part B — original plan (kept for reference)

Incremental, each phase independently testable. File-level steps.

### B0 — foundation
- `ViewPortState` ([ViewPortState.cs](../Sources/Core/Pix2d.Shared/State/ViewPortState.cs)): add `Zoom` and `Pan` so each project persists its framing.
- `IPlatformStuffService` + [PlatformStuffService.cs](../Sources/Core/Pix2d.Core/Services/PlatformStuffService.cs): add `bool SupportsMultipleProjects => CurrentPlatform == CrossPlatformDesktop` — single gating point.
- `ProjectState` ([ProjectState.cs](../Sources/Core/Pix2d.Shared/State/ProjectState.cs)): add `Guid Id` (key for the undo-history dictionary and disk-cache/session namespace).

### B1 — state model
- `AppState` ([AppState.cs](../Sources/Core/Pix2d.Shared/State/AppState.cs)): use `LoadedProjects`; add observable `ActiveProjectIndex`. Invariant: when the list is non-empty, `CurrentProject == LoadedProjects[ActiveProjectIndex]`.
- Do **not** convert `LoadedProjects` to `ObservableCollection` (~110 `CurrentProject` references across ~41 files); signal list changes with a message instead.
- New messages in `Sources/Core/Pix2d.Shared/Messages/`: `ProjectActivatedMessage(ProjectState)` and `ProjectsListChangedMessage`. **Keep these distinct from `ProjectLoadedMessage`**, which triggers heavy work (`OperationService.Clear()`, sprite activation + `ShowAll()`, `SnappingService` grid reset).

### B2 — switch mechanics (core)
New `Services/Project/ProjectActivationService.cs` (singleton in [Pix2dBootstrapperDI.cs](../Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs)). `ActivateProject(target)` sequence:
1. No-op if already active.
2. Save outgoing `ViewPort.Pan/Zoom` into `CurrentProject.ViewPortState`.
3. `spriteEditor.Stop()`; `drawingService.CancelCurrentOperation()`.
4. Drain + discard the autosave tracker dirty set (so cells from the outgoing project don't leak in).
5. `operationService.SetActiveHistory(target.Id)`.
6. Set `ActiveProjectIndex` and `CurrentProject = target` (fires `WatchFor(x => x.CurrentProject)` consumers).
7. `SKApp.SceneManager.SetScene(target.SceneNode)` — **without** sending `ProjectLoadedMessage`.
8. Re-target the editor: `editService.RequestEdit([firstSpriteOf(target)])` (mirrors `EditService.OnProjectLoadedMessage`) → SpriteEditor / drawing target / Layers / Timeline refresh.
9. Restore `ViewPort.SetZoom/SetPan` from `target.ViewPortState` (fallback `ShowAll()` if never framed); apply grid.
10. Update window title; send `ProjectActivatedMessage` + `ProjectsListChangedMessage`; `viewPortRefreshService.Refresh()`.

Ordering matters: set `CurrentProject` before `SetScene` (`SceneService.SceneCreated` writes `SceneNode`); re-target the editor after `SetScene`; restore Pan/Zoom after the scene is set.

### B3 — undo/redo per project
- `OperationService` ([OperationService.cs](../Sources/Core/Pix2d.Core/Services/OperationService.cs)): store the undo/redo stacks + `_currentOperation` in a `Dictionary<Guid, History>` keyed by `ProjectState.Id`; add `SetActiveHistory(id)` / `RemoveHistory(id)`; the `ProjectLoadedMessage` handler clears only the just-loaded project's history.
- `OperationDiskCacheService.cs`: namespace cache paths per project id (otherwise two tabs' index-based filenames collide); add `ClearScope(Guid)`.

### B4 — re-bind stale `WatchFor` subscriptions (top risk)
Add a helper next to `WatchFor` (`StateExtensions`) that, on `AppState.WatchFor(x => x.CurrentProject, Rebind)`, unwatches the old project's sub-state and re-watches the new one. Apply at the ~8 sites that subscribe to `AppState.CurrentProject.*` or `...ViewPortState.*`: `ViewPortRefreshService`, `SnappingService`, `ToolService`, `AdditionalTopBarView`, `ToolBarView`, `LayersView`, `TimeLineView`, `ArtworkPreviewView`, `BackgroundSelectorView`. `SnappingService` must NOT zero the grid on a plain switch (only on `ProjectLoadedMessage`).

### B5 — per-project FrameEditorNode
- [EditService.cs](../Sources/Core/Pix2d.Core/Services/EditService.cs): create `FrameEditorNode` lazily per project (on activate/load) instead of once in the ctor.

### B6 — open/new ADD a tab (desktop)
- [ProjectService.cs](../Sources/Core/Pix2d.Core/Services/Project/ProjectService.cs): in `OpenFilesAsync` and `CreateNewProjectAsync`, branch on `SupportsMultipleProjects`. Desktop: build a new `ProjectState` (new Id, own Selection/FrameEditorNode), set File/scene, add to `LoadedProjects`, send `ProjectLoadedMessage` for per-project init (clean history, sprite activation), then `ActivateProject(new)`. Non-desktop: keep the current replace path.
- `IProjectService`: add `CloseProjectAsync(ProjectState)` + list/activation access. Fire `UpdateProjectNameInWindowTitle` from a `ProjectActivatedMessage` subscription.
- [FileCommands.cs](../Sources/Core/Pix2d.Shared/Commands/FileCommands.cs): desktop `NewTab` (Ctrl+T), `CloseTab` (Ctrl+W).

### B7 — tab bar UI (desktop only)
- New `UI/ProjectTabsView.cs` using the `ViewBase<State>` + `ObservableObject` + `WatchFor` + `_isSyncing` pattern (model on [AdditionalTopBarView.cs](../Sources/Core/Pix2d.Core/UI/AdditionalTopBarView.cs)); ListBox + `BulkAddObservableCollection` + `ItemTemplate` + `SelectedIndex` TwoWay (model on [TimeLineView.cs](../Sources/Core/Pix2d.Core/UI/Animation/TimeLineView.cs)). Each tab: `Title` + dirty `*` + close button; plus a "+" button. Rebuild on `ProjectsListChangedMessage`, update selection on `ProjectActivatedMessage`, refresh dirty on `ProjectSavedMessage`/`OperationInvokedMessage`. `SelectedIndex` change → `ActivateProject`.
- [MainView.cs](../Sources/Core/Pix2d.Core/UI/MainView.cs): place `ProjectTabsView` in `UiGrid` **Row 0** (empty today; no row re-indexing — `TopBarView` stays on Row 1; menu/loading overlays already span from Row 0). Gate visibility to desktop.

### B8 — close a tab
- [ProjectService.cs](../Sources/Core/Pix2d.Core/Services/Project/ProjectService.cs): `CloseProjectAsync(p)` — dirty-check (activate `p`, reuse `AskSaveCurrentProject`); pick a neighbor; remove from `LoadedProjects`; `RemoveHistory(p.Id)` + `ClearScope(p.Id)`; activate the neighbor (`SetScene`) THEN `p.SceneNode?.Unload()` + dispose Selection/FrameEditorNode; if it was the last tab, create a fresh blank project (matches `Pix2dBootstrapperDI.TryLoadStartupDocument`); send `ProjectsListChangedMessage`.

### B9 — autosave scoping (active tab only, v1)
- `ProjectChangeTracker.cs`: on `ProjectActivatedMessage`, `Drain()` and discard.
- `AutoSaveService.cs`: no structural change for v1; add a comment/log that autosave + recovery is active-tab-only; multi-session recovery (one session folder per `ProjectState`, restoring N tabs) is v2.

### Part B risks
- Stale `WatchFor` (B4) is mandatory or sub-panels silently stop updating after a switch.
- `ActivateProject` ordering (B2).
- Do not send `ProjectLoadedMessage` on a switch — audit every `Register<ProjectLoadedMessage>` (SceneService, EditService, OperationService, SnappingService, ToolService, ViewPortService, ViewPortRefreshService, DrawingService) and classify "fresh-load only" vs "also on activate".
- `SetScene` does not dispose the outgoing scene — only tab CLOSE should `Unload`.
- Memory grows with open tabs (scene + undo stacks each); consider a soft cap.

### Part B verification
1. Open 2–3 projects → tabs on top (desktop only); switching is instant; each keeps its own scene, zoom/pan, selection.
2. Edit in A, switch to B, **Undo** → rolls back B (not A); switch back to A → its history intact.
3. Dirty project marked `*`; closing a tab prompts to save; closing the last tab → fresh blank project.
4. Mobile/WASM head: single-project behavior, no tabs (gated by `SupportsMultipleProjects`).
5. Crash with active tab B → B is recovered (v1 limitation).

---

## Build / run

```
dotnet build Sources/Heads/Pix2d.Desktop          # Core + Shared + SkiaNodes + Desktop
dotnet run   --project Sources/Heads/Pix2d.Desktop
```

There is no test project; QA is manual (sample files under `TestImages/`).
