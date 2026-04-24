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
            .Margin(8)
            .Children(
                new ToggleSwitch()
                    .OnContent("Erase mode: On")
                    .OffContent("Erase mode: Off")
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

        public void SetTool(FillTool? tool)
        {
            _tool = tool;

            _isSyncing = true;
            EraseMode = tool?.EraseMode ?? false;
            _isSyncing = false;
        }

        partial void OnEraseModeChanged(bool value)
        {
            if (_isSyncing || _tool == null)
                return;

            _tool.EraseMode = value;
        }
    }
}
