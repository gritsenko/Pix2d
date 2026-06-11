using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.Messages.ViewPort;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.UI;

public partial class ZoomPanelView(IViewPortService viewPortService, IMessenger messenger, ICommandService commandService)
    : ViewBase<ZoomPanelView.State>(new State(viewPortService, messenger, commandService))
{
    private static readonly IReadOnlyList<float> KeyZoomScales = [0.25f, 0.5f, 1f, 2f, 4f, 8f, 16f];
    private const int LongPressDelayMs = 450;

    protected override StyleGroup BuildStyles() =>
    [
        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<BlurPanel>(s => s.Name("ZoomButtonsPanel"))
                .IsVisible(false)
        }
    ];

    protected override object Build(State state) => BuildContent(state);

    private object BuildContent(State state)
    {
        var longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressDelayMs) };
        Button zoomButton = null!;
        var isLongPressTriggered = false;
        var isLongPressTracking = false;

        void StopLongPressTracking()
        {
            longPressTimer.Stop();
            isLongPressTracking = false;
        }

        void ShowZoomFlyout()
        {
            if (zoomButton == null)
                return;

            var flyout = CreateZoomFlyout(state);
            flyout.Closed += (_, _) => isLongPressTriggered = false;
            flyout.ShowAt(zoomButton);
        }

        longPressTimer.Tick += (_, _) =>
        {
            longPressTimer.Stop();
            if (!isLongPressTracking)
                return;

            isLongPressTriggered = true;
            ShowZoomFlyout();
        };

        return new Grid()
            .Styles(
                // Figma "Body 16": Zed Mono Extended 16px.
                new Style<Button>()
                    .FontSize(16)
                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
            )
            .Height(56)
            .Cols("*,Auto")
            .Children(

                new BlurPanel()
                    .Margin(0, 0, 6, 0)
                    .MinWidth(80)
                    .Content(
                        new Button()
                            .Ref(out zoomButton)
                            .Classes("app-button")
                            .OnPointerPressed(e =>
                            {
                                var point = e.GetCurrentPoint(zoomButton);
                                var isSecondaryPress = point.Properties.IsRightButtonPressed;
                                if (isSecondaryPress)
                                {
                                    StopLongPressTracking();
                                    isLongPressTriggered = false;
                                    ShowZoomFlyout();
                                    e.Handled = true;
                                    return;
                                }

                                var isPrimaryPress = point.Properties.IsLeftButtonPressed || e.Pointer.Type == PointerType.Touch;
                                if (!isPrimaryPress)
                                    return;

                                isLongPressTriggered = false;
                                isLongPressTracking = true;
                                longPressTimer.Stop();
                                longPressTimer.Start();
                            })
                            .OnPointerReleased(_ => StopLongPressTracking())
                            .OnPointerCaptureLost(_ => StopLongPressTracking())
                            .OnPointerExited(_ =>
                            {
                                if (isLongPressTracking && !isLongPressTriggered)
                                    StopLongPressTracking();
                            })
                            .OnClick(_ =>
                            {
                                if (isLongPressTriggered)
                                {
                                    isLongPressTriggered = false;
                                    return;
                                }

                                state.ViewCommands.ZoomAll.Execute(null);
                            })
                            .Content(state, x => x.CurrentPercentZoom)
                        ),

                new BlurPanel().Col(1)
                    .Name("ZoomButtonsPanel")
                    .Margin(6, 0, 0, 0)
                    .ClipToBounds(true)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new Button()
                                    .Classes("app-button")
                                    .Command(state.ViewCommands.ZoomOut)
                                    .Content(
                                        new Path()
                                            .Width(24)
                                            .Height(24)
                                            .Data(Geometry.Parse("M3.25092 11.2501C3.15153 11.2487 3.05286 11.267 2.96063 11.3041C2.86841 11.3412 2.78447 11.3962 2.71369 11.466C2.64291 11.5357 2.58671 11.6189 2.54835 11.7106C2.50999 11.8023 2.49023 11.9007 2.49023 12.0001C2.49023 12.0995 2.50999 12.1979 2.54835 12.2896C2.58671 12.3813 2.64291 12.4644 2.71369 12.5342C2.78447 12.604 2.86841 12.659 2.96063 12.6961C3.05286 12.7331 3.15153 12.7515 3.25092 12.7501H20.7509C20.8503 12.7515 20.949 12.7331 21.0412 12.6961C21.1334 12.659 21.2174 12.604 21.2881 12.5342C21.3589 12.4644 21.4151 12.3813 21.4535 12.2896C21.4918 12.1979 21.5116 12.0995 21.5116 12.0001C21.5116 11.9007 21.4918 11.8023 21.4535 11.7106C21.4151 11.6189 21.3589 11.5357 21.2881 11.466C21.2174 11.3962 21.1334 11.3412 21.0412 11.3041C20.949 11.267 20.8503 11.2487 20.7509 11.2501H3.25092Z"))
                                            .Fill(StaticResources.Brushes.ForegroundBrush)),

                                new Button()
                                    .Classes("app-button")
                                    .Command(state.ViewCommands.ZoomIn)
                                    .Content(
                                        new Path()
                                            .Width(24)
                                            .Height(24)
                                            .Data(Geometry.Parse("M11.9892 2.48935C11.7905 2.49245 11.6011 2.57432 11.4626 2.71696C11.3242 2.85959 11.2481 3.05135 11.2509 3.25009V11.2501H3.25092C3.15153 11.2487 3.05286 11.267 2.96063 11.3041C2.86841 11.3412 2.78447 11.3962 2.71369 11.466C2.64291 11.5358 2.58671 11.6189 2.54835 11.7106C2.50999 11.8023 2.49023 11.9007 2.49023 12.0001C2.49023 12.0995 2.50999 12.1979 2.54835 12.2896C2.58671 12.3813 2.64291 12.4644 2.71369 12.5342C2.78447 12.604 2.86841 12.659 2.96063 12.6961C3.05286 12.7331 3.15153 12.7515 3.25092 12.7501H11.2509V20.7501C11.2495 20.8495 11.2679 20.9481 11.3049 21.0404C11.342 21.1326 11.397 21.2165 11.4668 21.2873C11.5366 21.3581 11.6197 21.4143 11.7114 21.4527C11.8031 21.491 11.9015 21.5108 12.0009 21.5108C12.1003 21.5108 12.1987 21.491 12.2904 21.4527C12.3821 21.4143 12.4653 21.3581 12.535 21.2873C12.6048 21.2165 12.6598 21.1326 12.6969 21.0404C12.734 20.9481 12.7523 20.8495 12.7509 20.7501V12.7501H20.7509C20.8503 12.7515 20.949 12.7331 21.0412 12.6961C21.1334 12.659 21.2174 12.604 21.2881 12.5342C21.3589 12.4644 21.4151 12.3813 21.4535 12.2896C21.4918 12.1979 21.5116 12.0995 21.5116 12.0001C21.5116 11.9007 21.4918 11.8023 21.4535 11.7106C21.4151 11.6189 21.3589 11.5358 21.2881 11.466C21.2174 11.3962 21.1334 11.3412 21.0412 11.3041C20.949 11.267 20.8503 11.2487 20.7509 11.2501H12.7509V3.25009C12.7524 3.14971 12.7336 3.05006 12.6958 2.95705C12.6581 2.86403 12.602 2.77955 12.531 2.70861C12.4599 2.63767 12.3754 2.5817 12.2823 2.54404C12.1893 2.50638 12.0896 2.48778 11.9892 2.48935Z"))
                                            .Fill(StaticResources.Brushes.ForegroundBrush))
                            )
                    )
            );
    }

    private MenuFlyout CreateZoomFlyout(State state)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.Bottom };
        var viewPort = viewPortService.ViewPort;

        flyout.Items.Add(new MenuItem()
            .Header(L("Zoom In"))
            .Command(state.ViewCommands.ZoomIn));
        flyout.Items.Add(new MenuItem()
            .Header(L("Zoom Out"))
            .Command(state.ViewCommands.ZoomOut));
        flyout.Items.Add(new MenuItem()
            .Header("100%")
            .Command(state.ViewCommands.Zoom100));
        flyout.Items.Add(new MenuItem()
            .Header(L("Zoom All"))
            .Command(state.ViewCommands.ZoomAll));
        flyout.Items.Add(new Separator());

        foreach (var scale in KeyZoomScales)
        {
            var currentScale = scale;
            var isActive = Math.Abs(viewPort.Zoom - currentScale) < 0.0001f;

            flyout.Items.Add(new MenuItem()
                .Header($"{currentScale * 100:0}%{(isActive ? " \u2713" : string.Empty)}")
                .OnClick(_ => viewPort.SetZoom(currentScale)));
        }

        return flyout;
    }

    public sealed partial class State : ObservableObject
    {
        private readonly IViewPortService _viewPortService;

        [ObservableProperty]
        public partial string CurrentPercentZoom { get; set; } = "0%";

        public ViewCommands ViewCommands { get; }

        public State(IViewPortService viewPortService, IMessenger messenger, ICommandService commandService)
        {
            _viewPortService = viewPortService;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;

            Load();

            messenger.Register<ViewPortInitializedMessage>(this, _ => Load());
            messenger.Register<ViewPortChangedViewMessage>(this, _ => Load());
        }

        private void Load()
        {
            var currentZoom = _viewPortService.ViewPort?.Zoom ?? 0;
            CurrentPercentZoom = (currentZoom * 100).ToString("###0") + "%";
        }
    }
}
