using DomainModels.Device.Models;

namespace Fobelity.Home.MiniSplit.Server.Services
{
  public interface IApiServer
  {
    Task<T?> GetAsync<T>(string url);
    Task<DeviceStatus?> GetMiniSplitStatusAsync();
  }

}
