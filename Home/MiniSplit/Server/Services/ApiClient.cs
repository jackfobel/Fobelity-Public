using DomainModels.Device.Models;
using DomainModels.Storage.Models;
using DomainModels.Weather;
using Fobelity.Home.MiniSplit.Domain.Token.Interfaces;
using System.Net.Http.Headers;

public class ApiClient : IApiClient
{
  private readonly HttpClient _httpClient;

  public ApiClient(HttpClient httpClient)
  {
    _httpClient = httpClient;
  }

  public async Task<DeviceStatus?> GetMiniSplitStatusAsync(string bearerToken)
  {
    return await GetAsync<DeviceStatus>("api/minisplit/status", bearerToken);
  }

  public async Task<DeviceDetails?> GetMiniSplitDetailsAsync(string? bearerToken = null)
  {
    return await GetAsync<DeviceDetails>("api/minisplit/device-details", bearerToken);
  }

  public async Task<DeviceStatus?> TurnOnMiniSplit(string? bearerToken = null)
  {
    return await PostJsonAsync<object, DeviceStatus>("api/minisplit/turn-on", new { }, bearerToken);
  }

  public async Task<DeviceStatus?> TurnOffMiniSplit(string? bearerToken = null)
  {
    return await PostJsonAsync<object, DeviceStatus>("api/minisplit/turn-off", new { }, bearerToken);
  }

  public async Task<string?> AutomateAsync(WeatherModel model, string? bearerToken = null)
  {
    return await PostJsonAsync<WeatherModel, string>("api/minisplit/automate", model, bearerToken);
  }

  public async Task<MiniSplitConfigData?> GetMiniSplitConfigDataAsync(string? bearerToken = null)
  {
    var result = await GetAsync<MiniSplitConfigDataResponse>("api/minisplit/config-data", bearerToken);
    return result?.result;
  }

  public async Task<WeatherModel?> GetWeatherDataAsync(string? bearerToken = null)
  {
    return await GetAsync<WeatherModel>("api/minisplit/current-weather", bearerToken);
  }

  public async Task UpdateMiniSplitConfigAsync(MiniSplitConfigData configData, string bearerToken)
  {
    await PostJsonAsync("api/minisplit/update-config", configData, bearerToken);
  }


  // ---------- Private Generic Helpers ----------

  private async Task<T?> GetAsync<T>(string url, string? bearerToken = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, url);
    if (!string.IsNullOrEmpty(bearerToken))
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    var response = await _httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<T>();
  }

  private async Task<TResponse?> PostJsonAsync<TRequest, TResponse>(
  string url,
  TRequest data,
  string? bearerToken = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
      Content = JsonContent.Create(data)
    };

    if (!string.IsNullOrEmpty(bearerToken))
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    var response = await _httpClient.SendAsync(request);

    if (!response.IsSuccessStatusCode)
    {
      var errorBody = await response.Content.ReadAsStringAsync();
      throw new HttpRequestException(
        $"Request to {url} failed with status {response.StatusCode}. Body: {errorBody}");
    }

    try
    {
      return await response.Content.ReadFromJsonAsync<TResponse>();
    }
    catch (Exception ex)
    {
      var raw = await response.Content.ReadAsStringAsync();
      throw new InvalidOperationException(
        $"Failed to deserialize JSON response from {url} to {typeof(TResponse).Name}. " +
        $"Raw response: {raw}", ex);
    }
  }


  private async Task PostJsonAsync<TRequest>(string url, TRequest data, string? bearerToken = null)
  {
    var request = new HttpRequestMessage(HttpMethod.Post, url)
    {
      Content = JsonContent.Create(data)
    };

    if (!string.IsNullOrEmpty(bearerToken))
    {
      request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    await _httpClient.SendAsync(request);
    //return response.IsSuccessStatusCode;
  }



}
