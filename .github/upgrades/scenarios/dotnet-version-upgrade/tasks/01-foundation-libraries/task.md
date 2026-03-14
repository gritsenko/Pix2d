# 01-foundation-libraries: Upgrade shared foundation libraries

Retarget the shared dependency chain that the rest of the solution builds on: `SkiaNodes`, `Pix2d.Infrastructure`, `Pix2d.Shared`, `Pix2d.Core`, and `Pix2d.UI`. This task covers target framework updates and the source-compatible API adjustments identified in the assessment for shared code.

This task establishes the dependency baseline for every plugin, test project, and application head. Keep the group internally consistent so downstream projects only need to absorb one upgraded shared surface.

**Done when**: All foundation libraries target `.NET 10`, restore successfully, and build cleanly together with any required API fixes applied.
