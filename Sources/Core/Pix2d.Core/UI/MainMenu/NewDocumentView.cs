using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Command;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.MainMenu;

public partial class NewDocumentView : ViewBase<NewDocumentView.State>
{
    public NewDocumentView(IProjectService projectService, IViewPortService viewPortService, ICommandService commandService, AppState appState)
        : base(new State(projectService, viewPortService, commandService, appState))
    {
    }

    protected override object Build(State state) =>
        new Border()
            .Padding(32, 0, 0, 0)
            .Child(
                new StackPanel()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        new TextBlock()
                            .FontSize(24)
                            .Text(L("New")),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text(L("Create new sprite")),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text(L("Preset")),

                        new ComboBox()
                            .DataTemplates(
                                GetTextTemplate<SizePreset>(x => x?.Title ?? "")
                            )!
                            .Margin(0, 8, 0, 0)
                            .MaxWidth(300)
                            .ItemsSource(state.AvailablePresets)
                            .SelectedItem(state, x => x.SelectedPreset, BindingMode.TwoWay),

                        new SliderEx().Label(L("Width")).Width(200).Units("px").Minimum(1).Maximum(1024)
                            .Value(state, x => x.ArtworkWidth, BindingMode.TwoWay),

                        new SliderEx().Label(L("Height")).Width(200).Units("px").Minimum(1).Maximum(1024)
                            .Value(state, x => x.ArtworkHeight, BindingMode.TwoWay),

                        new Button()
                            .Classes("btn")
                            .Content(L("Create"))
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Margin(0, 24, 0, 0)
                            .Background(StaticResources.Brushes.SelectedToolBrush)
                            .OnClick(_ => state.CreateProject())

                    ) //StackPanel.Children
            );


    private static IDataTemplate GetTextTemplate<T>(Func<T, string> func) =>
        new FuncDataTemplate<T>((itemVm, ns) => (Control)new TextBlock().Text(func(itemVm)))!;

    public sealed partial class State : ObservableObject
    {
        private readonly IProjectService _projectService;

        public ObservableCollection<SizePreset> AvailablePresets { get; } = [];

        [ObservableProperty]
        public partial SizePreset? SelectedPreset { get; set; }

        [ObservableProperty]
        public partial int ArtworkWidth { get; set; }

        [ObservableProperty]
        public partial int ArtworkHeight { get; set; }

        public State(IProjectService projectService, IViewPortService viewPortService, ICommandService commandService, AppState appState)
        {
            _projectService = projectService;
            ViewCommands = commandService.GetCommandList<ViewCommands>()!;

            appState.UiState.WatchFor(x => x.ShowMenu, EnsureLoaded);
            Load(viewPortService);
        }

        public ViewCommands ViewCommands { get; }

        public void CreateProject()
        {
            ViewCommands.HideMainMenuCommand.Execute();
            _ = _projectService.CreateNewProjectAsync(new SKSize(ArtworkWidth, ArtworkHeight));
        }

        partial void OnSelectedPresetChanged(SizePreset? value)
        {
            ArtworkWidth = value?.Width ?? 64;
            ArtworkHeight = value?.Height ?? 64;
        }

        private void EnsureLoaded()
        {
            if (AvailablePresets.Count == 0)
                Load(null);
        }

        private void Load(IViewPortService? viewPortService)
        {
            if (AvailablePresets.Count > 0)
                return;

            var bounds = viewPortService?.ViewPort?.Size ?? new SKSize(64, 64);
            var viewportWidth = (int)bounds.Width;
            var viewportHeight = (int)bounds.Height;

            AddPreset(64, 64, L("Custom"));
            AddPreset(16, 16);
            AddPreset(32, 32);
            AddPreset(48, 48);
            AddPreset(64, 64);
            AddPreset(128, 128);
            AddPreset(256, 256);
            AddPreset(512, 512);
            AddPreset(viewportWidth, viewportHeight, $"{viewportWidth}x{viewportHeight} {L("(Viewport size)")}");

            SelectedPreset = AvailablePresets[4];
        }

        private void AddPreset(int width, int height, string? title = null)
        {
            var preset = new SizePreset(width, height);
            if (title != null)
            {
                preset.Title = title;
            }

            AvailablePresets.Add(preset);
        }
    }

    public sealed class SizePreset
    {
        public SizePreset(int width, int height)
        {
            Width = width;
            Height = height;

            Title = $"{Width}x{Height}";
        }
        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}