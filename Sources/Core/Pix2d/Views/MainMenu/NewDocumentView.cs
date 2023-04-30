using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Interactivity;
using Pix2d.Shared;
using Pix2d.ViewModels.MainMenu;
using SkiaSharp;

namespace Pix2d.Views.MainMenu;

public class NewDocumentView : ComponentBase
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
                            .Text("New"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Create new sprite"),

                        new TextBlock()
                            .Margin(0, 8, 0, 8)
                            .Text("Preset"),

                        new ComboBox()
                            .DataTemplates(
                                GetTextTemplate<NewDocumentSettingsPresetViewModel>(x => x?.Title ?? "")
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


    private NewDocumentSettingsPresetViewModel? _selectedPreset = null!;
    private int _artworkWidth;
    private int _artworkHeight;


    private IDataTemplate GetTextTemplate<T>(Func<T, string> func) =>
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


    public ObservableCollection<NewDocumentSettingsPresetViewModel> AvailablePresets { get; set; } = new();

    public NewDocumentSettingsPresetViewModel SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value;
            ArtworkWidth = value?.Width ?? 64;
            ArtworkHeight = value?.Height ?? 64;
            StateHasChanged();
        }
    }

    protected override void OnAfterInitialized()
    {
        Load();
    }

    private void OnCreateClicked(RoutedEventArgs obj)
    {
        Commands.View.HideMainMenuCommand.Execute();
        ProjectService.CreateNewProjectAsync(new SKSize(ArtworkWidth, ArtworkHeight));
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
            this.AvailablePresets.Add(new NewDocumentSettingsPresetViewModel());
        else
        {
            var p = new NewDocumentSettingsPresetViewModel(width, height);
            if (title != default)
            {
                p.Title = title;
            }
            this.AvailablePresets.Add(p);
        }
    }

}