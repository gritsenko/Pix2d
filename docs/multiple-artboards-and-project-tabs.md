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

## Part A — "edit sprite as object" mode + SpriteActionsView (DONE)

Single-clicking an artboard's name label makes that artboard the active one (`IEditService.ActivateArtboard`,
same as clicking the artboard body). Double-clicking the label enters **object-edit mode** for that sprite. The mode is a small
state machine owned by [ArtboardObjectEditService.cs](../Sources/Core/Pix2d.Core/Services/ArtboardObjectEditService.cs)
with three sub-modes ([ArtboardObjectEditMode.cs](../Sources/Core/Pix2d.Shared/Primitives/ArtboardObjectEditMode.cs)):

- **Move** (default after selection) — the artboard is dragged **only by its name label** (no body drag);
  the interior is covered by an invisible blocker so a click there can't start a stray brush stroke. A press
  on the empty space outside the artboard ends the session (same as the toolbar's **Done** button). Each
  finished label drag commits one undoable `MoveOperation`.
- **Resize** — frame handles scale the pixel content (nearest-neighbour) to the new size on **Apply**
  ([ResizeArtboardScaleOperation.cs](../Sources/Core/Pix2d.Core/Plugins/Sprite/Operations/ResizeArtboardScaleOperation.cs),
  uses `Pix2dSprite.ResizeImage` — the *scaling* path, despite the name).
- **Crop** — frame handles change the canvas without scaling (trim / extend), committed on **Apply** via the
  existing `ResizeArtboardOperation` (`Pix2dSprite.Crop`).

Resize/Crop only preview the working frame rect; the sprite pixels are untouched until Apply, so one Ctrl+Z
reverts the whole gesture. **Cancel** (or Esc) discards the preview and returns to Move; **Esc** from Move
exits the session. Resize/Crop ignore clicks outside the frame — they are confirmed only from the toolbar.

UI / wiring:
- [SpriteActionsView.cs](../Sources/Core/Pix2d.Core/UI/SpriteActionsView.cs) — contextual toolbar floating
  top-center of the canvas, placed in `MainView`'s overlay grid next to `ActionsBarView`. Self-hides when the
  session is inactive. Move mode shows **Resize / Crop / Set name / Done**; Resize&Crop show the mode title +
  **Apply / Cancel**. **Set name** opens `IDialogService.ShowInputDialogAsync` and renames the artboard
  (label updates live; not undoable in v1).
- View ↔ service is driven by [ArtboardObjectEditStateChangedMessage.cs](../Sources/Core/Pix2d.Shared/Messages/ArtboardObjectEditStateChangedMessage.cs)
  (raised on begin / mode switch / end).
- [ArtboardObjectEditorNode.cs](../Sources/Core/Pix2d.Shared/InteractiveNodes/ArtboardObjectEditorNode.cs) —
  mode-aware overlay: a label-drag thumb positioned over the name label via the new
  `ArtboardLabelsLayer.GetLabelRect(vp, sprite)` helper (Move only), a body blocker (all modes), and the
  corner/edge handles + size badge (Resize/Crop only).
- `ArtboardObjectEditService` now also depends on `IDialogService` (DI comment updated at
  `Pix2dBootstrapperDI.cs`). Esc routing lives in `SpriteEditCommands.Cancel` → `service.OnEscape()`.

**Interactive QA still needed (object-edit):**
1. Single-click a label → that artboard becomes active (highlight border + Layers/Timeline follow), no toolbar.
   Double-click a label → toolbar appears (Resize / Crop / Set name / Done); the artboard highlights.
2. Move mode: drag the **label** moves the artboard; dragging the body does nothing; clicking outside (or
   Done) exits; the move is a single undo step.
3. Resize: drag a corner, Apply → content scales to the new size, anchored at the opposite corner; Undo
   reverts in one step. Cancel/Esc discards.
4. Crop: drag handles, Apply → canvas trims/extends with no scaling; kept content stays anchored. Undo reverts.
5. Set name → dialog renames the artboard; the label updates.
6. Esc from Resize/Crop returns to Move; Esc from Move exits.

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

There is no test project; QA is manual (sample files under `TestImages/`). The `Pix2d.Desktop.Wap`
packaging project may fail locally without DesktopBridge tooling — build the `Pix2d.Desktop` head directly.
