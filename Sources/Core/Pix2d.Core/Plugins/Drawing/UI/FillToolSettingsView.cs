using Avalonia.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Pix2d.Plugins.Drawing.Tools;

namespace Pix2d.Plugins.Drawing.UI;

public partial class FillToolSettingsView() : ViewBase<FillToolSettingsView.State>(new State())
{
    protected override object Build(State state) =>
        new StackPanel()
            .Margin(8)
            .Children(
                new ToggleSwitch()
                    .OnContent("Erase mode: On")
                    .OffContent("Erase mode: Off")
                    .IsChecked(state, x => x.EraseMode, BindingMode.TwoWay)
            );

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == DataContextProperty)
            ViewModel?.SetTool(change.NewValue as FillTool);
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