namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions;

using Fobelity.Home.Automation.DeviceHub.Core.Models;

public interface IThermostat
{
  string Id { get; }
  string Name { get; }
  string Location { get; }

  Task<ThermostatStatus> GetStatusAsync(CancellationToken ct = default);
  Task<ThermostatStatus> SetAsync(ThermostatSetRequest request, CancellationToken ct = default);
}
