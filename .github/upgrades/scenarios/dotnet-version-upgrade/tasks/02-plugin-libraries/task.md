# 02-plugin-libraries: Upgrade plugin libraries

Upgrade the plugin projects that depend on the shared foundation: `Pix2d.Plugins.BaseEffects`, `Pix2d.Plugins.Psd`, `Pix2d.Plugins.Ai`, `Pix2d.Plugins.Drawing`, `Pix2d.Plugins.OpenCv`, `Pix2d.Plugins.PixelText`, `Pix2d.Plugins.PngCompress`, and `Pix2d.Plugins.Simple`. Include any package or behavioral updates surfaced by the assessment.

These projects are low-risk individually, but they form the extension layer consumed by multiple heads, so they should move together after the shared libraries stabilize.

**Done when**: All plugin libraries target `.NET 10`, restore and build against the upgraded foundation libraries, and their package compatibility issues are resolved.
