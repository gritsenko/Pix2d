using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Declarative;
using Pix2d.UI.Shared;

namespace Pix2d.Plugins.Ai.UI;

public class ImageGenerateSettingsView : LocalizedComponentBase
{

    protected override object Build() =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Children(
                new TextBox()
                    .Watermark(L("Enter text"))
                    .Text(Bind(Text))
                    .VerticalAlignment(VerticalAlignment.Center)
                    .AcceptsReturn(false)
                    .MinWidth(150),
                new Button()
                    .Content(L("Generate"))
            );

    public string Text { get; set; }


    private async void OnApplyButtonClicked()
    {
        string url = "https://aipixelartgenerator.com/wp-admin/admin-ajax.php";

        using (var httpClient = new HttpClient())
        {
            var postData = new Dictionary<string, string>()
            {
                { "action", "generate_pixel_art_image" },
                { "user-input", "minigun avanger" }
            };

            try
            {
                var response = await httpClient.PostAsync(url, new FormUrlEncodedContent(postData));
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response:");
                Console.WriteLine(responseBody);
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"HTTP Error: {ex.Message}");
            }
        }

        Console.ReadLine();

        Logger.Log("Generating image");
    }


}