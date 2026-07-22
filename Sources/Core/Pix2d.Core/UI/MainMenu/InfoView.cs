using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.UI.MainMenu;

public partial class InfoView : ViewBase<InfoView.State>
{
    public InfoView(
        IMessenger messenger,
        AppState appState,
        IPlatformStuffService platformStuffService,
        ICommandService commandService,
        ICrashReportService crashReportService,
        IUpdateService updateService)
        : base(new State(messenger, appState, platformStuffService, commandService, crashReportService, updateService))
    {
    }

    protected override object Build(State state) =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).Children(
                new StackPanel().HorizontalAlignment(HorizontalAlignment.Center)
                    .MaxWidth(360)
                    .Children(
                        new Image().Source(StaticResources.UltimateImage).Width(128).Height(128)
                            .Margin(new Thickness(0, 0, 0, 16)),
                        new TextBlock()
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .FontSize(32)
                            .Text(state, x => x.AppVersionText),
                        new Grid().Rows("32,32").Cols("*,Auto").Width(256).Margin(new Thickness(0, 16)).Children(
                            new TextBlock().Text(L("Current project")).VerticalAlignment(VerticalAlignment.Center),

                            new StackPanel().Col(1).Orientation(Orientation.Horizontal).Children([
                                new TextBlock().Col(1).Text(state, x => x.CurrentProjectTitle)
                                    .VerticalAlignment(VerticalAlignment.Center),


                                new AppButton()
                                    .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .VerticalAlignment(VerticalAlignment.Center)
                                    .Margin(new Thickness(8, 0, 0, 0))
                                    .Width(24).Height(24).Content("\xE70F")
                                    .Label("")
                                    .Command(state.FileCommands.Rename)
                            ])

                        ),
                        new TextBlock()
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .Margin(new Thickness(0, 0, 0, 8))
                            .TextWrapping(TextWrapping.Wrap)
                            .FontSize(16)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                            .Text(L("To share your art, suggestions or complains, please join us in:")),

                        new StackPanel().Orientation(Orientation.Horizontal)
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .Children(

                                new Button()
                                    .FontSize(14)
                                    .Classes("btn").Classes("btn-bright")
                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                    .Height(40)
                                    .Margin(new Thickness(6, 0, 6, 24))
                                    .OnClick(_ => state.OpenSupportPage())
                                    .Content(
                                        new StackPanel().Orientation(Orientation.Horizontal).Children(
                                            new Path()
                                                .Data(StaticResources.Icons.TelegramIcon)
                                                .Fill(Brushes.White)
                                                .Width(24)
                                                .Height(24)
                                                .Margin(12, 4, 0, 0)
                                                .VerticalAlignment(VerticalAlignment.Center)
                                                .Stretch(Stretch.Uniform),
                                            new TextBlock()
                                                .Text(L("SUPPORT PIX2D"))
                                                .VerticalAlignment(VerticalAlignment.Center)
                                                .Margin(12, 0)
                                        )
                                    )
                            ),

                        new Button()
                            .Classes("btn").Classes("btn-bright")
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .Height(36)
                            .Margin(new Thickness(0, 0, 0, 16))
                            .Command(state.CrashCommands.ShowCrashReport)
                            .Content(L("Show crash report")),

                        BuildUpdatePanel(state)
#if DEBUG
                        ,
                        BuildDebugCrashPanel(state)
#endif
                    )
            ));

    // Update-check UI — only meaningful on self-updating (portable desktop) builds; the whole block
    // is collapsed on Store / Android / WASM via State.SupportsSelfUpdate.
    private static Control BuildUpdatePanel(State state) =>
        new StackPanel()
            .IsVisible(state, x => x.SupportsSelfUpdate)
            .Margin(new Thickness(0, 0, 0, 16))
            .Children(
                new Border()
                    .IsVisible(state, x => x.HasUpdate)
                    .Classes("Panel")
                    .Padding(new Thickness(12))
                    .Margin(new Thickness(0, 0, 0, 8))
                    .Child(new StackPanel().Children(
                        new TextBlock()
                            .Classes("body16")
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .TextWrapping(TextWrapping.Wrap)
                            .Margin(new Thickness(0, 0, 0, 8))
                            .Text(state, x => x.UpdateHeaderText),
                        new Expander()
                            .Header(L("What's new"))
                            .Margin(new Thickness(0, 0, 0, 8))
                            .Content(new ScrollViewer()
                                .MaxHeight(200)
                                .Content(new TextBlock()
                                    .TextWrapping(TextWrapping.Wrap)
                                    .FontFamily(StaticResources.Fonts.TextArticlesFontFamily)
                                    .Text(state, x => x.ReleaseNotesText))),
                        new Button()
                            .Classes("btn").Classes("btn-bright")
                            .HorizontalAlignment(HorizontalAlignment.Center)
                            .Height(36)
                            .OnClick(_ => state.DownloadUpdate())
                            .Content(L("Download update")))),

                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new Button()
                            .Classes("btn")
                            .Height(32)
                            .IsEnabled(state, x => x.CanCheckForUpdates)
                            .OnClick(_ => state.CheckForUpdates())
                            .Content(L("Check for updates")),
                        new TextBlock()
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Margin(new Thickness(8, 0, 0, 0))
                            .Foreground(StaticResources.Brushes.SecondaryForegroundBrush)
                            .Text(state, x => x.CheckStatusText)));

#if DEBUG
    // Debug-only scaffolding to exercise the crash-report capture paths from the Info page.
    private static Control BuildDebugCrashPanel(State state) =>
        new Border()
            .BorderBrush(Brushes.OrangeRed)
            .BorderThickness(new Thickness(1))
            .Padding(new Thickness(8))
            .Margin(new Thickness(0, 0, 0, 16))
            .Child(new StackPanel().Children(
                new TextBlock()
                    .Text("DEBUG · simulate crash")
                    .Foreground(Brushes.OrangeRed)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 0, 0, 8)),
                new StackPanel()
                    .Orientation(Orientation.Horizontal)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Children(
                        new Button().Classes("btn").Margin(new Thickness(4)).Height(32)
                            .Content("UI thread").OnClick(_ => state.SimulateUiThreadCrash()),
                        new Button().Classes("btn").Margin(new Thickness(4)).Height(32)
                            .Content("Background").OnClick(_ => state.SimulateBackgroundCrash()),
                        new Button().Classes("btn").Margin(new Thickness(4)).Height(32)
                            .Content("Native").OnClick(_ => state.SimulateNativeCrash())),
                new TextBlock()
                    .Text("UI thread: shows report now · Background / Native: after relaunch")
                    .FontSize(10)
                    .Foreground(Brushes.OrangeRed)
                    .TextWrapping(TextWrapping.Wrap)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Margin(new Thickness(0, 8, 0, 0))));
#endif

    public sealed partial class State : ObservableObject
    {
        private const int UiCrashDialogRetryCount = 12;

        private readonly AppState _appState;
        private readonly IPlatformStuffService _platformStuffService;
        private readonly ICrashReportService _crashReportService;
        private readonly IUpdateService _updateService;

        private UpdateInfo? _availableUpdate;

        [ObservableProperty]
        public partial string AppVersionText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CurrentProjectTitle { get; set; } = string.Empty;

        public bool SupportsSelfUpdate { get; }

        [ObservableProperty]
        public partial bool HasUpdate { get; set; }

        [ObservableProperty]
        public partial string UpdateHeaderText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ReleaseNotesText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool CanCheckForUpdates { get; set; } = true;

        [ObservableProperty]
        public partial string CheckStatusText { get; set; } = string.Empty;

        public State(
            IMessenger messenger,
            AppState appState,
            IPlatformStuffService platformStuffService,
            ICommandService commandService,
            ICrashReportService crashReportService,
            IUpdateService updateService)
        {
            _appState = appState;
            _platformStuffService = platformStuffService;
            _crashReportService = crashReportService;
            _updateService = updateService;

            FileCommands = commandService.GetCommandList<FileCommands>()!;
            CrashCommands = commandService.GetCommandList<Pix2d.Command.CrashCommands>()!;
            AppVersionText = $"Pix2d v{platformStuffService.GetAppVersion()}";
            SupportsSelfUpdate = platformStuffService.SupportsSelfUpdate;

            _appState.WatchFor(x => x.CurrentProject, UpdateCurrentProjectTitle);
            _appState.WatchFor(x => x.CurrentProject.File, UpdateCurrentProjectTitle);
            _appState.WatchFor(x => x.CurrentProject.Title, UpdateCurrentProjectTitle);

            messenger.Register<ProjectLoadedMessage>(this, _ => UpdateCurrentProjectTitle());
            messenger.Register<ProjectSavedMessage>(this, _ => UpdateCurrentProjectTitle());

            UpdateCurrentProjectTitle();

            if (SupportsSelfUpdate)
                _ = RunUpdateCheckAsync(force: false);
        }

        /// <summary>Manual "Check for updates" — bypasses the once-per-day throttle.</summary>
        public void CheckForUpdates() => _ = RunUpdateCheckAsync(force: true);

        private async Task RunUpdateCheckAsync(bool force)
        {
            CanCheckForUpdates = false;
            if (force)
                CheckStatusText = L("Checking…");

            try
            {
                var update = await _updateService.CheckForUpdateAsync(force);
                if (update != null)
                {
                    _availableUpdate = update;
                    UpdateHeaderText = $"{L("Update available")}: v{update.Version}";
                    ReleaseNotesText = update.ReleaseNotes;
                    HasUpdate = true;
                    CheckStatusText = string.Empty;
                }
                else if (force)
                {
                    // Manual check that came back empty: reassure the user only when we actually asked.
                    CheckStatusText = HasUpdate ? string.Empty : L("You have the latest version");
                }
            }
            finally
            {
                CanCheckForUpdates = true;
            }
        }

        public void DownloadUpdate()
        {
            var url = _availableUpdate?.HtmlUrl;
            if (!string.IsNullOrWhiteSpace(url))
                _platformStuffService.OpenUrlInBrowser(url);
        }

        public FileCommands FileCommands { get; }
        public Pix2d.Command.CrashCommands CrashCommands { get; }

        public void OpenSupportPage()
        {
            Logger.LogEventWithParams("Support Pix2d clicked", null);
            _platformStuffService.OpenUrlInBrowser("https://pix2d.com/donate.html");
        }

        private void UpdateCurrentProjectTitle()
        {
            CurrentProjectTitle = _appState.CurrentProject?.Title ?? L("No project");
        }

#if DEBUG
        /// <summary>
        /// Posts an unhandled exception onto the UI thread. It surfaces through
        /// Dispatcher.UIThread.UnhandledException → ICrashReportService.CaptureFatal; that handler
        /// marks it handled, so the app stays alive. The follow-up dialog open is retried for a few
        /// UI turns because Avalonia doesn't guarantee the next posted callback runs only after the
        /// unhandled-exception pipeline has persisted the report.
        /// </summary>
        public void SimulateUiThreadCrash()
        {
            var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
            dispatcher.Post(
                () => throw new InvalidOperationException("Simulated UI-thread crash (Pix2d debug)"));

            PostShowCrashReportWhenAvailable(dispatcher, UiCrashDialogRetryCount);
        }

        private void PostShowCrashReportWhenAvailable(Avalonia.Threading.Dispatcher dispatcher, int attemptsRemaining)
        {
            dispatcher.Post(
                () =>
                {
                    if (_crashReportService.PendingCrashReport != null || _crashReportService.LoadLatestReport() != null)
                    {
                        CrashCommands.ShowCrashReport.Execute();
                        return;
                    }

                    if (attemptsRemaining > 0)
                        PostShowCrashReportWhenAvailable(dispatcher, attemptsRemaining - 1);
                },
                Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Throws on a non-UI thread → AppDomain.UnhandledException → CaptureFatal, then the process
        /// goes down. Verifies the full crash → persist → next-launch report loop. Relaunch the app
        /// afterwards to see the populated (non-empty) report.
        /// </summary>
        public void SimulateBackgroundCrash()
        {
            var thread = new System.Threading.Thread(
                () => throw new InvalidOperationException("Simulated background-thread crash (Pix2d debug)"))
            {
                IsBackground = true,
                Name = "Pix2dSimulatedCrash",
            };
            thread.Start();
        }

        /// <summary>
        /// Dereferences a null native pointer (SIGSEGV / access violation). Managed exception
        /// handlers cannot observe this — it verifies the OS process-exit-info path that powers the
        /// crash report on Android (ApplicationExitInfo: ReasonCrashNative). Relaunch to inspect.
        /// </summary>
        public void SimulateNativeCrash()
        {
            System.Runtime.InteropServices.Marshal.ReadInt32(IntPtr.Zero);
        }
#endif
    }
}

