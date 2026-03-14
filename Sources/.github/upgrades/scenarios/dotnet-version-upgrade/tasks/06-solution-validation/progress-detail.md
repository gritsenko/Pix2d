# Progress Detail: 06-solution-validation

## What changed
- Attempted full-solution restore and build after upgrading projects to .NET 10.

## Validation
- `dotnet restore` on the solution succeeded with warnings.
- Full `dotnet build` failed with 1 error and multiple warnings:
  - The WAP packaging project (`Heads\Pix2d.Desktop.Wap\Pix2d.Desktop.Wap.wapproj`) is a legacy Windows App Packaging project that requires the Desktop Bridge MSBuild targets (`Microsoft.DesktopBridge.props`) which are not present in the current SDK installation. This caused the solution build error.
  - All other projects (foundation, plugins, desktop, browser, droid, tests) built successfully. Browser required `wasm-tools` workload which was installed during this task and then built successfully.

## Warnings of note
- `OpenCvSharp4.WpfExtensions` restored as a .NETFramework package (NU1701) — may be incompatible on some heads.
- Android project produced NU1608 warnings about AndroidX package version mismatches.
- Trimming/ILLink trimming warnings related to code that uses reflection/JsonSerializer; review trimming settings for trimmed publishes.

## Remediation options
1. Install Desktop Bridge / UWP packaging components on the build machine (Visual Studio workload) so the WAP project can be restored and built.
2. Remove or exclude the `*.wapproj` from the solution during CI builds and handle packaging separately.
3. Migrate the WAP project to a supported packaging approach (manual effort) — I can propose steps if you want.

## Next steps
- If you want me to proceed, I can:
  - Install Desktop Bridge targets (if permitted) and re-run the build, or
  - Update AndroidX package versions to resolve NU1608 warnings, or
  - Create PR with the changes and notes for manual review.

