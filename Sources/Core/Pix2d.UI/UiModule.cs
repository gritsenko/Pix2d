namespace Pix2d.UI;

public class UiModule : IUiModule
{
    public object GetStyles()
    {
        return new Styles.AppStyles();
    }
}