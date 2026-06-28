using DomainModels.Device.Models;
using DomainModels.Storage.Models;
using DomainModels.Weather;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Token.Interfaces
{
  public interface IApiClient
  {
    Task<DeviceStatus?> GetMiniSplitStatusAsync(string bearerToken);
    Task<DeviceDetails?> GetMiniSplitDetailsAsync(string? bearerToken = null);
    Task<MiniSplitConfigData?> GetMiniSplitConfigDataAsync(string? bearerToken = null);
    Task UpdateMiniSplitConfigAsync(MiniSplitConfigData configData, string? bearerToken = null);
    Task<WeatherModel?> GetWeatherDataAsync(string? bearerToken = null);
    Task<DeviceStatus?> TurnOnMiniSplit(string? bearerToken = null);
    Task<DeviceStatus?> TurnOffMiniSplit(string? bearerToken = null);
    Task<string?> AutomateAsync(WeatherModel model, string? bearerToken = null);
  }
}
