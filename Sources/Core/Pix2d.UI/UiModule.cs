namespace Pix2d.UI;

public class UiModule : IUiModule
{
    public object GetStyles() => new Styles.AppStyles();
    public Type GetMainViewType() => typeof(MainView);
}