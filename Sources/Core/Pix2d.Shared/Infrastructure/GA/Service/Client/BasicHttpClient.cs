using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Pix2d.Infrastructure.GA.Extensions;
using Pix2d.Infrastructure.GA.Service.Request;

namespace Pix2d.Infrastructure.GA.Service.Client;

public class BasicHttpClient: IBasicHttpClient
{
    private readonly GoogleAnalyticsClientSettings _settings;
    private readonly HttpClient _client;
    

    public BasicHttpClient(GoogleAnalyticsClientSettings settings)
    {
        _settings = settings;

        _client = new HttpClient();
        _client.BaseAddress = new Uri("https://www.google-analytics.com");
    }
    
    public async Task<HttpResponseMessage> PostAsync(string path, IEventRequest data)
    {
        var fullPath = $"{path}?measurement_id={_settings.MeasurementId}&api_secret={_settings.AppSecret}";
        var hold = JsonSerializer.Serialize(data);
        
        // Send Message to GateWay
        return await _client.PostAsJsonAsync(fullPath, data, JsonHelper.GetOptions());
    }
}