using System.Collections;
using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Pix2d.Common.Extensions;
using Pix2d.UI.Resources;
using SkiaSharp;
using Path = Avalonia.Controls.Shapes.Path;

namespace Pix2d.UI.Shared;

public class ColorPalette : LocalizedComponentBase
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
    
    #endregion

    public event Action<SKColor>? ColorSelected;
    public event Action<SKColor>? ColorAdded;
    public event Action<SKColor>? ColorRemoved;

    protected override object Build() =>
        new Grid()
            .Children(
                new ItemsControl()
                    .Ref(out _itemsControl)
                    .ItemsPanel(StaticResources.Templates.WrapPanelTemplate)
                    .ItemsSource(ColorsProperty)
                    .ItemTemplate(new FuncDataTemplate<SKColor>((itemVm, _) =>
                            itemVm == SKColor.Empty
                                ? new Button() //ADD COLOR BUTTON
                                    .Background(ColorToAddProperty, BindingMode.OneWay,
                                        StaticResources.Converters.SKColorToBrushConverter, this)
                                    .OnClick(OnAddColorClicked)
                                    .Margin(6)
                                    .Width(32)
                                    .Height(32)
                                    .CornerRadius(32)
                                    .Content(
                                        new Path()
                                            .Width(24)
                                            .Height(24)
                                            .Data(Geometry.Parse(
                                                "M11.9892 2.48935C11.7905 2.49245 11.6011 2.57432 11.4626 2.71696C11.3242 2.85959 11.2481 3.05135 11.2509 3.25009V11.2501H3.25092C3.15153 11.2487 3.05286 11.267 2.96063 11.3041C2.86841 11.3412 2.78447 11.3962 2.71369 11.466C2.64291 11.5358 2.58671 11.6189 2.54835 11.7106C2.50999 11.8023 2.49023 11.9007 2.49023 12.0001C2.49023 12.0995 2.50999 12.1979 2.54835 12.2896C2.58671 12.3813 2.64291 12.4644 2.71369 12.5342C2.78447 12.604 2.86841 12.659 2.96063 12.6961C3.05286 12.7331 3.15153 12.7515 3.25092 12.7501H11.2509V20.7501C11.2495 20.8495 11.2679 20.9481 11.3049 21.0404C11.342 21.1326 11.397 21.2165 11.4668 21.2873C11.5366 21.3581 11.6197 21.4143 11.7114 21.4527C11.8031 21.491 11.9015 21.5108 12.0009 21.5108C12.1003 21.5108 12.1987 21.491 12.2904 21.4527C12.3821 21.4143 12.4653 21.3581 12.535 21.2873C12.6048 21.2165 12.6598 21.1326 12.6969 21.0404C12.734 20.9481 12.7523 20.8495 12.7509 20.7501V12.7501H20.7509C20.8503 12.7515 20.949 12.7331 21.0412 12.6961C21.1334 12.659 21.2174 12.604 21.2881 12.5342C21.3589 12.4644 21.4151 12.3813 21.4535 12.2896C21.4918 12.1979 21.5116 12.0995 21.5116 12.0001C21.5116 11.9007 21.4918 11.8023 21.4535 11.7106C21.4151 11.6189 21.3589 11.5358 21.2881 11.466C21.2174 11.3962 21.1334 11.3412 21.0412 11.3041C20.949 11.267 20.8503 11.2487 20.7509 11.2501H12.7509V3.25009C12.7524 3.14971 12.7336 3.05006 12.6958 2.95705C12.6581 2.86403 12.602 2.77955 12.531 2.70861C12.4599 2.63767 12.3754 2.5817 12.2823 2.54404C12.1893 2.50638 12.0896 2.48778 11.9892 2.48935Z"))
                                            .Fill(StaticResources.Brushes.ForegroundBrush)
                                    )
                                : new Button() // COLOR ITEM
                                    .Background(itemVm.ToBrush())
                                    .BorderThickness(1)
                                    .BorderBrush(Avalonia.Media.Colors.White.WithAlpha(0.3f).ToBrush().ToImmutable())
                                    .OnClick(_ => OnColorItemClicked(itemVm))
                                    .Width(32)
                                    .Height(32)
                                    .CornerRadius(32)
                                    .Margin(6)
                                    .ContextFlyout(
                                        new MenuFlyout()
                                            .Placement(PlacementMode.Bottom)
                                            .ItemsSource(new[]{
                                                new MenuItem()
                                                    .Header("Delete color")
                                                    .OnClick(_ => ColorRemoved?.Invoke(itemVm))
                                                })
                                    )
                        )
                    )

            );

    private ItemsControl _itemsControl = null!;

    private IList<SKColor>? _sourceColorsCollection;

    private void OnAddColorClicked(RoutedEventArgs obj) => ColorAdded?.Invoke(ColorToAdd);

    private void OnColorItemClicked(SKColor newColor) => ColorSelected?.Invoke(newColor);

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

    private void Obs_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        _itemsControl.ItemsSource = GetCurrentColors();
    }

    private IEnumerable GetCurrentColors()
    {
        var colors = new List<SKColor>();

        if (_sourceColorsCollection == null)
            return colors;

        colors.AddRange(_sourceColorsCollection);
        colors.Add(SKColor.Empty);
        return colors;
    }

}