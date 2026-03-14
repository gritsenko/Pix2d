# 05-android-head: Upgrade the Android head

Upgrade `Pix2d.Droid` after the shared dependency graph is stable. This task includes resolving the incompatible Android package set and the higher-count API issues reported by the assessment.

The Android head is isolated as its own group because it has the highest risk profile in the solution and should not block progress on the other heads.

**Done when**: The Android project targets the correct `.NET 10` Android TFM, incompatible packages are replaced or upgraded, and the project builds successfully against the upgraded shared libraries.
