# Progress Detail: 04-browser-and-desktop-heads

## What changed
- Finalized retargeting of Desktop and Browser heads to `.NET 10` TFMs.

## Validation
- `Pix2d.Desktop` built successfully targeting `net10.0`.
- `Pix2d.Browser` failed to build: missing WebAssembly workload (`wasm-tools`).
- `Pix2d.Desktop.Wap.wapproj` is legacy WAP (Windows App Packaging) and requires the Desktop Bridge SDK/targets; it was not converted in this task.

## Notes
- To build `Pix2d.Browser` locally, run `dotnet workload restore` to install WebAssembly workloads, or install the appropriate `wasm-tools` workload manually.
- `Pix2d.Desktop.Wap.wapproj` requires Visual Studio components (Desktop Bridge / UWP packaging) and cannot be automatically upgraded in this flow; consider manual migration or remove WAP support.

## Next steps
- Proceed to `05-android-head` to finish Android-specific package alignment, or address Browser workload installation if you want me to attempt it now.
