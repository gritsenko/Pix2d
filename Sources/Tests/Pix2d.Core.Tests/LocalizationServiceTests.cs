using Moq;
using Newtonsoft.Json;
using Pix2d.Abstract.Services;
using Pix2d.Common;
using Pix2d.State;

namespace Pix2d.Core.Tests;

public class LocalizationServiceTests
{
    [Fact]
    public void GettingTranslation_WithExoticLocale_DoesNotThrowAndReturnsKey()
    {
        // Arrange
        var appState = new AppState();
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Get<string>("locale")).Returns("zz"); // "zz" is an exotic/unsupported locale

        // Prepare a minimal strings.json in memory with only "en" locale
        var localizationDictionaries = new List<LocalizationDictionary>
        {
            new()
            {
                Locale = "en",
                Strings = new Dictionary<string, string>
                {
                    { "Hello", "Hello" }
                }
            }
        };
        var json = JsonConvert.SerializeObject(localizationDictionaries);
        // Act
        var service = new LocalizationService(appState, settingsService.Object, () => json);
        var value = service["Hello"];
        var missingValue = service["NonExistentKey"];

        // Assert
        Assert.Equal("Hello", value);
        Assert.Equal("NonExistentKey", missingValue); // Should return key if not found
    }
}