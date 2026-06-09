# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Pix2D is a cross-platform animated sprite / pixel art editor (Windows, Linux, macOS, Android, WebAssembly) built on Avalonia 12 and SkiaSharp. The solution lives entirely under [Sources/](Sources/) and is driven by the `.slnx` solution file [Sources/Pix2d.slnx](Sources/Pix2d.slnx).

## Common Commands

Target framework is **.NET 10** (see [Sources/Core/Pix2d.Core/Pix2d.Core.csproj](Sources/Core/Pix2d.Core/Pix2d.Core.csproj)); version is pinned in [Sources/Directory.Build.props](Sources/Directory.Build.props) as `Pix2dVersion`. Ignore the `.NET 7/8` references in `docs/developer_guide.md` and `CONTRIBUTING.md` — they are out of date.

```bash
# Restore / build the whole solution
dotnet restore Sources/Pix2d.slnx
dotnet build   Sources/Pix2d.slnx

# Run desktop (Windows/Linux/macOS)
dotnet run --project Sources/Heads/Pix2d.Desktop

# Publish WASM head
dotnet publish Sources/Heads/Pix2d.Browser -c Release

# Android (requires Android SDK)
dotnet build Sources/Heads/Pix2d.Droid -t:Install
```

There is **no test project** in this solution — `dotnet test` has nothing to run. `TestImages/` is a folder of sample `.pix2d` files used for manual QA, not a test project.

CI lives in `.github/workflows/release-publish.yml` and `dotnet-desktop-winstore.yml`. Multi-platform releases are triggered via `workflow_dispatch` or tag pushes `v*`.

## Architecture

### Head → Core → Shared layering

- **Heads** ([Sources/Heads/](Sources/Heads/)) — per-platform entry points: `Pix2d.Desktop`, `Pix2d.Droid`, `Pix2d.Browser`, `Pix2d.Desktop.Wap` (MS Store bridge). Each defines a `Program`/`Activity` that creates a platform-specific `Pix2dBootstrapperDI` subclass, configures DI, then calls Avalonia's `BuildAvaloniaApp().UseServiceProvider(sp).StartWith...Lifetime(args)`. See [Sources/Heads/Pix2d.Desktop/Program.cs](Sources/Heads/Pix2d.Desktop/Program.cs) and [Sources/Heads/Pix2d.Desktop/DesktopPix2dBootstrapperDI.cs](Sources/Heads/Pix2d.Desktop/DesktopPix2dBootstrapperDI.cs).
- **Core** ([Sources/Core/Pix2d.Core/](Sources/Core/Pix2d.Core/)) — `EditorApp` (the Avalonia `Application`), UI, services, and the base `Pix2dBootstrapperDI`. Everything UI-facing lives here.
- **Shared** ([Sources/Core/Pix2d.Shared/](Sources/Core/Pix2d.Shared/)) — abstractions, state, messages, commands, primitives, node types. Heads and plugins reference Shared; Shared references nothing inside the product.
- **Infrastructure** ([Sources/Core/Pix2d.Infrastructure/](Sources/Core/Pix2d.Infrastructure/)) — cross-cutting plumbing (logger, MVVM base types, result/task helpers, `ServiceProviderPluginAttribute`).
- **SkiaNodes** ([Sources/Core/SkiaNodes/](Sources/Core/SkiaNodes/)) — standalone scene-graph / rendering library built on SkiaSharp. `SKNode`, `ViewPort`, `SceneManager`, `SKApp`, `SKInput`. The editor paints through a `ViewPort` that walks the `SKNode` tree.

### Bootstrapping & DI

The single source of truth for service registration is `Pix2dBootstrapperDI.ConfigureServices` in [Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs](Sources/Core/Pix2d.Core/Pix2dBootstrapperDI.cs). Head bootstrappers override it to add platform-specific services (clipboard, platform-stuff, file system) and `LoadPlugins()` to pick which plugins ship with each head. Desktop currently loads: `Sprite`, `Png/Jpg/Gif/Svg` formats, `BaseEffects`, `Drawing`, `PixelText`, `Ai`.

Plugins implement `IPix2dPlugin` ([Sources/Core/Pix2d.Shared/Abstract/IPix2dPlugin.cs](Sources/Core/Pix2d.Shared/Abstract/IPix2dPlugin.cs)) — a single `Initialize()` method called after DI is built. A plugin class decorated with `[ServiceProviderPlugin(interfaceType, instanceType)]` auto-registers an additional singleton when loaded via `LoadPlugin<T>()`.

Avalonia views are constructed through the DI container via `UseComponentControlFactory(type => ActivatorUtilities.CreateInstance(sp, type))`, so view constructors can take services directly.

### State & messaging

Application state is an observable tree rooted at `AppState` ([Sources/Core/Pix2d.Shared/State/AppState.cs](Sources/Core/Pix2d.Shared/State/AppState.cs)), with nested `ProjectState`, `UiState`, `ToolsState`, `SpriteEditorState`, `SelectionState`, `ViewPortState`. All derive from `StateBase` ([Sources/Core/Pix2d.Shared/Abstract/State/StateBase.cs](Sources/Core/Pix2d.Shared/Abstract/State/StateBase.cs)) which exposes `Get<T>/Set<T>` + per-property and global watchers. `AppState` is registered as a singleton and injected wherever needed — **treat it as the canonical mutable state**, don't duplicate fields in view-models.

Cross-cutting events go through `IMessenger` (MvvmCross `Messenger.Default`) — see [Sources/Core/Pix2d.Shared/Messages/](Sources/Core/Pix2d.Shared/Messages/) for message types (`ProjectLoadedMessage`, `ViewPortInitializedMessage`, `NodesSelectedMessage`, etc.).

### UI

UI is **Avalonia + [Avalonia.Markup.Declarative](https://github.com/AvaloniaUI/Avalonia.Markup.Declarative)**, i.e. views are defined in C# (no XAML). Views typically inherit `ViewBase<TViewModel>` or `ComponentBase`. Styles are defined programmatically via `Style<T>` blocks — see [Sources/Core/Pix2d.Core/UI/MainView.cs](Sources/Core/Pix2d.Core/UI/MainView.cs) for the top-level layout and styling pattern. Shared styles, resources, measures live under [Sources/Core/Pix2d.Core/UI/Styles/](Sources/Core/Pix2d.Core/UI/Styles/) and `StaticResources`.

Hot-reload of views is enabled for `DEBUG` desktop builds (`MetadataUpdateHandler(typeof(HotReloadManager))` in `Program.cs`).

### Plugins & tools

- **Sprite plugin** ([Sources/Core/Pix2d.Core/Plugins/Sprite/](Sources/Core/Pix2d.Core/Plugins/Sprite/)) is the core editor experience; `SpriteEditor` is a singleton consumed by `IEditService`.
- **Drawing plugin** ([Sources/Core/Pix2d.Core/Plugins/Drawing/](Sources/Core/Pix2d.Core/Plugins/Drawing/)) ships the brush/line/rect/oval/triangle/fill/eyedropper/selection tools, pixel selectors, and brushes.
- **Image format plugins** (`PngFormatPlugin`, `JpgFormatPlugin`, `GifFormatPlugin`, `SvgFormatPlugin`) are the import/export backends; add new formats by following the same pattern under `Plugins/ImageFormats/`.
- **External plugins** ([Sources/Plugins/](Sources/Plugins/)): `Pix2d.Plugins.Ai` (ONNX-based object extraction — always loaded on desktop), `Pix2d.Plugins.OpenCv`, `Pix2d.Plugins.Collaborate`, `Pix2d.Plugins.SimplePlugin`, `Pix2d.Plugins.Psd`. Several are present but commented out in desktop `LoadPlugins()` — they can be enabled there.

Tools implement `ITool` and are created lazily via `IToolService` which uses `ActivatorUtilities.CreateInstance` — tool constructors can take services directly.

### Commands & shortcuts

Commands are declared in `CommandsListBase` subclasses (e.g. [Sources/Core/Pix2d.Core/GlobalCommands.cs](Sources/Core/Pix2d.Core/GlobalCommands.cs), and the per-area lists in [Sources/Core/Pix2d.Shared/Commands/](Sources/Core/Pix2d.Shared/Commands/) — `FileCommands`, `EditCommands`, `ViewCommands`, `ArrangeCommands`, `ClipboardCommands`, …). Each command is exposed via `GetCommand(action, nameKey, shortcut, contextType)` — the `contextType` (`EditContextType.Sprite/General/All/...`) gates when it's active. `ICommandService.Initialize()` reflects over these classes and registers global shortcuts.

### Project file format

`.pix2d` is a JSON-serialized `SKNode` tree via `NodeSerializer`. `NodeSerializer.ExtraAssemblies` must list every assembly that contributes custom node types — currently set in the bootstrapper constructor to `Pix2dBootstrapperDI.Assembly` and `Pix2dSprite.Assembly`. If a plugin adds a new node type, register its assembly there.

### App lifetime on each head

`EditorApp.OnFrameworkInitializationCompleted` switches on `ApplicationLifetime`:
- `IClassicDesktopStyleApplicationLifetime` → builds a `MainWindow` hosting `HostView`.
- `IActivityApplicationLifetime` (Android) → sets `MainViewFactory` so the Activity creates `HostView`.
- `ISingleViewApplicationLifetime` (WASM) → sets `MainView = HostView` directly.

The startup document (opened file) is flowed through `Pix2dBootstrapperDI.StartupDocument` and loaded once `ViewPortInitializedMessage` fires — if none, session auto-load runs, and if that also fails a `FileCommands.New` is executed so the editor always has a scene.

## Conventions worth knowing

- Prefer editing services/state/messages over adding new cross-cutting singletons; the DI graph is small and documented inline in `Pix2dBootstrapperDI.ConfigureServices` with `// Depends on:` comments — keep those accurate when wiring new services.
- Platform-specific code belongs in the head project, not behind `#if` in Core. File-system / clipboard / platform-stuff abstractions already exist for this.
- `docs/developer_guide.md` and `CONTRIBUTING.md` reference an old solution name (`Pix2d.sln`), obsolete head names (`Pix2d.Windows`, `Pix2d.Linux`), and older .NET versions. When docs and code disagree, the code is right.
