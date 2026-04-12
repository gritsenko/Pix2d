namespace Pix2d.UI;

public static class LocalizationHelper
{
    private static readonly Func<string> Empty = static () => string.Empty;
    private static ILocalizationService? _localizationService;

    public static void Initialize(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public static Func<string> L(string? inputString)
    {
        if (inputString == null)
            return Empty;

        return () => _localizationService?[inputString] ?? inputString;
    }
}