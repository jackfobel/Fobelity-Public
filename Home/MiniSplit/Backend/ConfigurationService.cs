using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DomainModels.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestSharp;
using System.Text.RegularExpressions;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace BackendServices
{
  public class ConfigurationService : IConfigurationService
  {
    public string TuyaClientId { get; }
    public string TuyaClientSecret { get; }
    public string TuyaEndpoint { get; }
    public string IoTDeviceId { get; }
    public string TokenUrl { get; }
    public string Timestamp { get; }
    public string StorageConnString { get; }
    public string WeatherServiceUrl { get; }
    public string ClientSecret { get; }
    public bool DebugMode { get; }

    private readonly ILogger<ConfigurationService> _logger;


    public ConfigurationService(IConfiguration config, ILogger<ConfigurationService> logger, string clientSecret)
    {
      _logger = logger;
      _logger.LogInformation("ℹ️ Initializing ConfigurationService");

      TuyaClientId = GetRequiredConfig(config, "clientId");
      TuyaClientSecret = clientSecret;
      ClientSecret = clientSecret;

      TuyaEndpoint = GetRequiredConfig(config, "endpoint");
      IoTDeviceId = GetRequiredConfig(config, "deviceId");
      TokenUrl = GetRequiredConfig(config, "tokenUrl");
      StorageConnString = GetRequiredConfig(config, "azureTableStorageConnString");
      WeatherServiceUrl = GetRequiredConfig(config, "WeatherServiceUrl");

      var debugMode = config["DebugMode"];
      DebugMode = bool.TryParse(debugMode, out var parsedDebug) && parsedDebug;

      Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

      _logger.LogInformation("✅ DebugMode: {DebugMode}", DebugMode);
    }


    private string GetRequiredConfig(IConfiguration config, string key)
    {
      var value = config[key];

      if (string.IsNullOrWhiteSpace(value))
      {
        _logger.LogError("❌ Configuration value for '{Key}' is missing or empty.", key);
        throw new InvalidOperationException($"Configuration value for '{key}' is missing or empty.");
      }

      //// Don't log secrets directly, but do confirm they are non-empty.
      //if (key.ToLowerInvariant().Contains("secret"))
      //{
      //  //logger.LogInformation("🔐 Configuration key '{Key}' is present (secret length: {Length})", key, value.Length);
      //}
      //else
      //{
      //  logger.LogInformation("✅ Configuration: {Key} = {Value}", key, value);
      //}

      return value;
    }
  }
}
