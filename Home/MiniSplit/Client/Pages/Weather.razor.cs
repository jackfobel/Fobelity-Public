using DomainModels.Weather;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Fobelity.Home.MiniSplit.Client.Services;
using System.Net.Http.Json;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Weather
  {
    public bool IsInitialized { get; private set; }
    public WeatherModel? weatherResponse { get; private set; }

    [Inject] public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] public IAntiforgeryHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] public ILogger<Weather> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
      if (IsInitialized)
        return;

      try
      {
        var client = await HttpClientFactory.CreateClientAsync();

        var response = await client.GetAsync("bff/minisplit/current-weather");
        if (response.IsSuccessStatusCode)
        {
          weatherResponse = await response.Content.ReadFromJsonAsync<WeatherModel>();
        }
        else
        {
          var body = await response.Content.ReadAsStringAsync();
          Logger.LogWarning("Weather API call failed: {StatusCode} - {Body}", response.StatusCode, body);
        }
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error retrieving current weather.");
      }

      IsInitialized = true;
    }
  }
}
