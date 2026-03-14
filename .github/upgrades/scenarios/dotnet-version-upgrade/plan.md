# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade `C:\Projects\pix2d\Sources\Pix2d.sln` from .NET 9 to .NET 10.
**Scope**: Large multi-project solution with 18 projects, mixed platform heads, and a small set of required package/API compatibility fixes.

### Selected Strategy
**Hybrid** — Solution segmented into 4 groups with per-group strategies.
**Rationale**: Shared libraries and plugins are tightly coupled but low-risk, while the browser, desktop, Android, and WAP heads have different target frameworks and compatibility profiles.

## Tasks

### 01-foundation-libraries: Upgrade shared foundation libraries

Retarget the shared dependency chain that the rest of the solution builds on: `SkiaNodes`, `Pix2d.Infrastructure`, `Pix2d.Shared`, `Pix2d.Core`, and `Pix2d.UI`. This task covers target framework updates and the source-compatible API adjustments identified in the assessment for shared code.

This task establishes the dependency baseline for every plugin, test project, and application head. Keep the group internally consistent so downstream projects only need to absorb one upgraded shared surface.

**Done when**: All foundation libraries target `.NET 10`, restore successfully, and build cleanly together with any required API fixes applied.

---

### 02-plugin-libraries: Upgrade plugin libraries

Upgrade the plugin projects that depend on the shared foundation: `Pix2d.Plugins.BaseEffects`, `Pix2d.Plugins.Psd`, `Pix2d.Plugins.Ai`, `Pix2d.Plugins.Drawing`, `Pix2d.Plugins.OpenCv`, `Pix2d.Plugins.PixelText`, `Pix2d.Plugins.PngCompress`, and `Pix2d.Plugins.Simple`. Include any package or behavioral updates surfaced by the assessment.

These projects are low-risk individually, but they form the extension layer consumed by multiple heads, so they should move together after the shared libraries stabilize.

**Done when**: All plugin libraries target `.NET 10`, restore and build against the upgraded foundation libraries, and their package compatibility issues are resolved.

---

### 03-client-heads-baseline: Validate the shared baseline for application heads

Confirm that the upgraded foundation and plugin groups provide a stable baseline for the executable projects. This includes validating restore/build health before head-specific fixes begin and capturing any newly exposed issues that only appear after the shared layers move forward.

This is the cross-group checkpoint for the hybrid strategy. It reduces rework before touching the browser, desktop, Android, and packaging projects.

**Done when**: The solution restore/build succeeds for the shared layers, blocking head-project issues are identified, and the upgrade can proceed into application heads with the dependency baseline fixed.

---

### 04-browser-and-desktop-heads: Upgrade browser, desktop, and packaging heads

Upgrade `Pix2d.Browser`, `Pix2d.Desktop`, and `Pix2d.Desktop.Wap` to their .NET 10-compatible target frameworks and resolve head-specific issues, including any packaging adjustments required by the WAP project.

These projects share most of the plugin surface and have lower package risk than Android, so they can move together once the shared baseline is confirmed.

**Done when**: Browser and desktop heads target `.NET 10`-compatible TFMs, the WAP packaging project is updated or explicitly handled, and the desktop/browser startup projects build successfully.

---

### 05-android-head: Upgrade the Android head

Upgrade `Pix2d.Droid` after the shared dependency graph is stable. This task includes resolving the incompatible Android package set and the higher-count API issues reported by the assessment.

The Android head is isolated as its own group because it has the highest risk profile in the solution and should not block progress on the other heads.

**Done when**: The Android project targets the correct `.NET 10` Android TFM, incompatible packages are replaced or upgraded, and the project builds successfully against the upgraded shared libraries.

---

### 06-solution-validation: Run full-solution validation and test updates

Finish the upgrade by validating the full solution, including `Pix2d.Core.Tests`, package deprecation cleanup, and end-to-end restore/build/test checks. This task confirms the grouped work integrates correctly across the entire repository.

Use this task to address any final test SDK or deprecated package adjustments surfaced only after all production projects have moved to `.NET 10`.

**Done when**: The full solution restores and builds, tests pass or any remaining failures are documented, and the repository is ready for final review.
