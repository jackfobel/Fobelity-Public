using DomainModels.Configuration.Interfaces;
using DomainModels.Weather;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Net.Http;

namespace BackendServices
{
  public class WeatherService : IWeatherService
  {
    private HttpClient? _httpClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WeatherService> _logger;
    private readonly IConfigurationService _configurationService;

    public WeatherService(
      IHttpClientFactory httpClientFactory,
      ILogger<WeatherService> logger,
      IConfigurationService configurationService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _configurationService = configurationService;

    }

    public async Task<WeatherModel> GetCurrentTemperature()
    {
      _httpClient = _httpClientFactory.CreateClient();
      var weatherApiUrl = _configurationService.WeatherServiceUrl;

      if (string.IsNullOrWhiteSpace(weatherApiUrl))
      {
        throw new InvalidOperationException("WeatherServiceUrl is not configured.");
      }


      var response = await _httpClient.GetAsync(weatherApiUrl);

      if (!response.IsSuccessStatusCode)
      {
        throw new Exception("Failed to fetch temperature from weather API.");
      }

      var content = await response.Content.ReadAsStringAsync();
      var weatherData = JsonConvert.DeserializeObject<WeatherModel>(content);


      return weatherData;

    }




  }



}
