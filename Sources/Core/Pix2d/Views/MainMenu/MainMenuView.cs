using Avalonia.Styling;
using CommonServiceLocator;
using Pix2d.ViewModels.MainMenu;

namespace Pix2d.Views.MainMenu;

public class MainMenuView : ViewBaseSingletonVm<MainMenuViewModel>
{
    private IDataTemplate _menuItemTemplate =
        new FuncDataTemplate<MainMenuItemViewModel>((itemVm, ns) =>
            new Button()
                //.Background(@itemVm.IsSelected, StaticResources.Converters.BoolToBrushItemBackgroundConverter)
                .BindClass(@itemVm.IsSelected, "selected")
                .FontSize(16)
                .Content(
                    new Grid().Cols("32,*")
                        .HorizontalAlignment(HorizontalAlignment.Stretch)
                        .Children(
                            new TextBlock().Col(0) //icon
                                .HorizontalAlignment(HorizontalAlignment.Center)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .FontFamily(StaticResources.Fonts.IconFontSegoe)
                                .IsVisible(!itemVm.IsSplitter)
                                .Text(itemVm.IconGlyph.ToString()),
                            new TextBlock().Col(1)
                                .VerticalAlignment(VerticalAlignment.Center)
                                .Text(itemVm.Name)
                            )
                    )
                .Padding(8, 8, 8, 8)
                .HorizontalContentAlignment(HorizontalAlignment.Left)
                .Command(itemVm.SelectCommand)
                .CommandParameter(itemVm)
            );

    protected override object Build(MainMenuViewModel vm)
    {
        this.Styles(
            //Typed style definition
            new Style<Button>(s => s.OfType<Button>().Class("selected"))
                .Background(StaticResources.Brushes.AccentBrush),

            //General style definition
            new Style(s => s.OfType<Button>().Class("selected"))
                .Setter(TemplatedControl.BackgroundProperty, StaticResources.Brushes.AccentBrush)
        );

        return new Border()
            .Background(Brushes.DarkGray)
            .Child(
                new Grid().Cols("200,*")
                    .Children(
                        new ItemsControl()
                            .Items(vm.MenuItems)
                            .Background(StaticResources.Brushes.SelectedItemBrush)
                            .ItemTemplate(_menuItemTemplate),
                        new Border().Col(1)
                            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                            .Child(
                                new ContentControl()
                                    .DataTemplates(
                                        NewFileTemplate,
                                        OpenFileTemplate,
                                        SaveFileTemplate,
                                        SupportTemplate
                                    )
                                    .Content(@vm.SelectedTab, BindingMode.OneWay)
                            )
                    )
            );
    }

    public static FuncDataTemplate<NewDocumentSettingsViewModel> NewFileTemplate =>
        new((vm, ns) => new NewDocumentView(vm));

    public static FuncDataTemplate<OpenDocumentViewModel> OpenFileTemplate =>
        new((vm, ns) => new OpenDocumentView(vm));

    public static FuncDataTemplate<SaveDocumentViewModel> SaveFileTemplate =>
        new((vm, ns) => new SaveDocumentView(vm));

    public static FuncDataTemplate<SupportViewModel> SupportTemplate => new((vm, ns) =>
        new Grid().Children(
            new Button()
                .VerticalAlignment(VerticalAlignment.Center)
                .HorizontalAlignment(HorizontalAlignment.Center)
                .Content("https://boosty.to/pix2d")
                .OnClick(args =>
                {
                    ServiceLocator.Current.GetInstance<IPlatformStuffService>().OpenUrlInBrowser("https://boosty.to/pix2d");
                })
        )
    );

}