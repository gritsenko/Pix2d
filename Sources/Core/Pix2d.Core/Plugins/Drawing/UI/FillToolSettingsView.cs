using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Plugins.Drawing.Tools;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.Drawing.UI;

public partial class FillToolSettingsView : ViewBase
{
    private readonly State _state = new();

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .VerticalAlignment(VerticalAlignment.Center)
            .Margin(8)
            .Spacing(12)
            .Children(
                new SliderEx()
                    .Width(168)
                    .Label(L("Opacity"))
                    .Units("%")
                    .Minimum(1)
                    .Maximum(100)
                    .Value(_state, x => x.Opacity, BindingMode.TwoWay),
                new ToggleSwitch()
                    .VerticalAlignment(VerticalAlignment.Center)
                    .OnContent(L("Erase mode: On"))
                    .OffContent(L("Erase mode: Off"))
                    .IsChecked(_state, x => x.EraseMode, BindingMode.TwoWay)
            );

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
            _state.SetTool(change.NewValue as FillTool);
    }

    public sealed partial class State : ObservableObject
    {
        private FillTool? _tool;
        private bool _isSyncing;

        [ObservableProperty]
        public partial bool EraseMode { get; set; }

        [ObservableProperty]
        public partial double Opacity { get; set; } = 100;

        public void SetTool(FillTool? tool)
        {
            _tool = tool;

            _isSyncing = true;
            EraseMode = tool?.EraseMode ?? false;
            Opacity = tool?.Opacity ?? 100;
            _isSyncing = false;
        }

        partial void OnEraseModeChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.EraseMode = value;
        }

        partial void OnOpacityChanged(double value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.Opacity = value;
        }
    }
}
