using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Interactivity;
using Pix2d.UI.Resources;
using SkiaSharp;

namespace Pix2d.UI.Shared;

public class ColorPalette : ViewBase
{
    #region AvaloniaProperties
    /// <summary>
    /// Colors
    /// </summary>
    public static readonly DirectProperty<ColorPalette, IList<SKColor>> ColorsProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, IList<SKColor>>(nameof(Colors), o => o.Colors, (o, v) => o.Colors = v);

    private IList<SKColor> _colors = new List<SKColor>();
    public IList<SKColor> Colors
    {
        get => _colors;
        set
        {
            SetAndRaise(ColorsProperty, ref _colors, value);
            OnColorsSet(ref _colors);
        }
    }

    /// <summary>
    /// Color to add
    /// </summary>
    public static readonly DirectProperty<ColorPalette, SKColor> ColorToAddProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, SKColor>(nameof(ColorToAdd), o => o.ColorToAdd, (o, v) => o.ColorToAdd = v);

    private SKColor _colorToAdd = SKColor.Empty;
    public SKColor ColorToAdd
    {
        get => _colorToAdd;
        set => SetAndRaise(ColorToAddProperty, ref _colorToAdd, value);
    }

    /// <summary>
    /// Select color command
    /// </summary>
    public static readonly DirectProperty<ColorPalette, ICommand> SelectColorCommandProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, ICommand>(nameof(SelectColorCommand), o => o.SelectColorCommand, (o, v) => o.SelectColorCommand = v);

    private ICommand _selectColorCommand;

    public ICommand SelectColorCommand
    {
        get => _selectColorCommand;
        set => SetAndRaise(SelectColorCommandProperty, ref _selectColorCommand, value);
    }

    /// <summary>
    /// Add Color Command
    /// </summary>
    public static readonly DirectProperty<ColorPalette, ICommand> AddColorCommandProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, ICommand>(nameof(AddColorCommand), o => o.AddColorCommand, (o, v) => o.AddColorCommand = v);

    private ICommand _addColorCommand;
    public ICommand AddColorCommand
    {
        get => _addColorCommand;
        set => SetAndRaise(AddColorCommandProperty, ref _addColorCommand, value);
    }


    /// <summary>
    /// Can add color
    /// </summary>
    public static readonly DirectProperty<ColorPalette, bool> CanAddColorProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, bool>(nameof(CanAddColor), o => o.CanAddColor, (o, v) => o.CanAddColor = v);

    private bool _canAddColor;
    public bool CanAddColor
    {
        get => _canAddColor;
        set
        {
            SetAndRaise(CanAddColorProperty, ref _canAddColor, value);
            OnColorsSet(ref _colors);
        }
    }

    /// <summary>
    /// Remove Color Command
    /// </summary>
    public static readonly DirectProperty<ColorPalette, ICommand> RemoveColorCommandProperty
        = AvaloniaProperty.RegisterDirect<ColorPalette, ICommand>(nameof(RemoveColorCommand), o => o.RemoveColorCommand, (o, v) => o.RemoveColorCommand = v);

    private ICommand _removeColorCommand;
    public ICommand RemoveColorCommand
    {
        get => _removeColorCommand;
        set => SetAndRaise(RemoveColorCommandProperty, ref _removeColorCommand, value);
    }

    #endregion

    protected override object Build() =>
        new Grid()
            .Children(
                new ItemsControl()
                    .Ref(out _itemsControl)
                    .ItemsPanel(StaticResources.Templates.WrapPanelTemplate)
                    .ItemsSource(ColorsProperty)
                    .ItemTemplate(new FuncDataTemplate<SKColor>((itemVm, ns) =>
                            itemVm == SKColor.Empty
                                ? new Button() //ADD COLOR BUTTON
                                    .Content(
                                        new Border()
                                            .Background(ColorToAddProperty, converter: StaticResources.Converters.SKColorToBrushConverter)
                                            .Child(new TextBlock()
                                                .Text("+")
                                                .FontSize(18)
                                                .FontWeight(FontWeight.Bold)
                                            )
    )
                                    .OnClick(OnAddColorClicked)
                                    .Background(Brushes.Transparent/*, converter: StaticResources.Converters.SKColorToBrushConverter*/)
                                    .Width(36)
                                    .Height(36)
                                : new Button() // COLOR ITEM
                                    .Background(itemVm.ToBrush())
                                    .BorderBrush(itemVm.ToBrush())
                                    .OnClick(args => OnColorItemClicked(itemVm))
                                    .Width(36)
                                    .Height(36)
                                    .With(b =>
                                    {
                                        var flyout = new MenuFlyout() { Placement = PlacementMode.Bottom };
                                        flyout.AddItem("Delete color", RemoveColorCommand, itemVm);
                                        b.ContextFlyout = flyout;
                                    })
                        )
                    )

            );

    private ItemsControl _itemsControl;

    private IList<SKColor> _sourceColorsCollection;

    private void OnAddColorClicked(RoutedEventArgs obj)
    {
        if (AddColorCommand?.CanExecute(ColorToAdd) == true)
        {
            AddColorCommand?.Execute(ColorToAdd);
        }
    }

    private void OnColorItemClicked(SKColor itemVm)
    {
        SelectColorCommand?.Execute(itemVm);
    }

    private void OnColorsSet(ref IList<SKColor> colors)
    {
        if (CanAddColor)
        {
            if (_sourceColorsCollection is ObservableCollection<SKColor> oldColors)
            {
                oldColors.CollectionChanged -= Obs_CollectionChanged;
            }

            _sourceColorsCollection = colors;

            if (_sourceColorsCollection is ObservableCollection<SKColor> obs)
            {
                obs.CollectionChanged += Obs_CollectionChanged;
            }

            _itemsControl.ItemsSource = GetCurrentColors();
        }
        else
            _itemsControl.ItemsSource = colors;
    }

    private void Obs_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _itemsControl.ItemsSource = GetCurrentColors();
    }

    private IEnumerable GetCurrentColors()
    {
        var colors = new List<SKColor>();
        colors.AddRange(_sourceColorsCollection);
        colors.Add(SKColor.Empty);
        return colors;
    }
}