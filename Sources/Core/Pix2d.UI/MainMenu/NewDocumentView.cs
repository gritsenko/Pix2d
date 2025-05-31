using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Pix2d.Command;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.MainMenu;

public class NewDocumentView : LocalizedComponentBase
{
    protected override object Build() =>
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
                            )
                            .Margin(0, 8, 0, 0)
                            .MaxWidth(300)
                            .ItemsSource(AvailablePresets)
                            .SelectedItem(Bind(SelectedPreset, BindingMode.TwoWay)),

                        new SliderEx().Label(L("Width")).Width(200).Units("px").Minimum(1).Maximum(1024)
                            .Value(ArtworkWidth, BindingMode.TwoWay, bindingSource: this),

                        new SliderEx().Label(L("Height")).Width(200).Units("px").Minimum(1).Maximum(1024)
                            .Value(ArtworkHeight, BindingMode.TwoWay, bindingSource: this),

                        new Button()
                            .Classes("btn")
                            .Content(L("Create"))
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Margin(0, 24, 0, 0)
                            .Background(StaticResources.Brushes.SelectedToolBrush)
                            .OnClick(OnCreateClicked)

                    ) //StackPanel.Children
            );


    private SizePreset? _selectedPreset;
    private int _artworkWidth;
    private int _artworkHeight;


    private static IDataTemplate GetTextTemplate<T>(Func<T, string> func) =>
        new FuncDataTemplate<T>((itemVm, ns) => new TextBlock().Text(func(itemVm)));


    [Inject] private IProjectService ProjectService { get; set; }
    [Inject] private IViewPortService ViewPortService { get; set; }
    [Inject] private ICommandService CommandService { get; set; } = null!;


    private ViewCommands ViewCommands => CommandService.GetCommandList<ViewCommands>()!;


    public int ArtworkWidth
    {
        get => _artworkWidth;
        set
        {
            if (value == _artworkWidth) return;
            _artworkWidth = value;
            OnPropertyChanged();
        }
    }

    public int ArtworkHeight
    {
        get => _artworkHeight;
        set
        {
            if (value == _artworkHeight) return;
            _artworkHeight = value;
            OnPropertyChanged();
        }
    }


    public ObservableCollection<SizePreset> AvailablePresets { get; set; } = [];

    public SizePreset SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
            ArtworkWidth = value?.Width ?? 64;
            ArtworkHeight = value?.Height ?? 64;
            StateHasChanged();
            OnPropertyChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Messenger.Default.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(state => state.ShowMenu, Reset));

        Load();
    }

    private void OnCreateClicked(RoutedEventArgs obj)
    {
        ViewCommands.HideMainMenuCommand.Execute();
        ProjectService.CreateNewProjectAsync(new SKSize(ArtworkWidth, ArtworkHeight));
    }

    protected void Load()
    {
        var bounds = ViewPortService.ViewPort?.Size ?? new SKSize(64, 64);
        var viewportWidth = (int)bounds.Width;
        var viewportHeight = (int)bounds.Height;

        if (!AvailablePresets.Any())
        {
            AddPreset(64, 64, L("Custom")());
            AddPreset(16, 16);
            AddPreset(32, 32);
            AddPreset(48, 48);
            AddPreset(64, 64);
            AddPreset(128, 128);
            AddPreset(256, 256);
            AddPreset(512, 512);
            AddPreset(viewportWidth, viewportHeight, $"{viewportWidth}x{viewportHeight} {L("(Viewport size)")()}");

            SelectedPreset = AvailablePresets[4];

            ArtworkWidth = SelectedPreset.Width;
            ArtworkHeight = SelectedPreset.Height;
        }
        StateHasChanged();
    }

    private void AddPreset(int width, int height, string? title = null)
    {
        var p = new SizePreset(width, height);
        if (title != null)
        {
            p.Title = title;
        }
        AvailablePresets.Add(p);
    }

    private void Reset()
    {
        // reset presets here
    }

    public class SizePreset
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