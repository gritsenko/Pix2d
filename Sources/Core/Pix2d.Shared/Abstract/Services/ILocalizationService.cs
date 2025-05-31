namespace Pix2d.Abstract.Services;

public interface ILocalizationService
{
    string this[string name] { get; }

    public void SetLocale(string locale);
}