namespace DomainModels.Configuration.Interfaces
{
  public interface IConfigurationService
  {
    bool DebugMode { get; }

    string TuyaClientId { get; }
    string TuyaClientSecret { get; }
    string TuyaEndpoint { get; }
    string IoTDeviceId { get; }
    string Timestamp { get; }
    string TokenUrl { get; }
    string StorageConnString { get; }
    string WeatherServiceUrl { get; }
  }
}