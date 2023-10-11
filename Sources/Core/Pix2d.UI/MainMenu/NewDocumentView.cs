using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Interactivity;
using Pix2d.Messages;
using Pix2d.UI.Resources;
using Pix2d.UI.Shared;
using SkiaSharp;

namespace Pix2d.UI.MainMenu;

public class NewDocumentView : ComponentBase
{
    public string ProjectName
    {
        get => _projectName;
        set
        {
            _projectName = value;
            OnPropertyChanged();
        }
    }

    public NewDocumentView()
    {
        Messenger.Default.Register<StateChangedMessage>(this, msg => msg.OnPropertyChanged<UiState>(state => state.ShowMenu, () => Reset()));
    }

    protected override object Build() =>
        new Border()
            .Padding(32, 0, 0, 0)
            .Child(
                new StackPanel()
                    .HorizontalAlignment(HorizontalAlignment.Left)
                    .Children(
                        new TextBlock()
                            .FontSize(24)
                            .Text("New"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Create new sprite"),
                        
                        new TextBox().Watermark("Name of the project")
                            // .IsVisible(Pix2DApp.Instance.AppState.Settings.AutoSaveNewProject)
                            .Text(@ProjectName, BindingMode.TwoWay),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Preset"),

                        new ComboBox()
                            .DataTemplates(
                                GetTextTemplate<SizePreset>(x => x?.Title ?? "")
                            )
                            .Margin(0, 8, 0, 0)
                            .MaxWidth(300)
                            .ItemsSource(AvailablePresets)
                            .SelectedItem(Bind(SelectedPreset, BindingMode.TwoWay)),

                        new SliderEx()
                            .Header("Width")
                            .Width(200)
                            .Units("px")
                            .Minimum(1)
                            .Maximum(1024)
                            .Value(ArtworkWidth, BindingMode.TwoWay, bindingSource: this),

                        new SliderEx()
                            .Header("Height")
                            .Width(200)
                            .Units("px")
                            .Minimum(1)
                            .Maximum(1024)
                            .Value(ArtworkHeight, BindingMode.TwoWay, bindingSource: this),

                        new Button()
                            .Content("Create")
                            .HorizontalAlignment(HorizontalAlignment.Left)
                            .Width(100)
                            .Margin(0, 24, 0, 0)
                            .Background(StaticResources.Brushes.SelectedHighlighterBrush)
                            .OnClick(OnCreateClicked)

                    ) //StackPanel.Children
            );


    private SizePreset? _selectedPreset = null!;
    private int _artworkWidth;
    private int _artworkHeight;
    private string _projectName = "";


    private static IDataTemplate GetTextTemplate<T>(Func<T, string> func) =>
        new FuncDataTemplate<T>((itemVm, ns) => new TextBlock().Text(func(itemVm)));


    [Inject] private IProjectService ProjectService { get; set; }

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


    public ObservableCollection<SizePreset> AvailablePresets { get; set; } = new();

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
        Load();
    }

    private void OnCreateClicked(RoutedEventArgs obj)
    {
        Commands.View.HideMainMenuCommand.Execute();
        ProjectService.CreateNewProjectAsync(new SKSize(ArtworkWidth, ArtworkHeight), ProjectName);
    }

    protected void Load()
    {
        var bounds = Pix2DApp.Instance.ViewPort?.Size ?? new SKSize(64, 64);
        var viewportWidth = (int)bounds.Width;
        var viewportHeight = (int)bounds.Height;

        if (!AvailablePresets.Any())
        {
            AddPreset();
            AddPreset(16, 16);
            AddPreset(32, 32);
            AddPreset(48, 48);
            AddPreset(64, 64);
            AddPreset(128, 128);
            AddPreset(256, 256);
            AddPreset(512, 512);
            AddPreset(viewportWidth, viewportHeight, $"{viewportWidth}x{viewportHeight} (Viewport size)");

            SelectedPreset = AvailablePresets[4];

            ArtworkWidth = SelectedPreset.Width;
            ArtworkHeight = SelectedPreset.Height;
        }
        StateHasChanged();
    }

    private void AddPreset(int width = default, int height = default, string title = default)
    {
        if (width == default)
            this.AvailablePresets.Add(new SizePreset());
        else
        {
            var p = new SizePreset(width, height);
            if (title != default)
            {
                p.Title = title;
            }
            this.AvailablePresets.Add(p);
        }
    }

    private void Reset()
    {
        ProjectName = "";
    }

    public class SizePreset
    {
        public SizePreset(int width, int height)
        {
            Width = width;
            Height = height;

            Title = $"{Width}x{Height}";
        }

        public SizePreset()
        {
            Title = "Custom";
        }

        public string Title { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}