using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions
{
  public interface IThermostatProvider
  {
    Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default);
    Task<IDeviceThermostat?> GetAsync(string id, CancellationToken ct = default);
  }
}
