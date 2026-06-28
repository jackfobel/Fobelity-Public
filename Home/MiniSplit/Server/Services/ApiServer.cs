using DomainModels.Device.Models;
using Microsoft.Identity.Web;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Fobelity.Home.MiniSplit.Server.Services
{

  public class ApiServer : IApiServer
  {
    private readonly HttpClient _httpClient;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger<ApiServer> _logger;

    public ApiServer(HttpClient httpClient, ITokenAcquisition tokenAcquisition, ILogger<ApiServer> logger)
    {
      _httpClient = httpClient;
      _tokenAcquisition = tokenAcquisition;
      _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string url)
    {
      var accessToken = await _tokenAcquisition.GetAccessTokenForAppAsync(
        "https://sanitized.redacted.com/controlsvcapi/.default");

      ////////Console.WriteLine($"--------------------> accessToken: {accessToken}");

      _httpClient.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", accessToken);

      var response = await _httpClient.GetAsync(url);

      _logger.LogInformation("ApiServer - GET {Url} => {StatusCode}", url, response.StatusCode);

      response.EnsureSuccessStatusCode();
      return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<DeviceStatus?> GetMiniSplitStatusAsync()
    {
      return await GetAsync<DeviceStatus>("https://YOUR-CONTROL-SERVICE.example.com/api/minisplit/status");
    }
  }
}
