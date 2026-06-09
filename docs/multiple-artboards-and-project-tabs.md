# Multiple artboards & multiple-project tabs — implementation plan

Status board for a two-part feature:

- **Part A — multiple sprites (artboards) on one scene** — ✅ implemented (commit on branch
  `feature/multiple-artboards-and-project-tabs`). Needs interactive QA.
- **Part B — multiple open projects with a desktop tab bar** — ⏳ not started. Full plan below so it can be
  continued on another machine.

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

**Build:** Desktop head builds clean; app boots, recovers session, autosave runs. (The `Pix2d.Desktop.Wap`
packaging project fails to build locally — missing DesktopBridge tooling in the SDK; environment-only.)

**Interactive QA still needed:**
1. "Artboard" button / Ctrl+Alt+N → new artboard appears to the right, becomes active (highlight border);
   Undo removes it.
2. Two artboards: clicking the second makes it active; Layers/Timeline switch to its layers/frames; drawing
   lands in it.
3. Save a 2-artboard project → reopen: both present at correct positions; thumbnail is a composite.
4. Kill the process mid-edit of the second artboard → autosave recovery contains both artboards' content.
5. Single-sprite regression: new project / PNG import / export unchanged.

---

## Part B — TODO (multiple-project tabs, desktop only)

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

There is no test project; QA is manual (sample files under `TestImages/`). The `Pix2d.Desktop.Wap`
packaging project may fail locally without DesktopBridge tooling — build the `Pix2d.Desktop` head directly.
