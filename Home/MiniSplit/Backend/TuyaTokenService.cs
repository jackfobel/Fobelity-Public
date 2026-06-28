using DomainModels.Configuration.Interfaces;
using DomainModels.Token.Interfaces;
using DomainModels.Token.Models;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace BackendServices
{
  public class TuyaTokenService : ITuyaTokenService
  {
    public TuyaTokenResponse CachedToken { get; private set; }
    private readonly ILogger<TuyaTokenService> _logger;

    private readonly IConfigurationService _configurationService;
    private DateTime _tokenExpiration;

    public TuyaTokenService(IConfigurationService configurationService, ILogger<TuyaTokenService> logger)
    {
      _configurationService = configurationService;
      _logger = logger;
    }

    public async Task<TuyaTokenResponse> GetToken()
    {
      if (CachedToken != null && DateTime.UtcNow < _tokenExpiration)
      {
        _logger.LogDebug("Returning cached Tuya token.");
        return CachedToken;
      }

      var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
      var url = "/v1.0/token?grant_type=1";

      try
      {
        using var client = new HttpClient { BaseAddress = new Uri(_configurationService.TuyaEndpoint) };

        var clientId = _configurationService.TuyaClientId;
        var secret = _configurationService.TuyaClientSecret?.Trim();
        var tokenUrl = _configurationService.TokenUrl;

        var signature = GenerateSignature(clientId, secret, timestamp, tokenUrl);

        //// Debug Logging
        //_logger.LogInformation("🧪 TuyaClientId: {0}", clientId);
        //_logger.LogInformation("🧪 TokenUrl: {0}", tokenUrl);
        //_logger.LogInformation("🧪 Timestamp: {0}", timestamp);

        //var stringToSign = $"GET\ne3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855\n\n{tokenUrl}";
        //var fullStringToSign = $"{clientId}{timestamp}{stringToSign}";

        //_logger.LogInformation("🧪 StringToSign (escaped): {0}", stringToSign.Replace("\n", "\\n"));
        //_logger.LogInformation("🧪 Raw bytes of StringToSign: {0}", BitConverter.ToString(Encoding.UTF8.GetBytes(stringToSign)));
        //_logger.LogInformation("🧪 StringToSign: {0}", fullStringToSign);
        //_logger.LogInformation("🧪 Signature: {0}", signature);

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("client_id", clientId);
        request.Headers.Add("sign", signature);
        request.Headers.Add("t", timestamp);
        request.Headers.Add("sign_method", "HMAC-SHA256");

        _logger.LogInformation("Requesting Tuya token from {Url}", url);

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        //_logger.LogInformation("🟡 Raw Tuya token response: {Content}", responseContent);

        if (!response.IsSuccessStatusCode)
        {
          _logger.LogError("Failed to retrieve Tuya token. Status: {Status}, Body: {Body}",
              response.StatusCode, responseContent);
          throw new InvalidOperationException($"Token request failed with status {response.StatusCode}");
        }

        CachedToken = JsonConvert.DeserializeObject<TuyaTokenResponse>(responseContent);

        //if (CachedToken?.result?.expire_time == null)
        //{
        //  //_logger.LogError("Tuya token response was invalid or incomplete. Parsed object: {@Token}", CachedToken);
        //  string content = await response.Content.ReadAsStringAsync();
        //  _logger.LogError("Tuya token response content: {Content}", content);

        //  throw new InvalidOperationException("Failed to parse token response from Tuya.");
        //}

        _tokenExpiration = DateTime.UtcNow.AddSeconds(CachedToken.result.expire_time);
        _logger.LogInformation("Tuya token acquired successfully. Expires at {Expiration}", _tokenExpiration);

        return CachedToken;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unhandled exception during Tuya token retrieval.");
        throw;
      }
    }


    public string GenerateSignature(string clientId, string secret, string timestamp, string url)
    {
      string sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
      var stringToSign = $"GET\n{sha256Hash}\n\n{url}";
      var str = $"{clientId}{timestamp}{stringToSign}";

      var secretBytes = Encoding.UTF8.GetBytes(secret?.Trim() ?? string.Empty);
      var stringBytes = Encoding.UTF8.GetBytes(str);

      using var hmac = new HMACSHA256(secretBytes);
      var hash = hmac.ComputeHash(stringBytes);
      return BitConverter.ToString(hash).Replace("-", "").ToUpper();
    }


    public string GenerateProductSignature(string clientId, string secret, string timestamp, string url, string accessToken)
    {
      string sha256Hash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
      var stringToSign = $"GET\n{sha256Hash}\n\n{url}";
      var str = $"{clientId}{accessToken}{timestamp}{stringToSign}";

      using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
      var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(str));
      return BitConverter.ToString(hash).Replace("-", "").ToUpper();
    }

    public string GeneratePostProductSignature(string clientId, string secret, string timestamp, string url, string accessToken, string payload)
    {
      string sha256Hash = SHA256_Encrypt(payload);
      var stringToSign = $"POST\n{sha256Hash}\n\n{url}";
      var str = $"{clientId}{accessToken}{timestamp}{stringToSign}";

      using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
      var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(str));
      return BitConverter.ToString(hash).Replace("-", "").ToUpper();
    }

    private string SHA256_Encrypt(string payload)
    {
      using var sha256Hash = SHA256.Create();
      byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(payload));
      return BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
  }
}
