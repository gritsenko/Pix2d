namespace Pix2d.Plugins.Sprite.ViewModels.Animation;

internal class ItemReorderInfo<TItem>
{
    public TItem[] Items { get; set; }

    public int OldIndex { get; set; }

    public int NewIndex { get; set; }
}