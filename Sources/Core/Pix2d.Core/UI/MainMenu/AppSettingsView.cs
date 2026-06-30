using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.UI.Resources;

namespace Pix2d.UI.MainMenu;

public partial class AppSettingsView : ViewBase<AppSettingsView.State>
{
    public AppSettingsView(
        AppState appState,
        ILocalizationService localizationService,
        IUiScaleService uiScaleService,
        ISettingsService settingsService)
        : base(new State(appState, localizationService, uiScaleService, settingsService))
    {
    }

    protected override object Build(State state) =>
        new ScrollViewer().Content(
            new StackPanel().Margin(16).Children(
                new StackPanel().HorizontalAlignment(HorizontalAlignment.Center)
                    .MaxWidth(360)
                    .Children(
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
                        ),

                        new TextBlock()
                            .Text(L("Stylus mode"))
                            .Margin(0, 16, 0, 8)
                            .FontSize(20)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsStylusModeEnabled, BindingMode.TwoWay)
                            .Margin(0, 0, 0, 6),
                        new TextBlock()
                            .Text(L("Blocks accidental single-finger canvas edits while keeping pen input active."))
                            .Margin(0, 0, 0, 12)
                            .TextWrapping(TextWrapping.Wrap)
                            .FontSize(14),

                        new TextBlock()
                            .Text(L("Pan with single finger"))
                            .Margin(0, 8, 0, 8)
                            .FontSize(20)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsSingleFingerPanEnabled, BindingMode.TwoWay)
                            .IsEnabled(state, x => x.IsStylusModeEnabled)
                            .Margin(0, 0, 0, 12),

                        new TextBlock()
                            .Text(L("Auto-open transform editor after selection"))
                            .Margin(0, 8, 0, 8)
                            .FontSize(20)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsAutoOpenTransformEditorAfterSelectionEnabled, BindingMode.TwoWay)
                            .Margin(0, 0, 0, 6),
                        new TextBlock()
                            .Text(L("Automatically switches from selection to transform mode when the selection is finished."))
                            .Margin(0, 0, 0, 12)
                            .TextWrapping(TextWrapping.Wrap)
                            .FontSize(14),

                        new TextBlock()
                            .Text(L("Pen haptic feedback"))
                            .Margin(0, 8, 0, 8)
                            .FontSize(20)
                            .VerticalAlignment(VerticalAlignment.Center)
                            .FontFamily(StaticResources.Fonts.TextArticlesFontFamily),
                        new ToggleSwitch()
                            .IsChecked(state, x => x.IsPenHapticsEnabled, BindingMode.TwoWay)
                            .Margin(0, 0, 0, 6),
                        new TextBlock()
                            .Text(L("Tactile \"pen on paper\" vibration while drawing. Requires a haptic pen such as the Surface Slim Pen 2 on Windows 11."))
                            .Margin(0, 0, 0, 12)
                            .TextWrapping(TextWrapping.Wrap)
                            .FontSize(14)
                    )
            ));

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly ILocalizationService _localizationService;
        private readonly IUiScaleService _uiScaleService;
        private readonly ISettingsService _settingsService;
        private bool _isSyncing;

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

        [ObservableProperty]
        public partial bool IsStylusModeEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsSingleFingerPanEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsAutoOpenTransformEditorAfterSelectionEnabled { get; set; }

        [ObservableProperty]
        public partial bool IsPenHapticsEnabled { get; set; }

        public State(
            AppState appState,
            ILocalizationService localizationService,
            IUiScaleService uiScaleService,
            ISettingsService settingsService)
        {
            _appState = appState;
            _localizationService = localizationService;
            _uiScaleService = uiScaleService;
            _settingsService = settingsService;

            AvailableLocales = localizationService.AvailableLocales;

            _appState.WatchFor(x => x.UiScale, SyncFromAppState);
            _appState.WatchFor(x => x.MouseWheelBehavior, OnMouseWheelBehaviorChangedExternally);
            _appState.WatchFor(x => x.IsTwoFingerDoubleTapUndoEnabled, OnTwoFingerUndoChangedExternally);
            _appState.WatchFor(x => x.TwoFingerDoubleTapTimeoutMs, OnTwoFingerTimeoutChangedExternally);
            _appState.WatchFor(x => x.IsStylusModeEnabled, OnStylusModeChangedExternally);
            _appState.WatchFor(x => x.IsSingleFingerPanEnabled, OnSingleFingerPanChangedExternally);
            _appState.WatchFor(x => x.IsAutoOpenTransformEditorAfterSelectionEnabled, OnAutoOpenTransformEditorAfterSelectionChangedExternally);
            _appState.WatchFor(x => x.IsPenHapticsEnabled, OnPenHapticsChangedExternally);

            SyncFromAppState();
        }

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

        partial void OnIsStylusModeEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.IsStylusModeEnabled = value;
            _settingsService.Set(nameof(AppState.IsStylusModeEnabled), value);
        }

        partial void OnIsSingleFingerPanEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.IsSingleFingerPanEnabled = value;
            _settingsService.Set(nameof(AppState.IsSingleFingerPanEnabled), value);
        }

        partial void OnIsAutoOpenTransformEditorAfterSelectionEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.IsAutoOpenTransformEditorAfterSelectionEnabled = value;
            _settingsService.Set(nameof(AppState.IsAutoOpenTransformEditorAfterSelectionEnabled), value);
        }

        partial void OnIsPenHapticsEnabledChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.IsPenHapticsEnabled = value;
            _settingsService.Set(nameof(AppState.IsPenHapticsEnabled), value);
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
            IsStylusModeEnabled = _appState.IsStylusModeEnabled;
            IsSingleFingerPanEnabled = _appState.IsSingleFingerPanEnabled;
            IsAutoOpenTransformEditorAfterSelectionEnabled = _appState.IsAutoOpenTransformEditorAfterSelectionEnabled;
            IsPenHapticsEnabled = _appState.IsPenHapticsEnabled;

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

        private void OnStylusModeChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.IsStylusModeEnabled), _appState.IsStylusModeEnabled);
        }

        private void OnSingleFingerPanChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.IsSingleFingerPanEnabled), _appState.IsSingleFingerPanEnabled);
        }

        private void OnAutoOpenTransformEditorAfterSelectionChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.IsAutoOpenTransformEditorAfterSelectionEnabled), _appState.IsAutoOpenTransformEditorAfterSelectionEnabled);
        }

        private void OnPenHapticsChangedExternally()
        {
            SyncFromAppState();
            _settingsService.Set(nameof(AppState.IsPenHapticsEnabled), _appState.IsPenHapticsEnabled);
        }
    }

    public sealed class MouseWheelBehaviorItem(Pix2d.Primitives.ViewPort.MouseWheelBehavior behavior, string title)
    {
        public Pix2d.Primitives.ViewPort.MouseWheelBehavior Behavior { get; } = behavior;

        public string Title { get; } = title;
    }
}