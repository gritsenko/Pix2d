using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Abstract.Export;
using Pix2d.Export.Sheet;
using Pix2d.Export.Sheet.Metadata;
using Pix2d.Plugins.PngFormat.Exporters;

namespace Pix2d.UI.Export;

public partial class SpriteSheetExportSettingsView()
    : ViewBase<SpriteSheetExportSettingsView.State>(new State()), IExportSettingsViewBase<SpriteSheetExporter>
{
    protected override object Build(State state) =>
        new StackPanel()
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Spacing(6)
            .Children(
                new TextBlock().Text(L("Packing")),
                new ComboBox()
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .Items(
                        new ComboBoxItem().Content(L("Grid")),
                        new ComboBoxItem().Content(L("Tight"))
                    )
                    .SelectedIndex(state, x => x.PackModeIndex, BindingMode.TwoWay),

                new TextBlock().Text(L("Max columns")),
                new NumericUpDown()
                    .PlaceholderText(L("Columns count"))
                    .Minimum(1)
                    .NumberFormat(new NumberFormatInfo { NumberDecimalDigits = 0 })
                    .Increment(1)
                    .Value(state, x => x.MaxColumns, BindingMode.TwoWay),

                new TextBlock().Text(L("Spacing (px)")),
                new NumericUpDown()
                    .Minimum(0)
                    .NumberFormat(new NumberFormatInfo { NumberDecimalDigits = 0 })
                    .Increment(1)
                    .Value(state, x => x.Padding, BindingMode.TwoWay),

                new ToggleSwitch()
                    .Content(L("Trim transparent borders"))
                    .IsChecked(state, x => x.Trim, BindingMode.TwoWay),

                new ToggleSwitch()
                    .Content(L("Power-of-two size"))
                    .IsChecked(state, x => x.PowerOfTwo, BindingMode.TwoWay),

                new TextBlock().Margin(0, 8, 0, 0).Text(L("Metadata (.json sidecar)")),
                new ComboBox()
                    .HorizontalAlignment(HorizontalAlignment.Stretch)
                    .ItemsSource(state.MetadataFormatNames)
                    .SelectedIndex(state, x => x.MetadataFormatIndex, BindingMode.TwoWay)
            );

    private SpriteSheetExporter _exporter = null!;

    public SpriteSheetExporter Exporter
    {
        get => _exporter;
        set
        {
            _exporter = value;
            ViewModel?.SetExporter(value);
        }
    }

    /// <summary>Raised whenever an option is changed so the host export dialog can refresh preview/output info.</summary>
    public event Action? SettingsChanged
    {
        add => ViewModel!.SettingsChanged += value;
        remove => ViewModel!.SettingsChanged -= value;
    }

    public sealed partial class State : ObservableObject
    {
        private SpriteSheetExporter? _exporter;

        /// <summary>Fired after any option is pushed onto the exporter instance.</summary>
        public event Action? SettingsChanged;

        // Index 0 = "None (image only)"; the rest come from the emitter registry.
        private readonly List<string> _metadataIds = ["none", .. SheetMetadataEmitters.All.Select(e => e.Id)];

        public List<string> MetadataFormatNames { get; } =
            [L("None (image only)"), .. SheetMetadataEmitters.All.Select(e => e.DisplayName)];

        [ObservableProperty] public partial int PackModeIndex { get; set; }
        [ObservableProperty] public partial decimal MaxColumns { get; set; } = 4;
        [ObservableProperty] public partial decimal Padding { get; set; }
        [ObservableProperty] public partial bool Trim { get; set; }
        [ObservableProperty] public partial bool PowerOfTwo { get; set; }
        [ObservableProperty] public partial int MetadataFormatIndex { get; set; }

        public void SetExporter(SpriteSheetExporter exporter)
        {
            _exporter = exporter;
            PackModeIndex = exporter.PackMode == SheetPackMode.Tight ? 1 : 0;
            MaxColumns = exporter.MaxColumns;
            Padding = exporter.Padding;
            Trim = exporter.Trim;
            PowerOfTwo = exporter.PowerOfTwo;
            var idx = _metadataIds.IndexOf(exporter.MetadataFormat);
            MetadataFormatIndex = idx >= 0 ? idx : 0;
        }

        partial void OnPackModeIndexChanged(int value)
        {
            if (_exporter == null)
                return;
            _exporter.PackMode = value == 1 ? SheetPackMode.Tight : SheetPackMode.Grid;
            SettingsChanged?.Invoke();
        }

        partial void OnMaxColumnsChanged(decimal value)
        {
            if (_exporter == null)
                return;
            _exporter.MaxColumns = (int)value;
            SettingsChanged?.Invoke();
        }

        partial void OnPaddingChanged(decimal value)
        {
            if (_exporter == null)
                return;
            _exporter.Padding = (int)value;
            SettingsChanged?.Invoke();
        }

        partial void OnTrimChanged(bool value)
        {
            if (_exporter == null)
                return;
            _exporter.Trim = value;
            SettingsChanged?.Invoke();
        }

        partial void OnPowerOfTwoChanged(bool value)
        {
            if (_exporter == null)
                return;
            _exporter.PowerOfTwo = value;
            SettingsChanged?.Invoke();
        }

        partial void OnMetadataFormatIndexChanged(int value)
        {
            if (_exporter != null && value >= 0 && value < _metadataIds.Count)
            {
                _exporter.MetadataFormat = _metadataIds[value];
                SettingsChanged?.Invoke();
            }
        }
    }
}
