# Pix2D — Long-Term Roadmap

> **Status:** Draft proposal · Last updated: 2026-07-07 · Baseline version: [v3.8.0](https://github.com/gritsenko/Pix2d/releases/tag/v3.8.0)
>
> This document is written for **both humans and AI coding agents**. Each work item includes context, acceptance criteria hints, and reference links so an agent can pick up a task with minimal extra briefing. Read [`CLAUDE.md`](../CLAUDE.md) and [`CONTRIBUTING.md`](../CONTRIBUTING.md) before starting any task. UI work must follow the project skill at [`.claude/skills/pix2d-ui`](../.claude/skills/pix2d-ui).

---

## 1. Vision & Positioning

**Pix2D is the asset pipeline tool for indie game developers: a modern, free, open-source sprite/pixel-art/animation editor that runs everywhere — Windows, Linux, macOS, Android, and the browser.**

Litmus test for every roadmap item:

> *Does this get an artist's asset into a running game faster?*

### Competitive landscape (for context, not imitation)

| Competitor | Strengths | Our differentiation |
|---|---|---|
| [Aseprite](https://www.aseprite.org/) ([source](https://github.com/aseprite/aseprite)) | Industry standard, huge feature set, scripting | Paid, desktop-only, dated UI. We win on: free/MIT, mobile + web, modern UI |
| [Pixelorama](https://orama-interactive.itch.io/pixelorama) ([source](https://github.com/Orama-Interactive/Pixelorama)) | Free, Godot-based, active community | We win on: native performance (SkiaSharp), mobile UX, stylus/pressure support |
| [Piskel](https://www.piskelapp.com/) | Zero-friction web editor | Effectively unmaintained. We win on: everything, once our web version is first-class |
| [LibreSprite](https://libresprite.github.io/) | Free Aseprite fork | Slow development. We win on: velocity, modern stack |

### Strategy in the AI era (2026)

Two real threats: (a) casual users generating assets instead of drawing them, (b) AI-native competitor editors. Two real advantages: (a) generative models are still bad at strict-palette, grid-consistent, animation-consistent pixel art — manual control remains technically necessary in this genre; (b) a solo maintainer with AI agents now matches the output of a small team, while competitors carry legacy codebases.

**Positioning answer:** Pix2D does not compete with generation — it *hosts* it. "AI drafts, human finishes, agent exports." And Pix2D itself becomes a tool that agents can drive (CLI + MCP), making it a node in AI game-dev pipelines rather than a victim of them. See [Track E](#track-e--ai--agent-integration).

---

## 2. How to read this roadmap (for agents)

- Work is organized into **Horizons** (time-ordered) and **Tracks** (thematic, continuous).
- Priority: `P0` = drop everything, `P1` = next release, `P2` = planned, `P3` = opportunistic.
- Before implementing: check [open issues](https://github.com/gritsenko/Pix2d/issues) for duplicates; link the roadmap item in the PR description.
- Definition of done for any item: code + tests (see [Track Q](#track-q--quality-infrastructure)) + entry in release notes draft + no regression in golden-image tests.
- Tech stack anchors: [Avalonia UI](https://docs.avaloniaui.net/) (currently 12.0.5), [Avalonia.Markup.Declarative](https://github.com/AvaloniaUI/Avalonia.Markup.Declarative), [SkiaSharp](https://github.com/mono/SkiaSharp) (currently 3.119.4), [SixLabors.ImageSharp](https://github.com/SixLabors/ImageSharp), .NET / C#. Web build runs on the Avalonia [WebAssembly backend](https://docs.avaloniaui.net/docs/guides/platforms/how-to-use-web-assembly).

---

## 3. Horizon 1 — Stabilization & Trust (0–3 months)

Goal: after the large v3.8.0 release, make export/selection/drawing bulletproof. Ship as a series of `3.8.x` patch releases.

### H1.1 Bug burn-down `P0`
- [x] Onion-skin layer leaks into export — [#230](https://github.com/gritsenko/Pix2d/issues/230). `Pix2dSprite.Layer.OnDraw` now gates the previous-frame ghost on `vp.Settings.RenderAdorners` (true only for the interactive editor viewport; false for every export/preview/thumbnail/clipboard/project-pack render), matching the existing `EditMode`/artboard-label/`BitmapNode` mip-cache convention. The "onion skin on by default / double-click to disable" note in the issue is stale (pre-3.8.0 animation-controls rework): defaults are now `false` and the toggle is single-click. **Follow-up (Track Q):** golden-image test comparing export with onion skin on/off — blocked on there being a test project.
- [x] Box-selection target movement interrupt — [#225](https://github.com/gritsenko/Pix2d/issues/225) (labeled Hot). Fixed by the transform-tool rewrite (`PixelTransformTool` / `SelectionController`): copy-on-write working-bitmap snapshots prevent tearing mid-transform (a638463), `MoveThumbNode` now raises a cancel event on interrupted drags (9dc8b41), and click-outside commits the transform instead of dropping it (4a5b18d) — a box-selection drag no longer disconnects partway through. **Follow-up (Track Q):** pointer-event regression test once a test project exists.
- [x] Pixel-perfect stroke flicker — [#241](https://github.com/gritsenko/Pix2d/issues/241). Resolved by the pixel-perfect drawing rework (swap-bitmap preview management `03b4282`, incremental preview + optimized bitmap handling `cf92167`, earlier `9e2e39f`); confirmed fixed in current builds.
- [x] Slider mouse-wheel support on desktop — [#242](https://github.com/gritsenko/Pix2d/issues/242) (small UX win, cheap). `SliderEx` now adjusts on `PointerWheelChanged` while hovered (one notch = 1 step, Ctrl = 10, clamped to min/max); the event is marked handled so a parent `ScrollViewer` doesn't scroll instead. Applies to all three internal slider layouts (two-line / one-line / narrow popup).
- [x] Palette slider interruption — [#236](https://github.com/gritsenko/Pix2d/issues/236). Not reproducible on desktop; hardened preventively: `Pix2dColorPicker` now marks its color-square / hue-slider pointer events `Handled` so a parent `ScrollViewer`'s touch `ScrollGestureRecognizer` can't hijack the pointer mid-drag and steal the capture (the capture-steal is what interrupted the drag on touch). Same class of fix as the slider mouse-wheel handling in [#242].
- [ ] Triage remaining [open issues](https://github.com/gritsenko/Pix2d/issues): label with `bug`/`Feature`, `complexity:*`, and platform (`android`, `windows`, `web`).

### H1.2 Project file format hardening `P0`
- [ ] Introduce explicit **format version** field in `.pix2d` files with a migration pipeline (`v(n) → v(n+1)` migrators, never in-place mutation of old files).
- [ ] Backward-compatibility test corpus: commit a set of `.pix2d` files produced by 3.5.x–3.8.x (see `TestSer/` folder) and assert they still open and render identically.
- [ ] Crash-recovery: on unclean shutdown, offer restore from the per-tab autosave introduced in 3.8.0.
- [ ] Document the format in `docs/file-format.md` (agents and third-party tools will need this; it is also the basis for future importers/exporters).

### H1.3 Repository hygiene `P1`
- [ ] Remove `old.cs`, `test.cs`, `test_serialize.cs` from repo root (move logic into proper test projects).
- [ ] Update [pix2d.com](https://pix2d.com) download links — the landing page still links portable/Linux builds of **3.5.3** while the latest release is **3.8.0**. Automate: generate download links from the [latest GitHub release](https://github.com/gritsenko/Pix2d/releases/latest) via API or a CI step that patches the site.
- [x] **Self-update notifier (portable desktop).** On launch, portable desktop builds check the [latest GitHub release](https://github.com/gritsenko/Pix2d/releases/latest) API and surface an "Update available" block on the Info page (version + release notes + Download button → opens the release page). Gated by `IPlatformStuffService.SupportsSelfUpdate` (runtime MSIX-identity check — false for the Store build, Android and WASM). Throttled to once/day via settings, with a manual "Check for updates" button. `IUpdateService` / `UpdateService`. **Next:** optional in-app download of the release asset, then a full auto-updater (elevation, binary swap, restart).
- [ ] Add this `ROADMAP.md` to the repo root and link it from `README.md` and the site.

---

## 4. Horizon 2 — Closing the Game-Dev Loop (3–9 months)

Goal: features that convert "nice pixel editor" into "the tool my game pipeline depends on."

### H2.1 Tilemap / tileset mode `P1` — *flagship feature*
The single biggest gap vs. Aseprite's [tilemap mode](https://www.aseprite.org/docs/tilemap/) and the top reason game devs pick a tool.
- [ ] Tile drawing with **live wrap preview** (canvas rendered repeated 3×3 so seams are visible while drawing).
- [ ] Tileset panel: tiles as first-class reusable objects; painting a map by stamping tiles.
- [ ] Auto-tiling metadata (Wang tiles / blob tilesets — see [reference](https://web.archive.org/web/2024/http://www.cr31.co.uk/stagecast/wang/blob.html)).
- [ ] Export: tileset image + JSON metadata; target compatibility with [Tiled TMX/TSX](https://doc.mapeditor.org/en/stable/reference/tmx-map-format/), [Godot TileSet](https://docs.godotengine.org/en/stable/tutorials/2d/using_tilesets.html), and [Unity Tilemap](https://docs.unity3d.com/Manual/class-Tilemap.html).

### H2.2 Engine-ready export pipeline `P1`
- [ ] **Sprite sheet export v2**: packing (grid + tight), per-tag animation ranges, frame durations, pivot points, 9-slice data.
- [ ] JSON metadata schema, documented in `docs/export-format.md`. Aim for structural compatibility with [Aseprite's `--data` JSON](https://www.aseprite.org/docs/cli/#data) so existing engine importers work out of the box.
- [ ] Direct presets: [Godot SpriteFrames](https://docs.godotengine.org/en/stable/classes/class_spriteframes.html) `.tres`, Unity-friendly sheet + meta, [libGDX TexturePacker atlas](https://libgdx.com/wiki/tools/texture-packer) format.
- [ ] **Headless CLI**: `pix2d export project.pix2d --spritesheet out.png --data out.json` for CI pipelines. This is also the foundation for the MCP server in Track E.

### H2.3 Interop: import competitors' formats `P1`
- [ ] `.ase`/`.aseprite` import — full spec is public: [ase-file-specs.md](https://github.com/aseprite/aseprite/blob/main/docs/ase-file-specs.md). Start read-only (layers, frames, tags, palette); write support is `P3`.
- [ ] Palette import/export: [GIMP `.gpl`](https://developer.gimp.org/core/standards/gpl/), plain `.hex`, PNG strip, and direct fetch from [Lospec Palette List API](https://lospec.com/palettes/api).
- [ ] `.piskel` import (JSON-based, trivial) — free user migration from Piskel.

### H2.4 Animation to competitive parity `P2`
- [ ] Animation **tags/ranges** within one document (idle / run / jump), exported as ranges in metadata.
- [ ] Per-frame duration (ms), not just global FPS.
- [ ] Linked cels (share one image across frames) — memory + workflow win for static parts.
- [ ] Export animated **GIF** (already?) verify quality, plus **APNG** and sprite-sheet-per-tag.

### H2.5 Scripting v1 (pre-plugin) `P2`
Cheaper to ship and maintain than a full plugin API; lets the community close niche gaps themselves.
- [ ] Embed a scripting runtime — candidates: Lua via [MoonSharp](https://github.com/moonsharp-devs/moonsharp) / [Lua-CSharp](https://github.com/nuskey8/Lua-CSharp), or C# scripting via [Roslyn Scripting API](https://github.com/dotnet/roslyn/blob/main/docs/wiki/Scripting-API-Samples.md). Decision doc first (`.plans/`).
- [ ] Minimal stable API surface: document, layers, frames, pixels, palette, selection, export. Version it from day one.
- [ ] Batch-run scripts from CLI (`pix2d run script.lua project.pix2d`) — synergy with H2.2.

---

## 5. Horizon 3 — Platform & Ecosystem (9–18+ months)

### H3.1 Web version as a first-class citizen `P1` (starts earlier, lands here)
The web app ([app.pix2d.com](https://app.pix2d.com)) is the top-of-funnel: zero install friction, and a moat vs. Aseprite.
- [ ] [File System Access API](https://developer.mozilla.org/en-US/docs/Web/API/File_System_API) for real open/save on Chromium; graceful fallback (download/upload) elsewhere.
- [ ] Full [PWA](https://web.dev/learn/pwa/) manifest + service worker offline support ("install Pix2D from the browser").
- [ ] Share a project via link (serialized project in URL fragment or a paste-bin-style backend for small files).
- [ ] Track WASM performance: profile with [dotnet-trace / browser profiling](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance) equivalents; consider `SIMD`/threads flags as the .NET WASM story matures.

### H3.2 macOS signing & notarization `P2`
Current `xattr -dr com.apple.quarantine` workaround loses most Mac users at the door.
- [ ] Enroll in [Apple Developer Program](https://developer.apple.com/programs/) ($99/yr — explicit donation goal, see Track C).
- [ ] CI notarization: [notarytool docs](https://developer.apple.com/documentation/security/notarizing-macos-software-before-distribution), GitHub Actions example: [apple-actions/import-codesign-certs](https://github.com/Apple-Actions/import-codesign-certs).
- [ ] Stretch: Homebrew cask (`brew install --cask pix2d`).

### H3.3 Plugin API v1 + catalog `P3`
Grow it out of Scripting v1 usage data — real extension points will be obvious by then.
- [ ] Stable plugin manifest, sandboxing/permissions decision, versioned API.
- [ ] "Marketplace" = curated `awesome-pix2d-plugins` list in the org + a page on pix2d.com. No infrastructure needed initially.

### H3.4 Sync / collaboration (exploratory) `P3`
Natural extension of the desktop+mobile+web story that no competitor has.
- [ ] Phase 1: project sync between devices via user's own cloud (Dropbox/Drive folders) — zero backend cost.
- [ ] Phase 2 (research): CRDT-based co-editing — see [Automerge](https://automerge.org/) / [Yjs](https://yjs.dev/) for the model; pixel grids are actually a friendly CRDT case (last-writer-wins per cell). Write a research note in `.plans/` before any code.

---

## 6. Track E — AI & Agent Integration (continuous, starts in Horizon 2)

Philosophy: **"AI drafts, human finishes, agent ships."** Never "AI draws instead of you."

### E.1 Generation as a draft layer `P2`
- [ ] "Generate to artboard": call an image provider (user-supplied API key; providers behind an interface — OpenAI Images, Stability, local [ComfyUI](https://github.com/comfyanonymous/ComfyUI) endpoint, etc.) and place the result as a normal layer.
- [ ] **Killer step — pixelization/quantization pass**: snap any generated/imported image to the project's palette and pixel grid. Pure generators do this badly; Pix2D already owns palettes. Algorithms: nearest-in-palette with optional [ordered dithering](https://en.wikipedia.org/wiki/Ordered_dithering) (Bayer), k-means palette extraction for "adopt palette from image".
- [ ] Ship the quantization pass as a standalone tool too (works without any AI provider) — immediately useful for reference images.

### E.2 AI-assisted grunt work `P3`
- [ ] Animation inbetweening between two keyframes (start with classical morphing/optical-flow approaches before ML).
- [ ] Palette-swap / recolor suggestions; tile variation generation.
- [ ] Style-preserving scale (integrate/reference [xBRZ](https://sourceforge.net/projects/xbrz/) or similar for upscale; content-aware downscale research).

### E.3 MCP server + agent-facing CLI `P1 within this track` — *strategic*
Make Pix2D drivable by AI agents so it becomes a node in AI game-dev pipelines.
- [ ] Build on the headless CLI (H2.2). Implement an [MCP](https://modelcontextprotocol.io/) server ([spec](https://spec.modelcontextprotocol.io/), [C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)) exposing tools such as:
  - `open_project(path)` / `get_project_info`
  - `export_spritesheet(project, tag?, format)` / `export_tileset`
  - `apply_palette(project, palette_source)`
  - `quantize_image(input, palette, grid)` (from E.1)
  - `run_script(project, script)` (from H2.5)
- [ ] Publish to MCP registries; document an end-to-end demo: *"agent generates art → quantizes in Pix2D → exports Godot SpriteFrames → game builds in CI."* That demo is also marketing (Track C).
- [ ] Keep `CLAUDE.md` and `.claude/skills/pix2d-ui` current — the repo itself must stay agent-friendly, since agent-assisted velocity is the project's core competitive advantage.

---

## 7. Track Q — Quality Infrastructure (continuous)

- [ ] **Test projects**: unit tests for document model, undo/redo, serialization, exporters. Framework: xUnit/NUnit; move root-level `test*.cs` logic here.
- [ ] **Golden-image rendering tests**: render known scenes with SkiaSharp headless, compare against committed PNGs with a small tolerance (see [Verify](https://github.com/VerifyTests/Verify) or a simple pixel-diff). Run in CI on every PR; this is the safety net for all Horizon 2/3 refactors.
- [ ] CI matrix in GitHub Actions: build Windows/Linux/Android/WASM on every PR; tests gate merge.
- [x] Crash telemetry: Sentry integrated on Android **and desktop** (Windows/Linux/macOS + MS Store WAP bundle) — opt-in, fatal-only. `DesktopSentryCrashTelemetrySink` mirrors the Android sink; DSN injected at build time via the `SentryDsn` MSBuild property (CI secret `SENTRY_DSN`, wired in `release-publish.yml` + `dotnet-desktop-winstore.yml`); consent toggle + auto-shown crash dialog now surface on desktop too. WASM still local-report-only (no reliable Sentry .NET browser-wasm support).
- [x] Usage/conversion analytics: anonymous custom-event tracking to `stats.pix2d.com` on Android, desktop, and WASM via `AppStatLoggerTarget` → `AppStatTrackingClient` (AOT/trim/WASM-safe, batched). Wired into the base bootstrapper's `InitAnalytics`; the endpoint is derived from the same baked-in DSN (`AppStatEndpoint.TryGetTrackUrl`), gated on a DSN being present. Only `Logger.LogEventWithParams` events are forwarded — crashes/diagnostic logs are filtered out by `ILoggerTarget.EventsOnly`. Uses a random anonymous `AppSettings.InstallId`. WASM delivery requires the stats server to allow CORS from the app origin. Events emitted so far: `App launched` (with head `Platform`, fired once analytics is enabled), `Project created` (in `ProjectService.CreateNewProjectAsync`, with `Size` — covers File→New, Ctrl+T new tab, custom-size dialog), `Exporting image` (in `ExportView.Export`, with `Exporter`). **Unreachable-backend resilience**: the client skips flushes entirely while the OS reports no connectivity (`NetworkInterface.GetIsNetworkAvailable`, assumed online on WASM), uses a 15 s HTTP timeout (was the 100 s default — a blackholed host no longer pins a flush), drops a batch on 4xx (poison payload) while requeueing on 5xx/network errors, and after 5 consecutive failed flushes goes dormant for the rest of the process lifetime (queue dropped, `Track()` no-op). Sentry sinks (desktop + Android) set `CacheDirectoryPath`, so a fatal crash captured while the host is down is persisted locally and re-sent on the next launch.
- [x] Unified telemetry consent (strict opt-in): a single `TelemetryConsent` (was crash-only `CrashTelemetryConsent`; legacy value migrated) now gates **both** analytics AND crash reporting. Nothing is sent until the user allows it. A crash-independent first-launch consent dialog (`TelemetryConsentDialogView`) is shown once when consent is `Unset` on a telemetry-supported platform; a pending crash still routes to `CrashReportDialogView` (its toggle collects the same consent) so the two never stack. `ICrashReportService.TelemetryConsentChanged` lets the bootstrapper bring analytics + the Sentry sink up the instant consent flips to Allowed, without a relaunch. Fixed the phantom "empty" crash dialog on desktop first launch: desktop now marks a clean exit in `OnAppClosing`, and the interrupted-launch heuristic no longer manufactures a content-free report when there's no OS exit verdict and no `Fatal.log`. A Settings toggle (`AppSettingsView` → "Anonymous telemetry") lets the user review/withdraw consent any time — flipping it off flushes and unregisters the AppStat target (`Logger.UnregisterLoggerTarget`) so collection stops mid-session, not just next launch. The first-launch consent prompt covers **Android + desktop + WASM** (the prompt has no crash-sink dependency; WASM keeps crash reports manual-only but browser analytics is opt-in-enabled), so no platform silently loses analytics under the opt-in model.
- [ ] Performance budget: startup time, stroke latency, memory per open tab; regression checks on large canvases (the 3.8.0 mipmap caching work is the baseline).

---

## 8. Track C — Community & Sustainability (continuous)

- [ ] **Public roadmap**: mirror this file into [GitHub Projects](https://github.com/gritsenko/Pix2d/projects) + [Milestones](https://github.com/gritsenko/Pix2d/milestones); label starter tasks [`good first issue`](https://github.com/gritsenko/Pix2d/labels).
- [ ] **Localization** via [Weblate](https://weblate.org/) (free hosting for libre projects) or [Crowdin](https://crowdin.com/) (free OSS tier). Issue activity already shows a Chinese-speaking user base; zh-CN, ru, es, pt-BR first.
- [ ] **Release marketing loop**: every release ships with a 30–60s GIF/video of the new feature. Channels: [r/PixelArt](https://www.reddit.com/r/PixelArt/), [r/gamedev](https://www.reddit.com/r/gamedev/), [Lospec](https://lospec.com/), [itch.io devlog](https://itch.io/), the existing [Telegram channel](https://t.me/pix2dApp), Mastodon/Bluesky gamedev tags.
- [ ] **Funding**: [GitHub Sponsors](https://github.com/sponsors) + [Open Collective](https://opencollective.com/) with a transparent "what money buys" page (macOS signing $99/yr, Google Play fee, domain, CI minutes). Later: optional paid convenience tier (e.g., hosted sync from H3.4) — follow the Krita/Pixelorama "free forever, pay for convenience" model.
- [ ] Add `ROADMAP.md`, `docs/file-format.md`, `docs/export-format.md` links to the website; agents and contributors should find canonical docs in ≤2 clicks.

---

## 9. Suggested release train

| Version | Theme | Key items |
|---|---|---|
| 3.8.x | Stabilization | H1.1, H1.2 |
| 3.9 | Export & interop | H2.2 (spritesheet v2 + CLI), H2.3 (.ase import, palettes) |
| 3.10 | Animation parity | H2.4 |
| 4.0 | **Tilemaps** | H2.1 (flagship), H1.2 format v2 |
| 4.1 | Scripting + MCP | H2.5, E.3 |
| 4.2 | Web first-class | H3.1, E.1 quantization |
| 4.x | macOS, plugins, sync | H3.2, H3.3, H3.4 |

*Order within trains is flexible; the invariant is: stability → export pipeline → tilemaps → agents/web.*

---

## 10. Non-goals (explicitly out of scope)

- General-purpose raster editor (competing with Krita/Photoshop) — stay pixel/sprite-focused.
- Building/hosting our own image-generation models — integrate providers, never own inference.
- A binary plugin marketplace with payments/DRM — curated list only.
- Vector tools beyond pixel-perfect shape primitives.

---

*Questions or proposals: open a [Discussion](https://github.com/gritsenko/Pix2d/discussions). PRs against this file are welcome.*
