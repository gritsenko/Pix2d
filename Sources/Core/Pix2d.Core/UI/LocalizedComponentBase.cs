using System.Diagnostics.CodeAnalysis;
using Avalonia.LogicalTree;

namespace Pix2d.UI.Shared;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties |
                            DynamicallyAccessedMemberTypes.NonPublicProperties |
                            DynamicallyAccessedMemberTypes.NonPublicFields)]
public abstract class LocalizedComponentBase : ComponentBase
{
    [Inject] private ILocalizationService LocalizationService { get; set; } = null!;
    [Inject] private AppState AppState { get; set; }

    /// <summary>
    /// Used for localization strings, override this in your own base component inherited from this ComponentBase
    /// </summary>
    /// <param name="inputString">String in original language</param>
    /// <returns>Translated string</returns>
    protected virtual Func<string> L(string? inputString)
    {
        if (inputString == null)
            return _empty;
        return () => LocalizationService[inputString];
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        AppState.WatchFor(x => x.Locale, OnLocaleChanged);
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        AppState.Unwatch(x => x.Locale, OnLocaleChanged);
    }

    protected virtual void OnLocaleChanged()
    {
        StateHasChanged();
    }

    private static string _empty() => "";
}

public abstract class LocalizedComponentBase<TViewModel>(TViewModel viewModel) : ComponentBase<TViewModel>(viewModel)
{
    [Inject] private ILocalizationService LocalizationService { get; set; } = null!;
    [Inject] private AppState AppState { get; set; }

    /// <summary>
    /// Used for localization strings, override this in your own base component inherited from this ComponentBase
    /// </summary>
    /// <param name="inputString">String in original language</param>
    /// <returns>Translated string</returns>
    protected virtual Func<string> L(string? inputString)
    {
        if (inputString == null)
            return _empty;
        return () => LocalizationService[inputString];
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        AppState.WatchFor(x => x.Locale, OnLocaleChanged);
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        AppState.Unwatch(x => x.Locale, OnLocaleChanged);
    }

    protected virtual void OnLocaleChanged()
    {
        StateHasChanged();
    }

    private static string _empty() => "";
}