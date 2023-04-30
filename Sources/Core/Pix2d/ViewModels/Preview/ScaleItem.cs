using System.ComponentModel;
using System.Windows.Input;

namespace Pix2d.ViewModels.Preview;

[Bindable(BindableSupport.Yes)]
public class ScaleItem
{
    public double Scale { get; set; }
    public string Title => Scale + "x";

    public ICommand SelectScaleCommand { get; set; }

    public ScaleItem(double scale, ICommand selectCommand)
    {
        Scale = scale;
        SelectScaleCommand = selectCommand;
    }
}