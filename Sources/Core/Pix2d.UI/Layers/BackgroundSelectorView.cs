using Pix2d.Common.Extensions;
using Pix2d.CommonNodes;
using Pix2d.UI.Shared;

namespace Pix2d.UI.Layers;

public class BackgroundSelectorView : LocalizedComponentBase
{
    protected override object Build() =>
        new Grid()
            .Children(
                new Button()
                    .Classes("color-button")
                    .Width(32)
                    .Height(32)
                    .CornerRadius(32)
                    .BorderThickness(1)
                    .BorderBrush(Colors.White.WithAlpha(0.3f).ToBrush().ToImmutable())
                    .Background(() => AppState.SpriteEditorState.ShowBackground ? AppState.SpriteEditorState.BackgroundColor.ToBrush() : Brushes.Transparent)
                    .Flyout(
                        new Flyout()
                            .Content(
                                new Grid()
                                    .Rows("Auto, Auto, Auto")
                                    .Children(
                                        new TextBlock().Text("Background"),
                                        new Pix2dColorPicker().Row(1)
                                            .Margin(10)
                                            .Color(() => AppState.SpriteEditorState.BackgroundColor,
                                                v =>
                                                {
                                                    UpdateState(() => AppState.SpriteEditorState.BackgroundColor = v);
                                                    UpdateSprite();
                                                })
                                            .Margin(0, 8)
                                            .Width(200)
                                            .Height(140),
                                        new ToggleSwitch().Row(2)
                                            .IsChecked(() => AppState.SpriteEditorState.ShowBackground,
                                                v =>
                                                {
                                                    UpdateState(() =>
                                                        AppState.SpriteEditorState.ShowBackground = v.Value);
                                                    UpdateSprite();
                                                })
                                            .Content(L("Show background"))
                                    )
                            )
                    )
            );

    [Inject] private AppState AppState { get; set; } = null!;
    [Inject] private IViewPortRefreshService ViewPortRefreshService { get; set; } = null!;

    private void UpdateSprite()
    {
        if (AppState.CurrentProject.CurrentEditedNode is not Pix2dSprite sprite) return;

        sprite.BackgroundColor = AppState.SpriteEditorState.BackgroundColor;
        sprite.UseBackgroundColor = AppState.SpriteEditorState.ShowBackground;
        ViewPortRefreshService.Refresh();
    }
}