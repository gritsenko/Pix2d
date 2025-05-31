using System;

namespace Pix2d.Abstract.UI;

public interface IDialogView<out TResult> : IDialogView
{
    TResult DialogResult { get; }
}

public interface IDialogView
{
    string Title { get; set; }
    Action<bool?> OnDialogClosed { get; set; }
}