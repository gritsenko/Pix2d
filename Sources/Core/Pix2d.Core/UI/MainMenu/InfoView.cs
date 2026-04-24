using Pix2d.Command;
using CommunityToolkit.Mvvm.ComponentModel;
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
        ILocalizationService localizationService,
        IPlatformStuffService platformStuffService,
        IUiScaleService uiScaleService,
        ICommandService commandService,
        ISettingsService settingsService)
        : base(new State(messenger, appState, localizationService, platformStuffService, uiScaleService, commandService, settingsService))
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

                    new TextBlock()
                        .HorizontalAlignment(HorizontalAlignment.Left)
                        .Text(L("Choose UI language:"))
                        .FontSize(20)
                        .Margin(0, 0, 0, 10)
                        .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                    new ComboBox()
                        .ItemsSource(state.AvailableLocales)
                        .DisplayMemberBinding(new Binding("FullTitle"))
                        .SelectedItem(state, x => x.SelectedLocale, BindingMode.TwoWay)
                        .Margin(0, 0, 0, 12),

                    new StackPanel().Orientation(Orientation.Horizontal)
                        .Children(
                            new TextBlock().Text(L("UI Scale:")).Margin(0, 16, 0, 10).FontSize(20).VerticalAlignment(VerticalAlignment.Center)
                                .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),

                            new TextBlock()
                                .Text(state, x => x.UiScaleText).Margin(10, 20, 0, 10).FontSize(18).VerticalAlignment(VerticalAlignment.Center)
                        ),

                    new Grid().Rows("32").Cols("*, 100,100").Margin(0, 0, 0, 12).Children(
                        new Slider().Col(0)
                            .Minimum(0.5)
                            .Maximum(2.5)
                            .TickFrequency(0.25)
                            .IsSnapToTickEnabled(true)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Value(state, x => x.UiScale, BindingMode.TwoWay),
                        new Button()
                            .Col(1)
                            .Classes("btn").Classes("btn-bright")
                            .Margin(8, 0, 0, 0)
                            .OnClick(_ => state.ApplyScale())
                            .Content(L("Apply")),
                        new Button()
                            .Col(2)
                            .Classes("btn").Classes("btn-bright")
                            .Margin(8, 0, 0, 0)
                            .OnClick(_ => state.ResetScale())
                            .Content(L("Reset"))
                    ),

                    new TextBlock()
                        .Text(L("Mouse wheel behavior:"))
                        .Margin(0, 16, 0, 10)
                        .FontSize(20)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                    new ComboBox()
                        .ItemsSource(state.AvailableMouseWheelBehaviors)
                        .DisplayMemberBinding(new Binding("Title"))
                        .SelectedItem(state, x => x.SelectedMouseWheelBehavior, BindingMode.TwoWay)
                        .Margin(0, 0, 0, 12),

                    new TextBlock()
                        .Text(L("Two-finger double-tap undo:"))
                        .Margin(0, 8, 0, 8)
                        .FontSize(20)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                    new ToggleSwitch()
                        .IsChecked(state, x => x.IsTwoFingerDoubleTapUndoEnabled, BindingMode.TwoWay)
                        .Margin(0, 0, 0, 12),

                    new TextBlock()
                        .Text(L("Double-tap timeout (ms):"))
                        .Margin(0, 8, 0, 8)
                        .FontSize(20)
                        .VerticalAlignment(VerticalAlignment.Center)
                        .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                    new Grid().Rows("32").Cols("*,Auto").Margin(0, 0, 0, 12).Children(
                        new Slider().Col(0)
                            .Minimum(200)
                            .Maximum(1000)
                            .TickFrequency(50)
                            .IsSnapToTickEnabled(true)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Value(state, x => x.TwoFingerDoubleTapTimeoutMs, BindingMode.TwoWay),
                        new TextBlock().Col(1)
                            .Margin(8, 0, 0, 0)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .Text(state, x => x.TwoFingerDoubleTapTimeoutText)
                    )
                ),
                new StackPanel()
                    .Children()
            ));

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ILocalizationService _localizationService;
        private readonly IPlatformStuffService _platformStuffService;
        private readonly IUiScaleService _uiScaleService;
        private readonly ISettingsService _settingsService;
        private bool _isSyncing;

        [ObservableProperty]
        public partial string AppVersionText { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string CurrentProjectTitle { get; set; } = string.Empty;

        [ObservableProperty]
        public partial LocaleInfo? SelectedLocale { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UiScaleText))]
        public partial double UiScale { get; set; }

        [ObservableProperty]
        public partial MouseWheelBehaviorItem? SelectedMouseWheelBehavior { get; set; }

        [ObservableProperty]
        public partial bool IsTwoFingerDoubleTapUndoEnabled { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TwoFingerDoubleTapTimeoutText))]
        public partial int TwoFingerDoubleTapTimeoutMs { get; set; }

        public State(
            IMessenger messenger,
            AppState appState,
            ILocalizationService localizationService,
            IPlatformStuffService platformStuffService,
            IUiScaleService uiScaleService,
            ICommandService commandService,
            ISettingsService settingsService)
        {
            _appState = appState;
            _localizationService = localizationService;
            _platformStuffService = platformStuffService;
            _uiScaleService = uiScaleService;
            _settingsService = settingsService;

            FileCommands = commandService.GetCommandList<FileCommands>()!;
            AvailableLocales = localizationService.AvailableLocales;
            AppVersionText = $"Pix2d v{platformStuffService.GetAppVersion()}";

            _appState.WatchFor(x => x.CurrentProject, UpdateCurrentProjectTitle);
            _appState.WatchFor(x => x.CurrentProject.File, UpdateCurrentProjectTitle);
            _appState.WatchFor(x => x.CurrentProject.Title, UpdateCurrentProjectTitle);
            _appState.WatchFor(x => x.UiScale, SyncFromAppState);
            _appState.WatchFor(x => x.MouseWheelBehavior, OnMouseWheelBehaviorChangedExternally);
            _appState.WatchFor(x => x.IsTwoFingerDoubleTapUndoEnabled, OnTwoFingerUndoChangedExternally);
            _appState.WatchFor(x => x.TwoFingerDoubleTapTimeoutMs, OnTwoFingerTimeoutChangedExternally);

            messenger.Register<ProjectLoadedMessage>(this, _ => UpdateCurrentProjectTitle());
            messenger.Register<ProjectSavedMessage>(this, _ => UpdateCurrentProjectTitle());

            UpdateCurrentProjectTitle();
            SyncFromAppState();
        }

        public FileCommands FileCommands { get; }

        public IReadOnlyList<LocaleInfo> AvailableLocales { get; }

        public IReadOnlyList<MouseWheelBehaviorItem> AvailableMouseWheelBehaviors { get; } =
        [
            new(Pix2d.Primitives.ViewPort.MouseWheelBehavior.Scroll, "Scroll"),
            new(Pix2d.Primitives.ViewPort.MouseWheelBehavior.Zoom, "Zoom")
        ];

        public string UiScaleText => $"{UiScale:0.##}x";

        public string TwoFingerDoubleTapTimeoutText => $"{TwoFingerDoubleTapTimeoutMs} ms";

        partial void OnSelectedLocaleChanged(LocaleInfo? value)
        {
            if (_isSyncing || value == null || string.Equals(_appState.Locale, value.Code, StringComparison.Ordinal))
                return;

            _localizationService.SetLocale(value.Code);
        }

        partial void OnUiScaleChanged(double value)
        {
            if (_isSyncing)
                return;

            _appState.UiScale = value;
        }

        partial void OnSelectedMouseWheelBehaviorChanged(MouseWheelBehaviorItem? value)
        {
            if (_isSyncing || value == null || _appState.MouseWheelBehavior == value.Behavior)
                return;

            _appState.MouseWheelBehavior = value.Behavior;
            _settingsService.Set(nameof(AppState.MouseWheelBehavior), value.Behavior);
        }

        partial void OnIsTwoFingerDoubleTapUndoEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.IsTwoFingerDoubleTapUndoEnabled = value;
            _settingsService.Set(nameof(AppState.IsTwoFingerDoubleTapUndoEnabled), value);
        }

        partial void OnTwoFingerDoubleTapTimeoutMsChanged(int value)
        {
            if (_isSyncing)
                return;

            _appState.TwoFingerDoubleTapTimeoutMs = value;
            _settingsService.Set(nameof(AppState.TwoFingerDoubleTapTimeoutMs), value);
        }

        public void ApplyScale()
        {
            _uiScaleService.SetUiScale(_appState.UiScale);
        }

        public void ResetScale()
        {
            UiScale = 1.0;
            ApplyScale();
        }

        public void OpenSupportPage()
        {
            _platformStuffService.OpenUrlInBrowser("https://pix2d.com/donate.html");
        }

        private void UpdateCurrentProjectTitle()
        {
            CurrentProjectTitle = _appState.CurrentProject?.Title ?? L("No project");
        }

        private void SyncFromAppState()
        {
            _isSyncing = true;

            UiScale = _appState.UiScale;
            SelectedLocale = AvailableLocales.FirstOrDefault(x => x.Code == (_appState.Locale ?? "en"))
                ?? new LocaleInfo("en", "English", "English");
            SelectedMouseWheelBehavior = AvailableMouseWheelBehaviors.FirstOrDefault(x => x.Behavior == _appState.MouseWheelBehavior)
                ?? AvailableMouseWheelBehaviors[0];
            IsTwoFingerDoubleTapUndoEnabled = _appState.IsTwoFingerDoubleTapUndoEnabled;
            TwoFingerDoubleTapTimeoutMs = _appState.TwoFingerDoubleTapTimeoutMs;

            _isSyncing = false;
        }

        private void OnMouseWheelBehaviorChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.MouseWheelBehavior), _appState.MouseWheelBehavior);
        }

        private void OnTwoFingerUndoChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.IsTwoFingerDoubleTapUndoEnabled), _appState.IsTwoFingerDoubleTapUndoEnabled);
        }

        private void OnTwoFingerTimeoutChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.TwoFingerDoubleTapTimeoutMs), _appState.TwoFingerDoubleTapTimeoutMs);
        }
    }

    public sealed class MouseWheelBehaviorItem(Pix2d.Primitives.ViewPort.MouseWheelBehavior behavior, string title)
    {
        public Pix2d.Primitives.ViewPort.MouseWheelBehavior Behavior { get; } = behavior;

        public string Title { get; } = title;
    }
}

