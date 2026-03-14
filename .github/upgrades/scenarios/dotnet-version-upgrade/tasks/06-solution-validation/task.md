# 06-solution-validation: Run full-solution validation and test updates

Finish the upgrade by validating the full solution, including `Pix2d.Core.Tests`, package deprecation cleanup, and end-to-end restore/build/test checks. This task confirms the grouped work integrates correctly across the entire repository.

Use this task to address any final test SDK or deprecated package adjustments surfaced only after all production projects have moved to `.NET 10`.

**Done when**: The full solution restores and builds, tests pass or any remaining failures are documented, and the repository is ready for final review.
