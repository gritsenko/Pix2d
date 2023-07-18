using Avalonia.Styling;

namespace Pix2d.Views.MainMenu;

public class MainMenuView : ComponentBase
{
    protected override object Build()
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
                    .Background(StaticResources.Brushes.SelectedItemBrush)
                    .Children(
                        new ItemsControl()
                            //.ItemTemplate(_menuItemTemplate)
                            .Items(
                                new MainMenuItemView()
                                    .Header("Back")
                                    .Icon("")
                                    .OnClicked(_ => Commands.View.HideMainMenuCommand.Execute()),
                                new MainMenuItemView()
                                    .Header("New")
                                    .Icon("\xE7C3")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(NewDocumentView)),

                                new MainMenuItemView()
                                    .Header("Open")
                                    .Icon("\xED41")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(OpenDocumentView)),

                                new MainMenuItemView()
                                    .Header("Save")
                                    .Icon("\xE74E")
                                    .OnClicked(OnItemClick),

                                new MainMenuItemView()
                                    .Header("Save as")
                                    .Icon("\xE792")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(SaveDocumentView)),

                                //new MainMenuItemView()
                                //    .Header("License")
                                //    .Icon("0xE719")
                                //    .OnClicked(OnItemClick),

                                new MainMenuItemView()
                                    .Header("Community\nand support")
                                    .Icon("\xE8F2")
                                    .OnClicked(OnItemClick)
                                    .TabViewType(typeof(SupportView))
                            ),
                        new Border().Col(1)
                            .Background(StaticResources.Brushes.PanelsBackgroundBrush)
                            .Child(
                                new ContentControl()
                                    .Ref(out _tabContent)
                            )
                    )
            );
    }

    ContentControl _tabContent;

    private void OnItemClick(MainMenuItemView obj)
    {
        _tabContent.Content = obj.GetTabContent();
    }

}