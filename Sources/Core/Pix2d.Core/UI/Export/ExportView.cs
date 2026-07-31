using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
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

    // Master/detail inside the preview pane, mirroring MainMenuView: on a wide screen the artboard list sits
    // beside the preview and both stay visible; on a narrow one the list is the landing view and the preview
    // covers it once an artboard is picked, with a Back button to return.
    public const string PreviewDetailName = "export-preview-detail";
    public const string PreviewListName = "export-preview-list";

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

        new Style<Button>(s => s.Class("export-row"))
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .BorderThickness(0)
            .Padding(6)
            .MinHeight(52)
            .HorizontalAlignment(HorizontalAlignment.Stretch)
            .HorizontalContentAlignment(HorizontalAlignment.Stretch),

        // Wide (default): preview left, artboard list right — the list never has to be dismissed.
        new Style<Grid>(s => s.Name(PreviewDetailName)).Col(0).ColSpan(1),
        new Style<Border>(s => s.Name(PreviewListName)).Col(1).ColSpan(1).Width(240),

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
                .ColSpan(2),

            // Narrow: the two share the cell. The list is declared first so the preview (opaque) covers it.
            new Style<Grid>(s => s.Name(PreviewDetailName)).Col(0).ColSpan(2),
            new Style<Border>(s => s.Name(PreviewListName)).Col(0).ColSpan(2).Width(double.NaN).Margin(0)
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
                                // Preview (fit / zoom via the overlay controls) + playback controls, beside the
                                // artboard list when more than one artboard is being exported.
                                new Grid()
                                    .Name(PreviewName)
                                    .Cols("*,Auto")
                                    .Children(
                                        // Declared first so the preview covers it when they share the cell (Narrow).
                                        BuildPreviewList(state),
                                        new Grid()
                                            .Name(PreviewDetailName)
                                            .Rows("Auto,*,Auto")
                                            .Background(StaticResources.Brushes.MainBackgroundBrush)
                                            .IsVisible(state, x => x.ShowPreviewDetail)
                                            .Children(
                                                BuildBackButton(state),
                                                new Panel().Row(1)
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
                                                BuildPlaybackBar(state).Row(2)
                                            )
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

    /// <summary>The artboards being exported, with the name each output will be written under and its measured
    /// output. Only shown for a batch — a single artboard is fully described by the preview itself.</summary>
    private static Control BuildPreviewList(State state) =>
        new Border()
            .Name(PreviewListName)
            .Margin(8, 0, 0, 0)
            .Padding(4)
            .CornerRadius(StaticResources.Measures.SmallButtonCornerRadius)
            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
            .IsVisible(state, x => x.ShowPreviewList)
            .Child(
                new Grid().Rows("Auto,*").Children(
                    new TextBlock()
                        .Classes("body11")
                        .Margin(6, 4)
                        .Text(state, x => x.ListHeaderText),
                    new ScrollViewer().Row(1)
                        .Content(
                            new ItemsControl()
                                .ItemsSource(state.ListItems)
                                .ItemTemplate(new FuncDataTemplate<ExportListItem>((row, _) =>
                                    row == null ? new Grid() : BuildPreviewListRow(state, row)))
                        )
                )
            );

    private static Control BuildPreviewListRow(State state, ExportListItem row) =>
        new Button()
            .Classes("export-row")
            .Background(row, x => x.RowBackground)
            .OnClick(_ => state.SelectListItem(row))
            .Content(
                new Grid().Cols("Auto,*")
                    .Children(
                        new SKImageView()
                            .Width(40)
                            .Height(40)
                            .ShowCheckerBackground(true)
                            .PixelPerfect(true)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Source(row.Thumbnail),
                        new StackPanel().Col(1)
                            .Margin(8, 0, 0, 0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Children(
                                new TextBlock()
                                    .Classes("body14")
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(row.Name),
                                new TextBlock()
                                    .Classes("body11")
                                    .TextTrimming(TextTrimming.CharacterEllipsis)
                                    .Text(row, x => x.DetailsText)
                            )
                    )
            );

    /// <summary>Narrow-screen return path from a previewed artboard to the list. Hidden on wide, where both
    /// are visible at once — same rule as <c>MainMenuView</c>'s back item.</summary>
    private static Control BuildBackButton(State state) =>
        new Button()
            .Classes("btn")
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Margin(0, 0, 0, 8)
            .IsVisible(state, x => x.ShowBackButton)
            .OnClick(_ => state.BackToList())
            .Content(
                new StackPanel().Orientation(Orientation.Horizontal).Spacing(6)
                    .Children(
                        new TextBlock().Classes("FontIcon").Text("\xEC52"),
                        new TextBlock().Text(L("Back"))
                    )
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

        // Master/detail state. On wide both panes are up; on narrow the list is the landing view and the
        // preview covers it once an artboard is picked (ShowBackButton returns to the list).
        [ObservableProperty]
        public partial bool ShowPreviewList { get; set; }

        [ObservableProperty]
        public partial bool ShowPreviewDetail { get; set; } = true;

        [ObservableProperty]
        public partial bool ShowBackButton { get; set; }

        [ObservableProperty]
        public partial string ListHeaderText { get; set; } = string.Empty;

        // Preview zoom: the SKImageView is sized to (source px × zoom); the ScrollViewer pans when it
        // exceeds the pane. "Fit" recomputes the zoom to fill the pane uniformly on every pane/image change.
        [ObservableProperty]
        public partial double PreviewImageWidth { get; set; }

        [ObservableProperty]
        public partial double PreviewImageHeight { get; set; }

        [ObservableProperty]
        public partial string PreviewZoomText { get; set; } = string.Empty;

        private ExportItem[] _items = [];
        private ExportListItem? _selectedRow;

        /// <summary>Narrow-screen only: the user has drilled into an artboard, so the preview covers the list.</summary>
        private bool _detailRequested;

        private CancellationTokenSource? _measureCts;

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
                {
                    OnDialogOpened();
                }
                else
                {
                    StopPlaybackTimer();
                    // Don't keep rendering artboards for a dialog nobody is looking at.
                    _measureCts?.Cancel();
                }
            });

            _appState.UiState.WatchFor(x => x.PreferredExportFormat, () => SelectExporter(_appState.UiState.PreferredExportFormat));

            // The breakpoint decides whether the list and the preview coexist or take turns.
            _appState.UiState.WatchFor(x => x.VisualState, UpdateMasterDetailVisibility);
        }

        public ViewCommands ViewCommands => _viewCommands;

        public SKBitmapObservable Preview { get; } = new();

        public IReadOnlyList<ExporterInfo> Exporters => _exportService.RegisteredExporters;

        public bool CanShare => _platformStuffService.CanShare;

        public double MaxFrameIndex => Math.Max(0, FramesCount - 1);

        public string FrameCounterText => $"{SelectedFrameIndex + 1}/{FramesCount}";

        public bool ShowFrameSelectHint => IsAnimated && IsSingleFrameExport && !IsBatchExport;

        public ObservableCollection<ExportListItem> ListItems { get; } = [];

        private ExportScope CurrentScope => ScopeIndex == 1 ? ExportScope.AllSprites : ExportScope.SelectedSprites;

        /// <summary>The artboard shown in the preview pane — whichever row is selected in the list.</summary>
        private ExportItem? PreviewItem => _selectedRow?.Item ?? _items.FirstOrDefault();

        /// <summary>The playback bar drives <see cref="_spriteEditor"/>, which only ever points at the artboard
        /// being edited — so hide it while a *different* artboard of a batch is previewed.</summary>
        private bool IsPreviewingActiveArtboard
        {
            get
            {
                var current = _appState.CurrentProject.CurrentEditedNode;
                var item = PreviewItem;
                return current != null && item != null && item.Nodes.Contains(current);
            }
        }

        public void SelectListItem(ExportListItem row)
        {
            SetSelectedRow(row);
            _detailRequested = true;
            UpdateMasterDetailVisibility();
            UpdatePreview();
        }

        public void BackToList()
        {
            _detailRequested = false;
            UpdateMasterDetailVisibility();
        }

        private void SetSelectedRow(ExportListItem? row)
        {
            _selectedRow = row;
            foreach (var item in ListItems)
                item.IsSelected = item == row;

            OnPropertyChanged(nameof(ShowPlaybackBar));
        }

        private void UpdateMasterDetailVisibility()
        {
            var isNarrow = _appState.UiState.VisualState == nameof(VisualStates.Narrow);

            ShowPreviewList = IsBatchExport;
            // Wide keeps both up; narrow shows the preview only once an artboard has been picked.
            ShowPreviewDetail = !IsBatchExport || !isNarrow || _detailRequested;
            ShowBackButton = IsBatchExport && isNarrow && _detailRequested;
        }

        public bool ShowPlaybackBar => IsAnimated && !IsSheetExport && IsPreviewingActiveArtboard;

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
        /// button, the list, the preview and the output summary — so they can never disagree.</summary>
        private void RefreshItems()
        {
            _items = _exportService.GetExportItems(CurrentScope).ToArray();
            IsBatchExport = _items.Length > 1;

            // Preserve the drilled-into artboard across a scope change when it's still in the export.
            var previousName = _selectedRow?.Name;

            ListItems.Clear();
            foreach (var item in _items)
                ListItems.Add(new ExportListItem(item));

            var current = _appState.CurrentProject.CurrentEditedNode;
            SetSelectedRow(
                ListItems.FirstOrDefault(x => x.Name == previousName)
                ?? ListItems.FirstOrDefault(x => current != null && x.Item.Nodes.Contains(current))
                ?? ListItems.FirstOrDefault());

            ListHeaderText = $"{L("Sprites")} ({_items.Length})".ToUpperInvariant();
            UpdateMasterDetailVisibility();
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

            // Reopening always lands on the list (narrow) rather than wherever the last session drilled to.
            _detailRequested = false;
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
            _ = MeasureItemsAsync();
        }

        /// <summary>
        /// Measures every artboard being exported and fills in both the list rows and the Output summary.
        /// Measuring means running the *real* exporter per artboard, so the loop yields to the dispatcher
        /// between artboards: rows appear progressively and the dialog stays responsive instead of freezing on
        /// a big scene, and the summary carries a "…" until the last one lands. A new schedule (scale, exporter
        /// option, scope) cancels the run in flight rather than racing it.
        /// </summary>
        private async Task MeasureItemsAsync()
        {
            // Cancel the run in flight, then dispose it — never `using` this one: the next schedule has to be
            // able to Cancel() it, and Cancel() on a disposed source throws.
            var previous = _measureCts;
            var cts = new CancellationTokenSource();
            _measureCts = cts;
            previous?.Cancel();
            previous?.Dispose();

            var token = cts.Token;

            // Nothing here may throw out of the method: it is fire-and-forget, so an escaping exception would
            // surface later as a context-free unobserved-task fatal.
            try
            {
                var info = SelectedExporterInfo;
                if (info == null || ListItems.Count == 0)
                {
                    OutputInfoText = string.Empty;
                    return;
                }

                var exporter = _configuredExporter ?? info.CreateInstanceFunc();
                var hasSidecar = exporter is SpriteSheetExporter sheet
                                 && !string.Equals(sheet.MetadataFormat, "none", StringComparison.OrdinalIgnoreCase);

                long totalBytes = 0;
                var totalFiles = 0;
                var measured = 0;
                var rows = ListItems.ToArray();

                foreach (var row in rows)
                {
                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        // Thumbnails don't depend on the export scale, so they survive a scale change.
                        if (!row.HasThumbnail)
                            row.SetThumbnail(RenderThumbnail(row.Item));

                        var (w, h, bytes, files) = await MeasureItemAsync(exporter, row.Item);
                        row.SetMetrics(w, h, bytes, files, L("files"));
                        totalBytes += bytes;
                        totalFiles += files;
                    }
                    catch (Exception e)
                    {
                        Logger.Log($"Export measure failed for '{row.Name}': {e.Message}");
                        row.SetUnmeasurable(L("Couldn't measure"));
                    }

                    measured++;
                    if (token.IsCancellationRequested)
                        return;

                    OutputInfoText = BuildOutputInfo(rows, measured, totalFiles, totalBytes, hasSidecar);

                    // Let the dispatcher run a layout/render pass before the next artboard.
                    if (measured < rows.Length)
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
            }
            catch (Exception e)
            {
                Logger.Log("Export measure pass failed: " + e.Message);
            }
        }

        /// <summary>
        /// Exact output of one artboard, straight from the exporter that will produce it: resolution, byte size
        /// and how many files it becomes.
        /// </summary>
        private async Task<(int Width, int Height, long Bytes, int Files)> MeasureItemAsync(IExporter exporter,
            ExportItem item)
        {
            var nodes = item.Nodes.ToArray();

            // PNG sequence: one file per frame — sum them.
            if (exporter is SpritePngSequenceExporter && nodes.FirstOrDefault() is Pix2dSprite seqSprite)
            {
                var count = seqSprite.GetFramesCount();
                var current = seqSprite.CurrentFrameIndex;
                long total = 0;
                int fw = 0, fh = 0;
                try
                {
                    for (var i = 0; i < count; i++)
                    {
                        seqSprite.SetFrameIndex(i);
                        using var bmp = nodes.RenderToBitmap(SKColor.Empty, Scale);
                        fw = bmp.Width;
                        fh = bmp.Height;
                        using var s = bmp.ToPngStream();
                        total += s.Length;
                    }
                }
                finally
                {
                    seqSprite.SetFrameIndex(current);
                }

                return (fw, fh, total, count);
            }

            // Stream exporters (PNG / GIF / SVG / sprite sheet): the produced stream is the exact output.
            if (exporter is IStreamExporter streamExporter)
            {
                await using var stream = await streamExporter.ExportToStreamAsync(nodes, Scale);
                var size = stream.Length;
                var (sw, sh) = TryGetImageSize(stream) ?? GetNodeSize(nodes);
                return (sw, sh, size, 1);
            }

            var (nw, nh) = GetNodeSize(nodes);
            return (nw, nh, 0, 1);
        }

        private string BuildOutputInfo(ExportListItem[] rows, int measured, int totalFiles, long totalBytes,
            bool hasSidecar)
        {
            var suffix = hasSidecar ? " " + L("+ metadata") : string.Empty;
            var pending = measured < rows.Length ? " …" : string.Empty;

            if (rows.Length == 1)
            {
                var row = rows[0];
                if (row.Width <= 0)
                    return string.Empty;

                if (row.Files > 1)
                    return $"{row.Width} × {row.Height} px · {row.Files} {L("files")} · ~{FormatSize(row.Bytes)} {L("total")}{suffix}";

                return row.Bytes > 0
                    ? $"{row.Width} × {row.Height} px · ~{FormatSize(row.Bytes)}{suffix}"
                    : $"{row.Width} × {row.Height} px";
            }

            var sprites = pending.Length > 0
                ? $"{measured}/{rows.Length} {L("sprites")}"
                : $"{rows.Length} {L("sprites")}";

            return $"{sprites} · {totalFiles} {L("files")} · ~{FormatSize(totalBytes)} {L("total")}{suffix}{pending}";
        }

        /// <summary>Small, scale-independent thumbnail for a list row — downscaled at render time so a 2048 px
        /// artboard doesn't cost a full-resolution bitmap per row.</summary>
        private static SKBitmap RenderThumbnail(ExportItem item)
        {
            const int box = 64;
            var nodes = item.Nodes.ToArray();
            var bounds = nodes.GetBounds();
            var longest = Math.Max(bounds.Width, bounds.Height);
            var scale = longest > box ? box / longest : 1;

            return nodes.RenderToBitmap(SKColor.Empty, scale);
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
