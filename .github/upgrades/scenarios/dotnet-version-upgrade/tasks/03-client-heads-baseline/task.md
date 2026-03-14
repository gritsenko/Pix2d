# 03-client-heads-baseline: Validate the shared baseline for application heads

Confirm that the upgraded foundation and plugin groups provide a stable baseline for the executable projects. This includes validating restore/build health before head-specific fixes begin and capturing any newly exposed issues that only appear after the shared layers move forward.

This is the cross-group checkpoint for the hybrid strategy. It reduces rework before touching the browser, desktop, Android, and packaging projects.

**Done when**: The solution restore/build succeeds for the shared layers, blocking head-project issues are identified, and the upgrade can proceed into application heads with the dependency baseline fixed.
