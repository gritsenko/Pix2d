using System.Collections.Generic;
using System.Linq;

namespace Pix2d.UI.MainMenu;

public class KeyShortcutsView : ComponentBase
{
    [Inject] ICommandService CommandService { get; set; } = null!;
    protected override object Build() {
        var entries = new List<TextBlock>();
        var rowDefs = new RowDefinitions();
        var row = 0;
        foreach (var command in CommandService.GetCommands().Where(c => c.DefaultShortcut != null))
        {
            rowDefs.Add(new RowDefinition(new GridLength(24)));
            entries.Add(new TextBlock().Text(command.Description).Row(row));
            entries.Add(new TextBlock().Text(command.GetShortcutString()).Row(row).Col(1));
            row++;
        }

        return new StackPanel().Children(
            new TextBlock().Text("Keyboard shortcuts:").Margin(0, 0, 0, 16).FontSize(20),
            new Grid().Rows(rowDefs).Cols("*,auto").Children(entries.ToArray()));
    }
}