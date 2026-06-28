using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Token.Interfaces;
using Newtonsoft.Json;
using System.Text;

namespace BackendServices
{
  public class DeviceService : IDeviceService
  {
    private readonly ITuyaTokenService _tokenService;
    private readonly IConfigurationService _configurationService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IConfigurationService configurationService,
        ITuyaTokenService tokenService,
        HttpClient httpClient,
        ILogger<DeviceService> logger)
    {
      _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
      _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
      _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
      _httpClient.BaseAddress = new Uri(configurationService.TuyaEndpoint);
      _logger = logger;

      _logger.LogInformation("ℹ️ Initializing DeviceService");
    }

    public async Task<T> GetDeviceData<T>(string deviceId, string url)
    {
      _logger.LogInformation("ℹ️ GetDeviceData called");

      var tokenResponse = await _tokenService.GetToken();
      var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

      _logger.LogInformation("ℹ️ Getting signature...");

      var signature = _tokenService.GenerateProductSignature(
          _configurationService.TuyaClientId,
          _configurationService.TuyaClientSecret,
          timestamp,
          url,
          _tokenService.CachedToken.result.access_token
      );

      _logger.LogInformation("ℹ️ Setting request");

      var request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.Add("client_id", _configurationService.TuyaClientId);
      request.Headers.Add("sign", signature);
      request.Headers.Add("t", timestamp);
      request.Headers.Add("sign_method", "HMAC-SHA256");

      _logger.LogInformation("ℹ️ Calling _httpClient.SendAsync...");

      var response = await _httpClient.SendAsync(request);
      if (!response.IsSuccessStatusCode)
      {
        _logger.LogInformation("❌ Failed to get data from SendAsync");
        return default;
      }

      var content = await response.Content.ReadAsStringAsync();

      var deserializedResponse = JsonConvert.DeserializeObject<T>(content);

      _logger.LogInformation("ℹ️ Deserialized content");

      return deserializedResponse;
    }

    public async Task<T> SendDeviceAction<T>(string deviceId, string url, object jsonPayload)
    {
      _logger.LogInformation("ℹ️ SendDeviceAction called");

      // Ensure token is available and valid
      var token = await _tokenService.GetToken();
      if (token == null || token.result == null || string.IsNullOrEmpty(token.result.access_token))
      {
        throw new InvalidOperationException("No valid Tuya token available.");
      }

      var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

      string jsonString = JsonConvert.SerializeObject(jsonPayload);
      var signature = _tokenService.GeneratePostProductSignature(
          _configurationService.TuyaClientId,
          _configurationService.TuyaClientSecret,
          timestamp,
          url,
          _tokenService.CachedToken.result.access_token,
          jsonString
      );

      _logger.LogInformation("ℹ️ Generated signature");

      var statusRequest = new HttpRequestMessage(HttpMethod.Post, url)
      {
        Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
      };

      statusRequest.Headers.Add("client_id", _configurationService.TuyaClientId);
      statusRequest.Headers.Add("sign", signature);
      statusRequest.Headers.Add("t", timestamp);
      statusRequest.Headers.Add("sign_method", "HMAC-SHA256");

      _logger.LogInformation("ℹ️ statusRequest has been set");

      var statusResponse = await _httpClient.SendAsync(statusRequest);
      var deviceStatusJson = JsonConvert.SerializeObject(statusResponse);

      _logger.LogInformation("ℹ️ _httpClient.SendAsync called");

      return JsonConvert.DeserializeObject<T>(deviceStatusJson);
    }

  }
}