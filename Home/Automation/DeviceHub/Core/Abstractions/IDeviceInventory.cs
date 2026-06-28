using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions;

public interface IDeviceInventory
{
  Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default);
  Task<IDeviceThermostat?> GetThermostatAsync(string id, CancellationToken ct = default);
  Task<IDeviceSensor?> GetSensorAsync(string id, CancellationToken ct = default);
}

