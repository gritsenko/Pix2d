using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.Plugins.Sprite.Commands;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using Pix2d.UI.Styles;

namespace Pix2d.UI;

public partial class TopBarView(IOperationService operationService, IMessenger messenger, AppState appState, ICommandService commandService)
    : ViewBase<TopBarView.State>(new State(operationService, messenger, appState, commandService))
{
    protected override StyleGroup BuildStyles() =>
    [
        new Style<BlurPanel>(s => s.OfType<BlurPanel>().Name("central-panel"))
            .ColSpan(3),

        new StyleGroup(_ => VisualStates.Narrow())
        {
            new Style<BlurPanel>(s => s.OfType<BlurPanel>().Name("central-panel"))
                .Col(1)
                .ColSpan(1),
        }
    ];

    protected override object Build(State state) =>
        new Grid()
            .Cols("Auto,*,Auto")
            .Margin(12, 12, 12, 0)
            .Children(
                //MENU BUTTON
                new BlurPanel().Col(0)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(
                        new AppButton()
                            .Label(L("Menu"))
                            .Content("\xe91d")
                            .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                            .Command(state.ViewCommands.ToggleMainMenuCommand)
                            .ToolTip_Tip(L(state.ViewCommands.ToggleMainMenuCommand.Tooltip))
                    ),
                //CENTRAL BLOCK
                new BlurPanel().Name("central-panel")
                    .DisableBlur(true)
                    .HorizontalAlignment(HorizontalAlignment.Center)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new AppButton()
                                    .Command(state.SpriteEditCommands.Clear)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Label(L("Clear"))
                                    .Content("\xe90f")
                                    .ToolTip_Tip(L(state.SpriteEditCommands.Clear.Tooltip)),
                                new AppButton()
                                    .Command(state.SpriteEditCommands.AddArtboard)
                                    .IconFontFamily(StaticResources.Fonts.IconFontSegoe)
                                    .Label(L("Sprite"))
                                    .Content("\xE710")
                                    .ToolTip_Tip(L(state.SpriteEditCommands.AddArtboard.Tooltip)),
                                new AppButton()
                                    .Name("export-button")
                                    .Label(L("Export"))
                                    .Command(state.ViewCommands.ShowExportDialogCommand)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe907")
                                    .ToolTip_Tip(L(state.ViewCommands.ShowExportDialogCommand.Tooltip)),
                                new AppToggleButton()
                                    .IsChecked(state, x => x.ShowExtraTools, BindingMode.TwoWay)
                                    .Label(L("Tools"))
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe909")
                                    .ToolTip_Tip(L(state.ViewCommands.ToggleExtraToolsCommand.Tooltip))
                            )
                    ),
                //UNDO REDO BLOCK
                new BlurPanel().Name("undo-panel").Col(2)
                    .Content(
                        new StackPanel()
                            .Orientation(Orientation.Horizontal)
                            .Children(
                                new AppButton().Col(1)
                                    .Command(state.EditCommands.Undo)
                                    .Label(L("Undo"))
                                    .ToolTip_Tip(L(state.EditCommands.Undo.Tooltip))
                                    .Content(
                                        new Grid()
                                            .HorizontalAlignment(HorizontalAlignment.Stretch)
                                            .VerticalAlignment(VerticalAlignment.Stretch)
                                            .Width(50)
                                            .Height(30)
                                            .Children(
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.DefaultTextFontFamily)
                                                    .Margin(new Thickness(0, 0, 4, 4))
                                                    .HorizontalAlignment(HorizontalAlignment.Right)
                                                    .VerticalAlignment(VerticalAlignment.Top)
                                                    .FontSize(12)
                                                    .Foreground(Colors.Gray.ToBrush())
                                                    .Text(state, x => x.UndoStepsText),
                                                new TextBlock()
                                                    .FontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                                    .VerticalAlignment(VerticalAlignment.Center)
                                                    .HorizontalAlignment(HorizontalAlignment.Center)
                                                    .Text("\xe90b")
                                            )),
                                new AppButton().Col(1)
                                    .Label(L("Redo"))
                                    .Command(state.EditCommands.Redo)
                                    .IconFontFamily(StaticResources.Fonts.Pix2dIconFontFamilyV3)
                                    .Content("\xe90d")
                                    .ToolTip_Tip(L(state.EditCommands.Redo.Tooltip))
                            )
                    )
            );

    public sealed partial class State : ObservableObject
    {
        private readonly AppState _appState;
        private readonly IOperationService _operationService;
        private bool _isSyncing;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(UndoStepsText))]
        public partial int UndoSteps { get; set; }

        [ObservableProperty]
        public partial bool ShowExtraTools { get; set; }

        public State(IOperationService operationService, IMessenger messenger, AppState appState, ICommandService commandService)
        {
            _appState = appState;
            _operationService = operationService;

            SpriteEditCommands = commandService.GetCommandList<SpriteEditCommands>()!;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;
            EditCommands = commandService.GetCommandList<EditCommands>()!;

            SyncFromAppState();

            messenger.Register<OperationInvokedMessage>(this, _ => UpdateUndoSteps());
            messenger.Register<ProjectLoadedMessage>(this, _ => UpdateUndoSteps());
            // Tab switches swap the whole undo history (per-project stacks).
            messenger.Register<ProjectActivatedMessage>(this, _ => UpdateUndoSteps());
            _appState.UiState.WatchFor(x => x.ShowExtraTools, SyncFromAppState);
        }

        public SpriteEditCommands SpriteEditCommands { get; }

        public ViewCommands ViewCommands { get; }

        public EditCommands EditCommands { get; }

        public string UndoStepsText => UndoSteps.ToString();

        partial void OnShowExtraToolsChanged(bool value)
        {
            if (_isSyncing)
                return;

            _appState.UiState.ShowExtraTools = value;
        }

        private void SyncFromAppState()
        {
            _isSyncing = true;
            ShowExtraTools = _appState.UiState.ShowExtraTools;
            UpdateUndoSteps();
            _isSyncing = false;
        }

        private void UpdateUndoSteps()
        {
            UndoSteps = _operationService?.UndoOperationsCount ?? 0;
        }
    }
}