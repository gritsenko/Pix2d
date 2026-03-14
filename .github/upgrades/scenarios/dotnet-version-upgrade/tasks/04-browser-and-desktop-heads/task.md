# 04-browser-and-desktop-heads: Upgrade browser, desktop, and packaging heads

Upgrade `Pix2d.Browser`, `Pix2d.Desktop`, and `Pix2d.Desktop.Wap` to their .NET 10-compatible target frameworks and resolve head-specific issues, including any packaging adjustments required by the WAP project.

These projects share most of the plugin surface and have lower package risk than Android, so they can move together once the shared baseline is confirmed.

**Done when**: Browser and desktop heads target `.NET 10`-compatible TFMs, the WAP packaging project is updated or explicitly handled, and the desktop/browser startup projects build successfully.
