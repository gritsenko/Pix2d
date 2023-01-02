using System;

namespace Pix2d.Abstract.UI
{
    public interface IDialogView
    {
        string Title { get; set; }
        Action<bool?> OnDialogClosed { get; set; }
    }
}