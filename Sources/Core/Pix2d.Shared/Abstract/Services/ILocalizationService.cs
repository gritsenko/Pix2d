namespace Pix2d.Abstract.Services;

public record LocaleInfo(string Code, string Title, string TitleNative)
{
    public string FullTitle => $"{TitleNative} ({Title})";
}

public interface ILocalizationService
{
    string this[string name] { get; }

    public IReadOnlyList<LocaleInfo> AvailableLocales { get; }
    public void SetLocale(string locale);

    event Action? LocaleChanged;
}