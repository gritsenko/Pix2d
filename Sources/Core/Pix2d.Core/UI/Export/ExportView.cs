using System.IO;
using Avalonia.Styling;
using Avalonia.Data;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Export;
using Pix2d.Command;
using Pix2d.Common;
using Pix2d.CommonNodes;
using Pix2d.Infrastructure.Tasks;
using Pix2d.Plugins.ImageFormats.GifFormat.Exporters;
using Pix2d.Plugins.ImageFormats.SvgFormat.Exporters;
using Pix2d.Plugins.PngFormat.Exporters;
using Pix2d.Plugins.Sprite.Editors;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using SkiaNodes.Extensions;
using SkiaSharp;

namespace Pix2d.UI.Export;

public partial class ExportView : ViewBase<ExportView.State>
{
    public const string PreviewName = "export-preview";
    public const string SettingsName = "export-settings";

    public ExportView(IExportService exportService, IPlatformStuffService platformStuffService, AppState appState, ICommandService commandService)
        : base(new State(exportService, platformStuffService, appState, commandService))
    {
    }

    protected override StyleGroup BuildStyles() =>
    [
        new Style<Button>(s => s.Class("anim-btn"))
            .CornerRadius(10)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
            .FontSize(14)
            .Width(40)
            .Height(40)
            .Padding(0),

        new Style<Button>(s => s.Class("zoom-btn"))
            .CornerRadius(8)
            .Height(32)
            .MinWidth(32)
            .Padding(8, 0)
            .Foreground(StaticResources.Brushes.ForegroundBrush)
            .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
            .FontSize(14)
            .Background(Brushes.Transparent),

        new StyleGroup(_ => VisualStates.Wide())
        {
            new Style<Grid>(s => s.Name(ExportView.PreviewName))
                .Col(0).ColSpan(1)
                .Row(0).RowSpan(2),
            new Style<ScrollViewer>(s => s.Name(ExportView.SettingsName))
                .Col(1).ColSpan(1)
                .Row(0).RowSpan(2)
                .Margin(16, 0)
        },

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<Grid>(s => s.Name(ExportView.PreviewName))
                .Col(0).ColSpan(2)
                .Row(0).RowSpan(1),
            new Style<ScrollViewer>(s => s.Name(ExportView.SettingsName))
                .Col(0).ColSpan(2)
                .Row(1).RowSpan(1)
                .Margin(0, 16),
            new Style<ExportProWarningView>()
                .ColSpan(2)
        }
    ];

    protected override object Build(State state)
    {
        ScrollViewer previewScroll = null!;

        var root =
        new Grid().Rows("auto,*").Cols("*,auto")
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Children(
                new TextBlock().FontSize(24).VerticalAlignment(VerticalAlignment.Center).Margin(16, 0)
                    .Text(L("Export artwork")),
                new Button().Col(1).Content("X").Height(40).Width(40).Command(state.ViewCommands.HideExportDialogCommand),
                new Border().Row(1).ColSpan(2)
                    .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                    .Padding(16)
                    .Child(
                        new Grid()
                            .Rows("*,*,Auto")
                            .Cols("*,*")
                            .MinWidth(200)
                            .MinHeight(200)
                            .Children(
                                // Preview (fit / zoom via the overlay controls) + playback controls
                                new Grid()
                                    .Name(PreviewName)
                                    .Rows("*,auto")
                                    .Children(
                                        new Panel()
                                            .Background(StaticResources.Brushes.MainBackgroundBrush)
                                            .Children(
                                                new ScrollViewer()
                                                    .Ref(out previewScroll)
                                                    .HorizontalScrollBarVisibility(ScrollBarVisibility.Auto)
                                                    .VerticalScrollBarVisibility(ScrollBarVisibility.Auto)
                                                    .Content(
                                                        new SKImageView()
                                                            .ShowCheckerBackground(true)
                                                            .PixelPerfect(true)
                                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                                            .VerticalAlignment(VerticalAlignment.Center)
                                                            .Width(state, x => x.PreviewImageWidth)
                                                            .Height(state, x => x.PreviewImageHeight)
                                                            .Source(state.Preview)
                                                    ),
                                                BuildPreviewZoomControls(state)
                                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                                    .VerticalAlignment(VerticalAlignment.Top)
                                                    .Margin(8)
                                            ),
                                        BuildPlaybackBar(state).Row(1)
                                    ),
                                // Settings
                                new ScrollViewer()
                                    .Name(SettingsName)
                                    .Content(
                                        new StackPanel()
                                            .Spacing(8)
                                            .Children(
                                                BuildScopeSelector(state),
                                                new TextBlock().Text(L("Export type")),
                                                new ComboBox()
                                                    .ItemTemplate<ExporterInfo>(item =>
                                                        new TextBlock().Text(item?.Name ?? string.Empty))
                                                    .ItemsSource(state.Exporters)
                                                    .SelectedItem(state, x => x.SelectedExporterInfo, BindingMode.TwoWay),
                                                new ContentControl()
                                                    .Content(state, x => x.ExporterSettingsContent!),
                                                new SliderEx()
                                                    .Label(L("Image scale"))
                                                    .Units("x")
                                                    .Minimum(1)
                                                    .Maximum(20)
                                                    .Value(state, x => x.Scale, BindingMode.TwoWay),
                                                BuildOutputInfo(state)
                                            )
                                    ),
                                // Action buttons — own row at the bottom so they never overlap the settings.
                                new StackPanel().ColSpan(2).Row(2)
                                    .Orientation(Orientation.Horizontal)
                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(0, 12, 0, 0)
                                    .Children(
                                        new Button()
                                            .Classes("btn")
                                            .Content(L("Save"))
                                            .Width(110)
                                            .Margin(0, 0, 20, 0)
                                            .Foreground(Brushes.White)
                                            .Background(StaticResources.Brushes.AccentButtonBrush)
                                            .OnClick(_ => state.Export()),
                                        new Button()
                                            .Classes("btn")
                                            .Content(L("Cancel"))
                                            .Width(110)
                                            .Command(state.ViewCommands.HideExportDialogCommand),
                                        new Button()
                                            .Classes("btn")
                                            .HorizontalAlignment(HorizontalAlignment.Center)
                                            .Width(110)
                                            .Margin(0, 0, 20, 0)
                                            .IsVisible(state.CanShare)
                                            .Content(new StackPanel().Orientation(Orientation.Horizontal).Children(
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                                    .Margin(new Thickness(0, 0, 8, 0))
                                                    .Text("\xE72D"),
                                                new TextBlock().Text(L("Share")))
                                            )
                                            .OnClick(_ => state.Share())
                                    )
                            )
                    ));

        // Feed the preview pane size into the fit/zoom math (fit = fill this pane uniformly).
        previewScroll.SizeChanged += (_, e) => state.OnPreviewPaneSizeChanged(e.NewSize.Width, e.NewSize.Height);

        return root;
    }

    /// <summary>Which artboards the export covers. Hidden for a single-artboard project, where both
    /// options mean the same thing.</summary>
    private static Control BuildScopeSelector(State state) =>
        new StackPanel()
            .Spacing(8)
            .IsVisible(state, x => x.ShowScopeSelector)
            .Children(
                new TextBlock().Text(L("Sprites to export")),
                new ComboBox()
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Items(
                        new ComboBoxItem().Content(state, x => x.SelectedScopeLabel),
                        new ComboBoxItem().Content(state, x => x.AllScopeLabel)
                    )
                    .SelectedIndex(state, x => x.ScopeIndex, BindingMode.TwoWay)
            );

    private static Control BuildPreviewZoomControls(State state) =>
        new Border()
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Padding(2)
            .Child(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(2)
                    .Children(
                        new Button().Classes("zoom-btn")
                            .Content("−")
                            .ToolTip_Tip(L("Zoom out"))
                            .OnClick(_ => state.PreviewZoomOut()),
                        new Button().Classes("zoom-btn").MinWidth(52)
                            .Content(state, x => x.PreviewZoomText)
                            .ToolTip_Tip(L("Fit to view"))
                            .OnClick(_ => state.PreviewFit()),
                        new Button().Classes("zoom-btn")
                            .Content("+")
                            .ToolTip_Tip(L("Zoom in"))
                            .OnClick(_ => state.PreviewZoomIn())
                    )
            );

    /// <summary>Play/step/scrub controls for animated sprites — lets the user check frames before export
    /// and pick which frame a single-frame exporter should output. Hidden for static (single-frame) sprites.</summary>
    private static Control BuildPlaybackBar(State state) =>
        new Border()
            .Margin(0, 8, 0, 0)
            .Padding(8, 6)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .IsVisible(state, x => x.ShowPlaybackBar)
            .Child(
                new StackPanel().Spacing(4).Children(
                    new Grid().Cols("auto,*,auto")
                        .Children(
                            new StackPanel().Orientation(Orientation.Horizontal).Spacing(2)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .Children(
                                    new Button()
                                        .Classes("anim-btn")
                                        .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                        .Content("\xe92e")
                                        .ToolTip_Tip(L("Stop"))
                                        .OnClick(_ => state.StopPlayback()),
                                    new Button()
                                        .Classes("anim-btn")
                                        .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                        .Content("\xe92f")
                                        .ToolTip_Tip(L("Previous frame"))
                                        .OnClick(_ => state.PrevFrame()),
                                    new Button()
                                        .Classes("anim-btn")
                                        .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                        .Content(state, x => x.PlayIcon)
                                        .ToolTip_Tip(L("Play / pause"))
                                        .OnClick(_ => state.TogglePlay()),
                                    new Button()
                                        .Classes("anim-btn")
                                        .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                        .Content("\xe931")
                                        .ToolTip_Tip(L("Next frame"))
                                        .OnClick(_ => state.NextFrame())
                                ),
                            new Slider().Col(1)
                                .Margin(8, 0)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .Minimum(0)
                                .Maximum(state, x => x.MaxFrameIndex)
                                .SmallChange(1)
                                .LargeChange(1)
                                .TickFrequency(1)
                                .IsSnapToTickEnabled(true)
                                .Value(state, x => x.SelectedFrameValue, BindingMode.TwoWay),
                            new TextBlock().Col(2)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .MinWidth(44)
                                .TextAlignment(TextAlignment.Right)
                                .Text(state, x => x.FrameCounterText)
                        ),
                    new TextBlock()
                        .Classes("body11")
                        .IsVisible(state, x => x.ShowFrameSelectHint)
                        .Text(L("The selected frame will be exported"))
                )
            );

    private static Control BuildOutputInfo(State state) =>
        new Border()
            .Margin(0, 12, 0, 0)
            .Padding(10)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.MainBackgroundBrush)
            .Child(
                new StackPanel().Spacing(4).Children(
                    new TextBlock().Classes("body11").Text(L("Output").ToUpperInvariant()),
                    new TextBlock()
                        .TextWrapping(TextWrapping.Wrap)
                        .Foreground(StaticResources.Brushes.ForegroundBrush)
                        .Text(state, x => x.OutputInfoText)
                )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly IExportService _exportService;
        private readonly IPlatformStuffService _platformStuffService;
        private readonly AppState _appState;
        private readonly ViewCommands _viewCommands;
        private IExporter? _configuredExporter;

        private SpriteEditor? _spriteEditor;
        private readonly DispatcherTimer _playTimer;
        private readonly DispatcherTimer _outputInfoTimer;
        private bool _isSyncing;

        [ObservableProperty]
        public partial double Scale { get; set; } = 1;

        [ObservableProperty]
        public partial ExporterInfo? SelectedExporterInfo { get; set; }

        [ObservableProperty]
        public partial Control? ExporterSettingsContent { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(MaxFrameIndex))]
        [NotifyPropertyChangedFor(nameof(FrameCounterText))]
        public partial int FramesCount { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FrameCounterText))]
        public partial int SelectedFrameIndex { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPlaybackBar))]
        public partial bool IsAnimated { get; set; }

        /// <summary>Selected exporter packs every frame into one image — the preview shows the whole sheet,
        /// so the per-frame playback bar is hidden.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowPlaybackBar))]
        public partial bool IsSheetExport { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlayIcon))]
        public partial bool IsPlaying { get; set; }

        /// <summary>True for exporters that output a single image of the current frame (PNG/JPG/SVG) —
        /// then the scrubbed frame is what gets written; false for GIF/sheet/sequence which use every frame.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowFrameSelectHint))]
        public partial bool IsSingleFrameExport { get; set; } = true;

        [ObservableProperty]
        public partial string OutputInfoText { get; set; } = string.Empty;

        // Export scope: 0 = the selected artboards, 1 = every artboard in the scene. Only offered when the
        // scene actually holds more than one artboard.
        [ObservableProperty]
        public partial int ScopeIndex { get; set; }

        [ObservableProperty]
        public partial bool ShowScopeSelector { get; set; }

        [ObservableProperty]
        public partial string SelectedScopeLabel { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string AllScopeLabel { get; set; } = string.Empty;

        /// <summary>More than one artboard is being exported, so there is no single output: the destination is
        /// a folder, the preview shows one representative artboard, and the frame scrub applies only to it.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowFrameSelectHint))]
        public partial bool IsBatchExport { get; set; }

        // Preview zoom: the SKImageView is sized to (source px × zoom); the ScrollViewer pans when it
        // exceeds the pane. "Fit" recomputes the zoom to fill the pane uniformly on every pane/image change.
        [ObservableProperty]
        public partial double PreviewImageWidth { get; set; }

        [ObservableProperty]
        public partial double PreviewImageHeight { get; set; }

        [ObservableProperty]
        public partial string PreviewZoomText { get; set; } = string.Empty;

        private ExportItem[] _items = [];

        private int _srcWidth;
        private int _srcHeight;
        private double _paneWidth;
        private double _paneHeight;
        private bool _fitPreview = true;
        private double _manualZoom = 1;

        public State(IExportService exportService, IPlatformStuffService platformStuffService, AppState appState, ICommandService commandService)
        {
            _exportService = exportService;
            _platformStuffService = platformStuffService;
            _appState = appState;
            _viewCommands = commandService.GetCommandList<ViewCommands>()!;

            _playTimer = new DispatcherTimer();
            _playTimer.Tick += OnPlayTick;
            _outputInfoTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _outputInfoTimer.Tick += OnOutputInfoTick;

            if (Exporters.Count > 0)
                SelectedExporterInfo = Exporters.First();

            _appState.UiState.WatchFor(x => x.ShowExportDialog, () =>
            {
                if (_appState.UiState.ShowExportDialog)
                    OnDialogOpened();
                else
                    StopPlaybackTimer();
            });

            _appState.UiState.WatchFor(x => x.PreferredExportFormat, () => SelectExporter(_appState.UiState.PreferredExportFormat));
        }

        public ViewCommands ViewCommands => _viewCommands;

        public SKBitmapObservable Preview { get; } = new();

        public IReadOnlyList<ExporterInfo> Exporters => _exportService.RegisteredExporters;

        public bool CanShare => _platformStuffService.CanShare;

        public double MaxFrameIndex => Math.Max(0, FramesCount - 1);

        public string FrameCounterText => $"{SelectedFrameIndex + 1}/{FramesCount}";

        public bool ShowFrameSelectHint => IsAnimated && IsSingleFrameExport && !IsBatchExport;

        private ExportScope CurrentScope => ScopeIndex == 1 ? ExportScope.AllSprites : ExportScope.SelectedSprites;

        /// <summary>The artboard shown in the preview pane: the one being edited when it's part of the export,
        /// otherwise the first — a batch has no single output to preview.</summary>
        private ExportItem? PreviewItem
        {
            get
            {
                var current = _appState.CurrentProject.CurrentEditedNode;
                return _items.FirstOrDefault(x => current != null && x.Nodes.Contains(current))
                       ?? _items.FirstOrDefault();
            }
        }

        public bool ShowPlaybackBar => IsAnimated && !IsSheetExport;

        public object PlayIcon => IsPlaying ? "\xe92c" : "\xe92d";

        /// <summary>Bridges the double-typed <see cref="Slider"/> to the integer frame index.</summary>
        public double SelectedFrameValue
        {
            get => SelectedFrameIndex;
            set => SelectedFrameIndex = (int)Math.Round(value);
        }

        partial void OnScaleChanged(double value)
        {
            UpdatePreview();
            ScheduleOutputInfoUpdate();
        }

        partial void OnSelectedExporterInfoChanged(ExporterInfo? value)
        {
            UpdateSettingsControl(value);
            IsSingleFrameExport = IsSingleFrameExporter(value);
            IsSheetExport = value?.ExporterType == typeof(SpriteSheetExporter);
            UpdatePreview();
            ScheduleOutputInfoUpdate();
        }

        partial void OnSelectedFrameIndexChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedFrameValue));
            if (_isSyncing)
                return;

            _spriteEditor?.SetFrameIndex(value);
            UpdatePreview();

            // For single-frame exporters the output (and its size) depends on the chosen frame.
            if (IsSingleFrameExport)
                ScheduleOutputInfoUpdate();
        }

        partial void OnScopeIndexChanged(int value)
        {
            RefreshItems();
            UpdatePreview();
            ScheduleOutputInfoUpdate();
        }

        /// <summary>Re-resolves what will be exported. Cheap, and the single source of truth for the Export
        /// button, the preview and the output summary — so they can never disagree.</summary>
        private void RefreshItems()
        {
            _items = _exportService.GetExportItems(CurrentScope).ToArray();
            IsBatchExport = _items.Length > 1;
        }

        private void RefreshScope()
        {
            var artboardsCount = _exportService.GetArtboardsCount();
            var selectedCount = _exportService.GetExportItems(ExportScope.SelectedSprites).Count;

            // A single-artboard project has nothing to choose between.
            ShowScopeSelector = artboardsCount > 1;
            SelectedScopeLabel = $"{L("Selected sprites")} ({selectedCount})";
            AllScopeLabel = $"{L("All sprites")} ({artboardsCount})";

            if (!ShowScopeSelector)
                ScopeIndex = 0;

            RefreshItems();
        }

        private void OnDialogOpened()
        {
            _spriteEditor = _appState.CurrentProject.CurrentNodeEditor as SpriteEditor;
            RefreshScope();

            // Take over playback so the editor's own timer doesn't fight the preview timer.
            if (_spriteEditor is { IsPlaying: true })
                _spriteEditor.TogglePlay();

            _playTimer.Stop();
            IsPlaying = false;

            FramesCount = _spriteEditor?.FramesCount ?? 0;
            IsAnimated = FramesCount > 1;

            _isSyncing = true;
            SelectedFrameIndex = _spriteEditor?.CurrentFrameIndex ?? 0;
            _isSyncing = false;

            UpdatePreview();
            ScheduleOutputInfoUpdate();
        }

        #region playback

        public void TogglePlay()
        {
            if (!IsAnimated)
                return;

            if (IsPlaying)
            {
                StopPlaybackTimer();
            }
            else
            {
                var fps = _spriteEditor is { FrameRate: > 0 } ? _spriteEditor.FrameRate : 12;
                _playTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
                _playTimer.Start();
                IsPlaying = true;
            }
        }

        /// <summary>Stop button — halt playback and rewind to the first frame (mirrors the editor's Stop).</summary>
        public void StopPlayback()
        {
            StopPlaybackTimer();
            if (FramesCount > 0)
                SelectedFrameIndex = 0;
        }

        /// <summary>Halt the preview timer without touching the current frame (used when the dialog closes).</summary>
        private void StopPlaybackTimer()
        {
            _playTimer.Stop();
            IsPlaying = false;
        }

        public void PrevFrame()
        {
            StopPlaybackTimer();
            if (FramesCount > 0)
                SelectedFrameIndex = (SelectedFrameIndex - 1 + FramesCount) % FramesCount;
        }

        public void NextFrame()
        {
            StopPlaybackTimer();
            if (FramesCount > 0)
                SelectedFrameIndex = (SelectedFrameIndex + 1) % FramesCount;
        }

        private void OnPlayTick(object? sender, EventArgs e)
        {
            if (FramesCount <= 0)
            {
                _playTimer.Stop();
                IsPlaying = false;
                return;
            }

            SelectedFrameIndex = (SelectedFrameIndex + 1) % FramesCount;
        }

        #endregion

        public async void Export()
        {
            if (SelectedExporterInfo == null)
                return;

            try
            {
                _playTimer.Stop();
                IsPlaying = false;

                RefreshItems();

                using var uiBlocker = new UiBlocker("Exporting...");
                Logger.LogEventWithParams("Exporting image", new Dictionary<string, string?>
                {
                    { "Exporter", SelectedExporterInfo.Name },
                    { "Scope", CurrentScope.ToString() },
                    { "Sprites", _items.Length.ToString() }
                });

                // The service picks the destination from the item count (one file dialog vs one folder) and
                // names every output after its artboard.
                await _exportService.ExportItemsAsync(_items, Scale,
                    _configuredExporter ?? SelectedExporterInfo.CreateInstanceFunc());

                _viewCommands.HideExportDialogCommand.Execute();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex);
            }
        }

        public void Share()
        {
            var exporter = SelectedExporterInfo ?? Exporters.FirstOrDefault();
            if (exporter == null)
            {
                Logger.Log("Could not find suitable exporter.");
                return;
            }

            if (exporter.CreateInstanceFunc() is not IStreamExporter instance)
            {
                Logger.Log("This exporter can not be used to share");
                return;
            }

            _platformStuffService.Share(instance, Scale);
            _viewCommands.HideExportDialogCommand.Execute();
        }

        private void UpdateSettingsControl(ExporterInfo? exporterInfo)
        {
            if (exporterInfo == null)
            {
                _configuredExporter = null;
                ExporterSettingsContent = null;
                return;
            }

            _configuredExporter = exporterInfo.CreateInstanceFunc();

            if (exporterInfo.ExporterType == typeof(SpriteSheetExporter))
            {
                var settingsView = ViewFactory.Create<SpriteSheetExportSettingsView>();
                settingsView.Exporter = (SpriteSheetExporter)_configuredExporter;
                settingsView.SettingsChanged += OnExporterSettingsChanged;
                ExporterSettingsContent = settingsView;
            }
            else if (exporterInfo.ExporterType == typeof(SpritePngSequenceExporter))
            {
                var settingsView = ViewFactory.Create<SpritePngSequenceExporterSettingsView>();
                settingsView.Exporter = (SpritePngSequenceExporter)_configuredExporter;
                settingsView.SettingsChanged += OnExporterSettingsChanged;
                ExporterSettingsContent = settingsView;
            }
            else
            {
                _configuredExporter = null;
                ExporterSettingsContent = null;
            }
        }

        private void OnExporterSettingsChanged()
        {
            UpdatePreview();
            ScheduleOutputInfoUpdate();
        }

        private async void UpdatePreview()
        {
            if (_appState.CurrentProject.CurrentNodeEditor is not SpriteEditor spriteEditor)
                return;

            var nodes = PreviewItem?.Nodes.ToArray() ?? [];
            if (nodes.Length == 0)
                return;

            // Sprite sheet: preview the actual packed sheet (all frames laid out), not just the active frame.
            if (_configuredExporter is SpriteSheetExporter sheetExporter)
            {
                try
                {
                    await using var stream = await sheetExporter.ExportToStreamAsync(nodes, Scale);
                    using var decoded = SKBitmap.Decode(stream);
                    if (decoded is { Width: > 0, Height: > 0 })
                    {
                        // SKBitmap.Decode yields a platform-native color type (BGRA on Windows); normalise to
                        // the app's Rgba8888 so ToBitmap()'s fixed Rgba interpretation shows correct colors.
                        SetPreviewBitmap(decoded.Copy(SkiaNodes.SKApp.ColorType));
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Sprite sheet preview failed: " + e.Message);
                }
            }

            var background = spriteEditor.CurrentSprite.UseBackgroundColor
                ? spriteEditor.CurrentSprite.BackgroundColor
                : SKColor.Empty;

            // RenderToBitmap throws when the export target (canvas × scale) is too large to allocate —
            // this method is `async void`, so an unhandled throw here becomes a fatal unobserved
            // exception rather than a failed preview. Catch it so an oversized preview just doesn't
            // update (the user can still lower the scale / export) instead of taking the app down.
            try
            {
                SetPreviewBitmap(nodes.RenderToBitmap(background, Scale));
            }
            catch (Exception e)
            {
                Logger.Log("Export preview render failed: " + e.Message);
            }
        }

        private void SetPreviewBitmap(SKBitmap bitmap)
        {
            _srcWidth = bitmap.Width;
            _srcHeight = bitmap.Height;
            Preview.SetBitmap(bitmap);
            RecomputePreviewLayout();
        }

        #region preview zoom

        public void OnPreviewPaneSizeChanged(double width, double height)
        {
            _paneWidth = width;
            _paneHeight = height;
            RecomputePreviewLayout();
        }

        public void PreviewFit()
        {
            _fitPreview = true;
            RecomputePreviewLayout();
        }

        public void PreviewZoomIn()
        {
            _fitPreview = false;
            _manualZoom = Math.Min(64, GetCurrentZoom() * 1.5);
            RecomputePreviewLayout();
        }

        public void PreviewZoomOut()
        {
            _fitPreview = false;
            _manualZoom = Math.Max(0.05, GetCurrentZoom() / 1.5);
            RecomputePreviewLayout();
        }

        private double GetCurrentZoom() => _fitPreview ? ComputeFitZoom() : _manualZoom;

        private double ComputeFitZoom()
        {
            if (_srcWidth <= 0 || _srcHeight <= 0 || _paneWidth <= 0 || _paneHeight <= 0)
                return 1;

            return Math.Min(_paneWidth / _srcWidth, _paneHeight / _srcHeight);
        }

        private void RecomputePreviewLayout()
        {
            if (_srcWidth <= 0 || _srcHeight <= 0)
            {
                PreviewImageWidth = 0;
                PreviewImageHeight = 0;
                PreviewZoomText = string.Empty;
                return;
            }

            var zoom = _fitPreview ? ComputeFitZoom() : _manualZoom;
            if (zoom <= 0)
                zoom = 1;

            PreviewImageWidth = _srcWidth * zoom;
            PreviewImageHeight = _srcHeight * zoom;
            PreviewZoomText = _fitPreview ? L("Fit") : $"{Math.Round(zoom * 100)}%";
        }

        #endregion

        #region output info

        private void ScheduleOutputInfoUpdate()
        {
            if (!_appState.UiState.ShowExportDialog)
                return;

            _outputInfoTimer.Stop();
            _outputInfoTimer.Start();
        }

        private void OnOutputInfoTick(object? sender, EventArgs e)
        {
            _outputInfoTimer.Stop();
            _ = ComputeOutputInfoAsync();
        }

        private async Task ComputeOutputInfoAsync()
        {
            try
            {
                var info = SelectedExporterInfo;
                if (info == null)
                {
                    OutputInfoText = string.Empty;
                    return;
                }

                var exporter = _configuredExporter ?? info.CreateInstanceFunc();

                if (_items.Length == 0)
                {
                    OutputInfoText = string.Empty;
                    return;
                }

                if (_items.Length > 1)
                {
                    OutputInfoText = await ComputeBatchOutputInfoAsync(exporter);
                    return;
                }

                var nodes = _items[0].Nodes.ToArray();

                // PNG sequence: one file per frame — report count + summed size.
                if (exporter is SpritePngSequenceExporter && nodes[0] is Pix2dSprite seqSprite)
                {
                    var count = seqSprite.GetFramesCount();
                    var current = seqSprite.CurrentFrameIndex;
                    long total = 0;
                    int w = 0, h = 0;
                    for (var i = 0; i < count; i++)
                    {
                        seqSprite.SetFrameIndex(i);
                        using var bmp = nodes.RenderToBitmap(SKColor.Empty, Scale);
                        w = bmp.Width;
                        h = bmp.Height;
                        using var s = bmp.ToPngStream();
                        total += s.Length;
                    }
                    seqSprite.SetFrameIndex(current);
                    OutputInfoText = $"{w} × {h} px · {count} {L("files")} · ~{FormatSize(total)} {L("total")}";
                    return;
                }

                // Stream exporters (PNG / JPG / GIF / sprite sheet): the produced stream is the exact output.
                if (exporter is IStreamExporter streamExporter)
                {
                    await using var stream = await streamExporter.ExportToStreamAsync(nodes, Scale);
                    var size = stream.Length;
                    var (w, h) = TryGetImageSize(stream) ?? GetNodeSize(nodes);
                    var suffix = exporter is SpriteSheetExporter sheet
                                 && !string.Equals(sheet.MetadataFormat, "none", StringComparison.OrdinalIgnoreCase)
                        ? " " + L("+ metadata")
                        : string.Empty;
                    OutputInfoText = $"{w} × {h} px · ~{FormatSize(size)}{suffix}";
                    return;
                }

                // SVG: text output, resolution matches the source frame.
                if (exporter is SvgImageExporter svg)
                {
                    using var s = svg.Export(nodes, Scale);
                    var (w, h) = GetNodeSize(nodes);
                    OutputInfoText = $"{w} × {h} px · ~{FormatSize(s.Length)}";
                    return;
                }

                var (nw, nh) = GetNodeSize(nodes);
                OutputInfoText = nw > 0 ? $"{nw} × {nh} px" : string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Log("Export output-info failed: " + ex.Message);
                OutputInfoText = string.Empty;
            }
        }

        /// <summary>
        /// Batch summary. Sizes come from the real exporters, which means rendering every artboard — capped,
        /// so a scene with dozens of them doesn't stall the dialog on every option change; past the cap we
        /// report the file count only rather than a made-up size.
        /// </summary>
        private async Task<string> ComputeBatchOutputInfoAsync(IExporter exporter)
        {
            const int maxItemsForSizeEstimate = 12;

            var sprites = $"{_items.Length} {L("sprites")}";

            // Frame sequence: each artboard gets its own subfolder, so the count is frames, not artboards.
            if (exporter is SpritePngSequenceExporter)
            {
                var frames = _items.Sum(item => item.Nodes.OfType<Pix2dSprite>().Sum(s => s.GetFramesCount()));
                return $"{sprites} · {frames} {L("files")} · {L("one folder per sprite")}";
            }

            var hasSidecar = exporter is SpriteSheetExporter sheet
                             && !string.Equals(sheet.MetadataFormat, "none", StringComparison.OrdinalIgnoreCase);
            var suffix = hasSidecar ? " " + L("+ metadata") : string.Empty;

            if (exporter is not IStreamExporter streamExporter || _items.Length > maxItemsForSizeEstimate)
                return $"{sprites} · {_items.Length} {L("files")}{suffix}";

            long total = 0;
            foreach (var item in _items)
            {
                await using var stream = await streamExporter.ExportToStreamAsync(item.Nodes, Scale);
                total += stream.Length;
            }

            return $"{sprites} · {_items.Length} {L("files")} · ~{FormatSize(total)} {L("total")}{suffix}";
        }

        private (int Width, int Height) GetNodeSize(SkiaNodes.SKNode[] nodes)
        {
            var bounds = nodes.GetBounds();
            return ((int)(bounds.Width * Scale), (int)(bounds.Height * Scale));
        }

        private static (int Width, int Height)? TryGetImageSize(Stream stream)
        {
            try
            {
                stream.Position = 0;
                using var codec = SKCodec.Create(stream);
                if (codec != null)
                    return (codec.Info.Width, codec.Info.Height);
            }
            catch
            {
                // Non-image stream — fall back to the node bounds.
            }

            return null;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024):0.##} MB";
        }

        #endregion

        private static bool IsSingleFrameExporter(ExporterInfo? info)
        {
            var t = info?.ExporterType;
            return t != typeof(SpriteSheetExporter)
                   && t != typeof(SpritePngSequenceExporter)
                   && t != typeof(GifImageExporter);
        }

        private void SelectExporter(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                return;

            var selected = Exporters.FirstOrDefault(x =>
                string.Equals(x.Id, format, StringComparison.OrdinalIgnoreCase)
                || x.CreateInstanceFunc().SupportedExtensions.Any(ext => string.Equals(ext, format, StringComparison.OrdinalIgnoreCase)));
            if (selected != null)
            {
                SelectedExporterInfo = selected;
            }
        }
    }
}
