using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions
{
  public interface IDeviceSensor
  {
    Task<SensorStatus> GetStatusAsync(CancellationToken ct = default);
  }
}
