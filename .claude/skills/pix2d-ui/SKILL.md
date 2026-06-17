---
name: pix2d-ui
description: Conventions for building and styling Pix2d's UI — Avalonia 12 + Avalonia.Markup.Declarative, views written in C# (NO XAML). Use whenever you create or edit a view/panel/popup, do styling or layout (верстка), add buttons/icons/sliders/toggles, wire a view-model State and bindings, pull colors/brushes/fonts/measures from StaticResources, localize UI text, or do SkiaSharp↔Avalonia bitmap interop in the editor UI. Read this BEFORE exploring the UI from scratch.
---

# Pix2d UI: styling & layout conventions

UI is **Avalonia 12 + [Avalonia.Markup.Declarative](https://github.com/AvaloniaUI/Avalonia.Markup.Declarative)** — every view is **C#, there is no XAML**. The editor canvas is **SkiaSharp** (SkiaNodes scene graph), separate from the Avalonia control UI.

Where things live:
- Views: `Sources/Core/Pix2d.Core/UI/**` (shared widgets in `UI/Shared/`)
- Global styles: `Sources/Core/Pix2d.Core/UI/Styles/AppStyles.cs`
- Colors / brushes / fonts / measures / icons: `Sources/Core/Pix2d.Core/Resources/StaticResouces.cs`
- UI text: `Sources/Core/Pix2d.Core/Assets/strings.json`
- Top-level layout & popup wiring: `Sources/Core/Pix2d.Core/UI/MainView.cs`

`ViewBase` / `ViewBase<TState>` / `ComponentBase` / `ViewFactory` / `Style<T>` come from the Avalonia.Markup.Declarative package (not the repo). Global usings (`Sources/Core/Pix2d.Core/GlobalUsings.cs`) already bring in `Avalonia*`, `Avalonia.Media`, `Avalonia.Media.Imaging` (`Bitmap`), `Avalonia.Markup.Declarative`, `Pix2d.Common.Extensions`, and `static Pix2d.UI.LocalizationHelper` (`L(...)`).

## View skeleton (the standard pattern)

```csharp
public partial class FooView(AppState appState) : ViewBase<FooView.State>(new State(appState))
{
    // Per-view styles (optional). Collection-expression of Style<T> blocks.
    protected override StyleGroup? BuildStyles() =>
    [
        new Style<ListBoxItem>(s => s.OfType<ListBoxItem>())
            .Background(StaticResources.Brushes.BrushItemBrush)
            .Width(44).Height(44).CornerRadius(12),
    ];

    // The layout. Return the root control.
    protected override object Build(State state) =>
        new Grid()
            .Rows("Auto,*,Auto")
            .Margin(8, 0)
            .Children(
                new TextBlock().Classes("body11").Text(L("Title").ToUpperInvariant()),
                new SliderEx().Row(1)
                    .Label(L("Size")).Units("px").Minimum(1)
                    .Value(state, x => x.Size, BindingMode.TwoWay),
                ViewFactory.Create<ChildView>().Row(2));   // child views via the DI factory

    // The view-model. Nested, sealed, partial; CommunityToolkit.Mvvm.
    public sealed partial class State : ObservableObject
    {
        private readonly SpriteEditorState _drawingState;
        private bool _isSyncing;

        [ObservableProperty] public partial double Size { get; set; }

        public State(AppState appState)
        {
            _drawingState = appState.SpriteEditorState;
            SyncFromState();
            _drawingState.WatchFor(x => x.CurrentBrushSettings, SyncFromState);
        }

        // Generated hook fired on Size change (CommunityToolkit). Guard against the sync feedback loop.
        partial void OnSizeChanged(double value)
        {
            if (_isSyncing) return;
            /* push to app state */
        }

        private void SyncFromState()
        {
            _isSyncing = true;
            Size = _drawingState.CurrentBrushSettings.Scale;
            _isSyncing = false;
        }
    }
}
```

- Views that need no view-model inherit plain `ViewBase` and override `Build()` (e.g. `UI/Shared/PopupView.cs`, `UI/Shared/SKImageView.cs`).
- View constructors may take services directly — they are built through DI (`ActivatorUtilities`). Use `ViewFactory.Create<T>()` for child views so they get the same treatment.
- `[ObservableProperty] public partial T Prop { get; set; }` (CommunityToolkit source-gen) — never hand-write the backing field. The generated `OnPropChanged`/`OnPropChanging` partials are the place for side effects.

## Bindings

- Static value: `.Text("x")`, `.Width(44)`.
- Bound to State: `.Value(state, x => x.Size, BindingMode.TwoWay)` — `(viewModel, selector, mode)`. `BindingMode` is `Avalonia.Data.BindingMode`.
- One-way is the default for the 2-arg form; pass the mode explicitly for two-way.

## Layout

- `Grid().Rows("Auto,*,64").Cols("*,Auto")` — `Auto` (content), `*` (star/fill), or fixed px. Place children with `.Row(n)`, `.Col(n)`, `.RowSpan/.ColSpan`.
- `StackPanel`, `WrapPanel` (`StaticResources.Templates.WrapPanelTemplate` for `ItemsPanel`), `Border`, `ScrollViewer`, and `Panel` (bare panel = z-stack: list children back-to-front for overlays, e.g. an `Image` then a corner `Button`).
- Alignment/spacing via `.HorizontalAlignment/.VerticalAlignment/.Margin/.Padding`. A `Border` with `CornerRadius` + `ClipToBounds(true)` clips its child to the rounded rect.

## StaticResources — never hardcode colors/sizes

Pull everything from `StaticResources` (`Resources/StaticResouces.cs`):

- **Brushes** — `Brushes.ForegroundBrush` (white 90%), `SecondaryForegroundBrush` (60%), `MutedForegroundBrush` (30%), `IconForegroundBrush` (icons, 70%), `PanelsBackgroundBrush`, `PanelStrokeBrush` (1px white 7% frame), `PopupBackgroundBrush`, `AccentBrush` (orange→amber gradient), `ButtonBackgroundBrush/ButtonHoverBrush`, `CheckerTilesBrush`.
- **Colors** — same tiers as `Color`; convert with `.ToBrush()`. Parse hex with `"#rrggbb".ToColor()`.
- **Fonts** — `Fonts.DefaultTextFontFamily` (Zed Mono — the UI text face), `Fonts.IconFontSegoe` (Segoe MDL2 glyph icons), `Fonts.Pix2dThemeFontFamily`.
- **Measures** — `ButtonCornerRadius` 16, `PanelCornerRadius` 16, `SmallButtonCornerRadius` 10, `SmallButtonSize` 36, `PanelMargin` 12, `ToolItemCornerRadius` 12, plus `Compact*` for Narrow layout.
- **Icons** — `Icons.GridIcon`, `Icons.CropIcon`, `MagicWandIcon`, … are `Geometry`. Render with `PathIcon`.

## Style classes (defined in AppStyles.cs)

Apply with `.Classes("...")`:

- Text: `"caption"` (9px, 60%), `"body11"` (11px, 60%), `"body14"` (14px, 90%), `"body16"` (16px, 90%). Section labels use `body11` + `.ToUpperInvariant()`.
- Buttons: `"small-button"` (36px round-ish, used for popup pin/close), `"app-button"` (44px, toolbar), `"btn"` (36px), `"btn-bright"`.
- `Border.Classes("Panel")` = standard panel chrome (bg + 1px border + 16px radius + clip).
- Glyph text: `"FontIcon"` (Segoe MDL2), `"Pix2dFontIcon"`.
- Add **view-local** styles in `BuildStyles()`; add **global/shared** styles in `AppStyles.cs` (mind ordering — later same-host styles win; Narrow overrides are declared last on purpose).

## Icons

- Geometry icon: `new PathIcon().Width(13).Height(13).Foreground(...).Data(StaticResources.Icons.GridIcon)` (see `UI/ActionsBarView.cs`).
- Glyph icon: `new TextBlock().FontFamily(StaticResources.Fonts.IconFontSegoe).Text("")` (Segoe MDL2 codepoints — close ``, pin ``). Prefer geometry icons (`Icons.*`) over guessing glyph codepoints.
- Toolbar buttons: `AppButton` / `AppToggleButton` (auto 44px, icon tier styling).

## Text & localization

- Avalonia has **no text-transform** — uppercase in code with `.ToUpperInvariant()` (the Figma design is UPPER for captions/headers).
- Wrap user-facing text in `L("...")`. The **key is the English string**; `L` returns the key unchanged when there's no translation, so new English text works immediately. Add translations under the `Strings` map in `Assets/strings.json`.

## Common widgets

- `PopupView` (`UI/Shared/PopupView.cs`) — floating panel with header, pin, close, drag, auto-size. Register in `MainView` and set `.Header(L(...))`, `.IsOpen(vm, x => x.ShowX, BindingMode.TwoWay)`, `.CloseButtonCommand(...)`, `.ShowPinButton(true)`, `.Width(270)`, `.Content(ViewFactory.Create<XView>())`.
- `SliderEx` (`UI/Shared/SliderEx.cs`) — `.Label/.Units/.Minimum/.Maximum/.Value(...)`; adapts to a compact popup editor on narrow widths.
- `SKImageView` (`UI/Shared/SKImageView.cs`) — shows an `SKBitmap` (via `SKBitmapObservable`), optional `.ShowCheckerBackground(true)`. Use for **live-updating** Skia bitmaps.

## SkiaSharp ↔ Avalonia interop (`Common/Extensions/BitmapExtensions.cs`)

- `skBitmap.ToBitmap()` → Avalonia `Bitmap`. **Copies the pixels** (Skia `FromPixelCopy` snapshot), so the source `SKBitmap` can be disposed right after; for a live bitmap, re-call `ToBitmap()` on each change instead.
- `skBitmap.ToBrush()` → `ImageBrush`; `skColor.ToBrush()` → solid brush.
- **Transparency checker:** `StaticResources.Brushes.CheckerTilesBrush` is tuned for **square** tiles (44px items) — it breaks (wide stripes/gaps) in wide/short boxes. For arbitrary aspect ratios, draw a 2×2 checker in Skia via `SKShader.CreateBitmap(pattern, Repeat, Repeat, SKMatrix.CreateScale(cell, cell))` — see `Shared/CommonNodes/DrawingContainerBaseNode.cs` (canvas checker) and `Plugins/Drawing/Brushes/BasePixelBrush.cs` `DrawPreviewBackground` (preview checker).

## Gotchas

- **`WatchFor` binds to the state instance at subscription time** → it goes stale after a project-tab switch. For anything under `AppState.CurrentProject.*` use `WatchForCurrentProject` / `WatchForCurrentProjectViewPort` (`Shared/Abstract/State/StateExtensions.cs`).
- **Sync feedback loops:** guard two-way State↔AppState sync with an `_isSyncing` flag (see skeleton) so a programmatic sync doesn't re-fire the user-edit path.
- **Don't mutate shared singletons for previews/thumbnails.** e.g. brush instances are shared between presets and the live canvas — clone or `Activator.CreateInstance(x.GetType())` a throwaway before rendering a preview, and dispose it.
- **Hot reload** is on for DEBUG desktop: editing a *view* reloads live. Non-view logic (services, brushes, scene nodes) needs an app restart (`dotnet run --project Sources/Heads/Pix2d.Desktop`).
- Build the UI project to typecheck a view: `dotnet build Sources/Core/Pix2d.Core/Pix2d.Core.csproj`.
