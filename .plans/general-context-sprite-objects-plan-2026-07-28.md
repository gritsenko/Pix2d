## Plan: General Context for Sprite Objects

Restore a first-class General context for sprite-object workflows on Desktop, with dedicated UI surfaces and commands (no branching in existing sprite handlers), while preserving current sprite-edit behavior. Entry/exit is double-click driven (label -> General context, sprite content -> Sprite mode) — **note this is a behavior change, not a preservation**: today a label double-click enters `ArtboardObjectEditService`'s object-edit mode while `CurrentContextType` stays `Sprite` (see findings below). Deletion in General becomes confirmed + undoable; multi-select (Shift) and multi-drag are enabled; toolbar/top/action UI switches by context; add one new compact-grid arrangement command.

**Current-state findings (verified against code, 2026-07-28)**

These correct the assumptions the original plan was written under:

1. **Label double-click does NOT lead to General today.** `ArtboardObjectEditService.Begin()` calls `IEditService.ActivateArtboard` -> `RequestEdit`, which sets `CurrentContextType = EditContextType.Sprite` (EditService.cs:321). Object-edit mode (Move/Resize/Crop + `SpriteActionsView`) is a Sprite-context overlay, orthogonal to `EditContextType`. Making label double-click enter General requires an activation path that does not force Sprite context, and an explicit decision on the fate of `ArtboardObjectEditService` (see Further considerations #4).
2. **Switching to General currently crashes tool activation.** No tool is registered for `EditContextType.General` (all `RegisterTool` calls use Sprite), and `ToolService.ActivateDefaultTool` falls back to `_defaultContextTool[EditContextType.General]` (ToolService.cs:75) -> `KeyNotFoundException`. `GlobalCommands.SwitchToFullMode` (Ctrl+F12) already flips the context to General today and lands in this broken state — a useful smoke test.
3. **Shift multi-select already exists, unused.** `SelectionService.Select(SKPoint/SKRect, addToSelection)` fully implements add + toggle-remove (SelectionService.cs:120-143); no caller exists today. The new General tool only needs to call it. Caveat: `Select(point)` on empty space falls back to selecting the container node (line 93) rather than clearing — verify/adjust for the click-empty-clears UX.
4. **Multi-drag already exists via `FrameEditorNode`.** It drives move/resize/rotate through `NodesSelection.InitOperation<MoveOperation>()`/`FinishOperation()` (one undoable op per gesture), and `EditService.UpdateEditors` already attaches/shows it whenever context != Sprite and a selection exists (hidden while an `IDrawingTool` is active). Phase 2.2 is wiring/QA, not new implementation.
5. **Delete in General is confirmed non-undoable** (`NodesSelection.Delete()` just calls `RemoveFromParent`) — plan step 3.1 stands. `DeleteNodesOperation` exists and is the right vehicle. Additional gap: deleting the currently edited sprite leaves `CurrentEditedNode`/drawing target dangling — `EditService.OnOperationInvoked` rescues onto a survivor only on undo/redo, so the delete command must do the same on perform.
6. **`EditCommands.CancelSelection` (Esc, General) is a no-op bug** — the lambda resolves `ISelectionService` and does nothing with it (EditCommands.cs:34-35). Fix alongside Phase 3.
7. **UI context-switching plumbing already exists.** `ToolBarView` rebuilds the tool list per `CurrentContextType` and hides color/brush buttons via `IsSpriteEditMode`; `AdditionalTopBarView` hides Animate/Layers toggles via `IsSpriteContext`. Phase 4.3 is mostly "provide General content", not "build the switching".
8. **General context already claims shortcuts**: Delete, Esc, Ctrl+C/V/X (trace-only placeholders in `ClipboardCommands`), Ctrl+[ / Ctrl+] (arrange), Ctrl+F12 (SwitchToSpriteMode). `CommandService` gates *shortcuts* by context, but `ExecuteCommandAsync` does NOT gate on `EditContextType` (documented in ScenarioTests) — UI buttons must gate themselves (Phase 5.3).
9. **No roadmap entry exists** for General-context restoration — per CLAUDE.md, add one (don't just tick), see Phase 7.

**Progress — COMPLETE (2026-07-28)**

*Increment 1 — Phase 1.2 + Phase 2 (the tool).* `ObjectManipulationTool` ([Sources/Core/Pix2d.Core/Tools/ObjectManipulationTool.cs](../Sources/Core/Pix2d.Core/Tools/ObjectManipulationTool.cs), resurrected from the pre-`4bea1ad` tool and modernized, Figma-style, arrow-cursor icon) is registered by `SpritePlugin` as the General context's default tool. Delivered: click select / Shift+click toggle (new `MoveThumbNode.PassShiftPressThrough`, enabled only on EditService's object-selection `FrameEditorNode`, so a Shift-press falls through the selection frame to the tool), one-gesture select-and-drag committing exactly one undoable `MoveOperation` (`FrameEditorNode.ActivateMoveThumb` fixed — it passed clickCount 0 and never started a drag session), rubber-band selection, hover highlight, double-click on an artboard → Sprite context (step 2.3). Findings #2 (KeyNotFoundException — `ActivateDefaultTool` now falls back safely), #3, #4 and #6 (Esc `CancelSelection`) resolved. Harness gained `PressWorld`/`MoveWorld`/`ReleaseWorld`/`ClickWorld` + `SetView` (gesture tests must pin 1:1 zoom — screen-pixel-sized thumb hit zones blanket the artboards at the 64px harness viewport's ShowAll zoom).

*Increment 2 — Phases 1.1, 1.3, 3, 4, 5, 7 (everything else).* Decision #4 resolved as recommended, **option (a)**: General supersedes the object-edit mode's Move half, and `ArtboardObjectEditService` becomes a Resize/Crop sub-mode service behind the new Shared-side `IArtboardObjectEditService`. Shipped:
- **Entry/exit (1.1):** `IEditService.EditArtboardAsObject` — activates the artboard *then* switches to General (so it stays the edit target), wired to the label double-click and to Ctrl+F12 (`SwitchToFullMode`, which previously called `ApplyCurrentEdit()` and detached `CurrentEditedNode`). Body double-click still returns to Sprite.
- **Move-only object frame (1.3):** new `FrameEditorNode.AllowRotate` + `AllowResize = false` on the object-selection frame — the generic thumbs commit a plain `TransformOperation`, which would change a `Pix2dSprite`'s `Size` without touching its layer bitmaps, and the pixel pipeline has no rotated canvas.
- **Commands (3):** `Edit.Delete` → confirm dialog (count + first name) + `DeleteNodesOperation` + survivor re-target (shared with the undo/redo rescue; clears the target when no artboard is left); new `Edit.Arrange.CompactInRows` → dense `ceil(sqrt(n))`-column grid in reading order, one `MoveOperation`; z-order commands made null-safe.
- **Sub-mode cleanup:** `ArtboardObjectEditMode.Move`, the label-drag thumb, the backdrop-exit action and the Move branch of the toolbar are deleted; a session is one Resize or one Crop and ends on Apply *or* Cancel. Esc for it lives in `EditCommands.CancelSelection`; the dead branch in `SpriteEditCommands.Cancel` is gone.
- **UI (4):** new `ObjectActionsBarView`; `SpriteActionsView` → `ArtboardCanvasEditView`; top bar swaps Clear (Sprite) for Delete (General); `ActionsBarView` gated on the new `MainViewModel.ShowSpriteExtraTools`.
- **Localization + guardrails (5):** en + ru strings added (also filling pre-existing gaps: "Crop", "Set name", "Artboard name"); every bar button disables itself by selection shape, since `ExecuteCommandAsync` does not gate on `EditContextType`.
- **Bug found while wiring (worth remembering):** a command list held as a *property* of another list (`EditCommands.Arrange`/`.Clipboard`) is a second, uninitialized instance — `CommandService` injects its services only into lists it registers — so its first `GetCommand` threw `NullReferenceException` and crashed `MainView` construction. Fixed by resolving through `ICommandService.GetCommandList<T>()`; both dead properties removed and the trap documented in CLAUDE.md. Same investigation showed **`ClipboardCommands` is registered nowhere**, so the General Ctrl+C/V/X placeholders are unreachable dead code, not live commands — left alone (wiring them would change shortcut behaviour) and the ROADMAP claim corrected.

**Verification (6):** ScenarioTests 52/52 assertions green + 76-command sweep with 0 findings; whole solution builds; live Debug build checked through the AgentTools inspector (entering General renders the object bar, top-bar Delete replaces Clear, Sprite extra-tools bar + color/brush buttons disappear, a fresh Ctrl+T tab returns to Sprite with them restored, `get_errors` clean, `layout_audit` unchanged from the Sprite baseline). **Docs (7):** ROADMAP H1.1 entry, CLAUDE.md (artboard + General-context + command-list-trap notes), and `docs/multiple-artboards-and-project-tabs.md` Part A rewritten to the shipped design.

*Increment 3 — post-review fixes from a real build.* Two defects the headless tests could not see:
- **Context switch didn't repaint until the window was resized.** Root-caused with `get_layout`: `tools-panel` reported `DesiredSize 56` while `Bounds` stayed `295` — measure ran, arrange didn't, so the subtree kept its old geometry; the "resize fixes it" symptom was `MainView.UpdateResponsiveLayout` re-applying the Narrow/Wide style classes and forcing a full pass. Fixed with `ToolBarView.InvalidateLayoutChain()` (walks ancestors invalidating measure **and** arrange on context change). Separately, the color/brush `BlurPanel` was left visible with only its children hidden, so an empty rounded box floated above the tools bar in General — the panel itself is now gated on the Sprite context. Verified without touching the window: bounds track 392 → 56 → 392 across Sprite → General → Sprite and every bar swaps in both directions.
- **No visual differentiation of modes.** Sprite stays the unmarked default; other contexts show an accent pill in the info panel (the cursor-coordinate area) — `LAYOUT MODE` for General, plus 3D/Text labels. The alternative the user floated — a full VS Code-style status bar (mode + coordinates + zoom + selection, docked full-width) — was **not** done and remains a separate UI item.

**Notes for the next increment**
- `screenshot_window` (AgentTools) captures only the Avalonia visual tree; the `SkiaCanvas` area comes out blank even though the control reports `IsVisible` + full bounds. Canvas-level visuals (the move-only selection frame, artboard labels, the Resize/Crop frame) cannot be eyeballed that way — verify them in ScenarioTests or by hand.
- The two `layout_audit` text-clip warnings are pre-existing (present in the Sprite baseline too), not from the new bars.
- Mobile/touch parity for the General context is untouched — desktop-first by decision.
- `ClipboardCommands` is still registered nowhere (General Ctrl+C/V/X are dead code). Wiring it is a deliberate behaviour change and was left out of scope.

**Steps**
1. Phase 1 - Context backbone and switching flow (blocks all next phases)
1.1 Implement the context transition contract in EditService: an entry point that activates an artboard as the edit target *without* forcing Sprite context (today `RequestEdit` always sets Sprite for a `Pix2dSprite`), used by label double-click => General; sprite-content double-click => Sprite for the clicked sprite (via existing `RequestEdit`). Decide and encode the `ArtboardObjectEditService` relationship per Further considerations #4.
1.2 Create and register a General-context selection tool (e.g. `ObjectSelectionTool`, `IToolService.RegisterTool<T>(EditContextType.General)`, desktop head decides availability) — this is the dedicated interaction path for General scene selection/drag AND the fix for the `ActivateDefaultTool` KeyNotFoundException (also make that fallback safe for contexts with no registered tool, e.g. General3d). Wire Shift additive selection via the existing `ISelectionService.Select(..., addToSelection: true)`.
1.3 Ensure project-tab switching and viewport refresh preserve invariants (CurrentEditedNode, CurrentContextType, adorner/editor visibility) — remember `WatchForCurrentProject` for any new state watchers.
2. Phase 2 - General interaction model (depends on Phase 1)
2.1 Wire multi-select sprite-object picking in General through the new tool: single click select, Shift-toggle add/remove (already implemented in SelectionService), click-empty clears (adjust the container-fallback in `Select(SKPoint)` or clear explicitly from the tool).
2.2 Verify multi-drag for selected sprites works through the existing `FrameEditorNode` + `NodesSelection.InitOperation<MoveOperation>`/`FinishOperation` path (single undo step per gesture); fix gaps rather than build new machinery.
2.3 Implement sprite-content double-click hit handling in the General tool to switch back to Sprite context for the clicked sprite.
3. Phase 3 - General-specific commands (depends on Phase 2)
3.1 Add a new General delete command path: confirmation via existing `IDialogService.ShowYesNoDialog`, undoable deletion (`DeleteNodesOperation`-based) replacing the direct `Selection.Delete()` in `EditCommands.Delete`; on deleting the active artboard, re-activate a surviving artboard (mirror `EditService.OnOperationInvoked` survivor logic) so `CurrentEditedNode`/drawing target never dangle. Fix the no-op `CancelSelection` (Esc => `ClearSelection()`).
3.2 Add Compact in rows command for selected sprites using agreed rule A: dense row-wise placement with wrap by max row width and consistent spacing (desktop-first behavior); one undoable operation (MoveOperation over all affected nodes).
3.3 Keep existing arrange z-order commands (BringForward/SendBackward, already `EditContextType.General`) and ensure they operate on current selection under the new flow.
4. Phase 4 - Dedicated UI surfaces for General (parallel with late Phase 3 parts once command names/contracts are stable)
4.1 Create separate General top menu view/model segment (desktop-first), including a dedicated delete button (separate from the Sprite-context Clear button, which is `SpriteEditCommands.Clear` in TopBarView).
4.2 Create separate General ActionBar view/model with object-context actions (including Compact in rows trigger).
4.3 Extend the existing context switching (`ToolBarView.IsSpriteEditMode` / tool-list rebuild, `AdditionalTopBarView.IsSpriteContext`) so General renders its own tools/actions set; color/brush buttons are already hidden outside Sprite.
4.4 Keep existing Sprite views (TopBar sprite section, ActionsBarView, TopToolUiContainer) focused on Sprite context to avoid branching explosion.
5. Phase 5 - Wiring, localization, safeguards (depends on Phases 3-4)
5.1 Register new command list(s) in command service, assign shortcuts/context gates; General already owns Delete/Esc/Ctrl+C-V-X/Ctrl+[-]/Ctrl+F12 — no collisions with Sprite bindings, but new shortcuts must not collide with these either.
5.2 Localize new UI strings in Assets/strings.json and ensure tooltip text follows current conventions (invoke the pix2d-ui skill for view work).
5.3 Add guardrails for empty selection and unsupported states: disable buttons when no sprite objects are selected in General, and gate button-invoked commands themselves (ExecuteCommandAsync does not check EditContextType).
6. Phase 6 - Verification and regression checks (depends on all phases)
6.1 Desktop manual validation: mode switches (double-click label/content), Shift multi-select, multi-drag, delete-confirm-undo, compact rows, toolbar/top/action context swapping.
6.2 Keyboard validation: Delete, Undo/Redo, Esc, Ctrl+F12 both directions (SwitchToFullMode no longer crashes tool activation), relevant shortcuts while switching contexts and tabs.
6.3 Run build and scenario checks (`Sources/Tools/Pix2d.ScenarioTests` command sweep) to ensure no regressions in Sprite mode and command routing.
7. Phase 7 - Documentation sync (final)
7.1 Add a ROADMAP.md entry for the General-context/object-workflow restoration under the appropriate Horizon/Track (none exists today) and mark delivered scope; keep `Last updated` current.
7.2 Update agent-facing notes: CLAUDE.md's "Multiple artboards & open-project tabs" section describes the object-edit flow this work changes — it must be rewritten to match the shipped behavior (docs must never disagree with code); update docs/multiple-artboards-and-project-tabs.md likewise.

**Relevant files**
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Services\EditService.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Services\ArtboardObjectEditService.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Services\ToolService.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Services\SelectionService.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\GlobalCommands.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\InteractiveNodes\ArtboardLabelsLayer.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\InteractiveNodes\FrameEditorNode.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\Selection\NodesSelection.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\Operations\DeleteNodesOperation.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\Commands\EditCommands.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Shared\Commands\ArrangeCommands.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Services\CommandService.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Plugins\Drawing\DrawingPlugin.cs (tool-registration pattern)
- (new) ObjectSelectionTool for EditContextType.General + its registration in the Sprite or Drawing plugin
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\TopBarView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\ActionsBarView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\SpriteActionsView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\TopToolUiContainer.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\MainView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\ToolBar\ToolBarView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\UI\AdditionalTopBarView.cs
- c:\Projects\Pix2d\Sources\Core\Pix2d.Core\Assets\strings.json
- c:\Projects\Pix2d\docs\ROADMAP.md
- c:\Projects\Pix2d\docs\multiple-artboards-and-project-tabs.md
- c:\Projects\Pix2d\CLAUDE.md

**Verification**
1. Build desktop head: dotnet build Sources/Heads/Pix2d.Desktop/Pix2d.Desktop.csproj.
2. Run app and verify transitions: label double-click enters General; sprite-content double-click returns to Sprite for clicked artboard; Ctrl+F12 both ways works without exceptions (previously KeyNotFoundException in ToolService).
3. Verify General multi-select/multi-drag: Shift selection add/remove, click-empty clears, drag selected sprites together, single undo restores all moved objects.
4. Verify deletion flow: Delete key and new top-menu delete button both show confirm dialog; on confirm remove objects; Ctrl+Z restores; deleting the active artboard re-targets a survivor (no dangling drawing target).
5. Verify compact layout command: selected sprites repack into dense rows with stable spacing and deterministic order; single undo restores positions.
6. Verify UI switching: General hides color/brush buttons (already) and shows General-specific top/action controls; Sprite restores existing controls.
7. Regression sweep: existing sprite drawing, layer operations, object-edit mode entry (per decision #4), export/menu dialogs, and project tab switching still behave as before; run the ScenarioTests sweep.

**Decisions**
- Compact command algorithm: Option A (dense grid).
- General deletion must be undoable.
- Top menu needs a dedicated new delete button in General (not reusing current Clear button behavior).
- Delivery scope: Desktop-first only.

**Further considerations**
1. Confirm exact spacing/default wrap width constants for Compact in rows to keep layout predictable across zoom/canvas sizes (recommend storing fixed world-space constants first; `EditService.ArtboardGap = 16f` is the existing precedent).
2. Decide whether General delete confirmation should include object count/name preview (recommend count + first name for safety).
3. Decide if Sprite object rename remains in SpriteActionsView only or also appears in new General action bar (recommend keeping it in object-edit view for first increment).
4. **[Blocking, decide before Phase 1] Fate of `ArtboardObjectEditService` vs. General context.** Today its Move/Resize/Crop state machine + `SpriteActionsView` + `ArtboardObjectEditorNode` cover single-sprite object editing inside Sprite context. Options: (a) General context supersedes its Move mode — selection/drag/delete/arrange live in General via `FrameEditorNode`, while Resize/Crop (and Rename per #3) remain reachable from the General action bar for a single selected sprite; (b) keep both flows side by side (risk: two competing object-move UXs and double-committed MoveOperations). Recommend (a). Whatever is chosen, label double-click must stop funneling through `ActivateArtboard`->`RequestEdit`'s forced Sprite switch.
5. `Select(SKPoint)`'s empty-space fallback selects the container node — decide whether General click-empty means "clear selection" (recommended, matches step 2.1) and whether that also exits General or stays (recommend: stays; exit only via content double-click / Esc).
