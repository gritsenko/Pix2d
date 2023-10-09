using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Mvvm;
using Pix2d.Abstract.Platform;
using Pix2d.Mvvm;

namespace Pix2d.Plugins.Drawing.ViewModels;

public class TextBarViewModel : Pix2dViewModelBase
{
    public event EventHandler TextAplied;
    public IFontService FontService { get; }

    public string Text
    {
        get => Get<string>();
        set => Set(value);
    }

    public bool IsBold
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool IsItalic
    {
        get => Get<bool>();
        set => Set(value);
    }

    public bool IsAliased
    {
        get => Get<bool>();
        set => Set(value);
    }

    [UpdateCanExecute(nameof(Text))]
    public ICommand ApplyCommand => GetCommand(() =>
    {
        OnTextApplied();
        Logger.Log("Apply text");
        Text = "";
    }, () => !string.IsNullOrEmpty(Text));

    [UpdateCanExecute(nameof(Text))]
    public ICommand CancelCommand => GetCommand(() =>
    {
        Logger.Log("Cancel text");
        Text = "";
    }, () => !string.IsNullOrEmpty(Text));

    public ObservableCollection<FontItemViewModel> Fonts { get; set; } = new ObservableCollection<FontItemViewModel>();

    public FontItemViewModel SelectedFont
    {
        get => Get<FontItemViewModel>();
        set => Set(value);
    }

    public int FontSize
    {
        get => Get<int>(14);
        set => Set(value);
    }

    public TextBarViewModel(IFontService fontService)
    {
        FontService = fontService;
    }

    protected override Task OnLoadAsync()
    {
        return LoadFonts();
    }

    private async Task LoadFonts()
    {
        Fonts.Clear();
        var fonts = await FontService.GetAvailableFontNamesAsync();
        foreach (string font in fonts)
        {
            Fonts.Add(new FontItemViewModel(font));
            // Debug.WriteLine(string.Format("Font: {0}", font));
        }

        SelectedFont =
            Fonts.FirstOrDefault(x => x.Name.Equals("Arial", StringComparison.InvariantCultureIgnoreCase)) ??
            Fonts.FirstOrDefault();
    }

    protected virtual void OnTextApplied()
    {
        TextAplied?.Invoke(this, EventArgs.Empty);
    }
}