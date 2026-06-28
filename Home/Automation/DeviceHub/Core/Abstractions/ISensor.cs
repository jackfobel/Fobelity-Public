namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions;

using Fobelity.Home.Automation.DeviceHub.Core.Models;

public interface ISensor
{
  string Id { get; }
  string Name { get; }
  string Location { get; }

  Task<SensorStatus> GetStatusAsync(CancellationToken ct = default);
}
