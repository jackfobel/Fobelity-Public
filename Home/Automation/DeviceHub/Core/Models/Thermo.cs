using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Api
{
  public sealed class Thermo(string id, string name, string loc, string brand, string model) : IDeviceThermostat
  {
    private ThermostatStatus _cur = new("cool", 78, null, 80, 55, true, DateTimeOffset.UtcNow);
    public string Id => id; public string Name => name; public string Location => loc; public string Brand => brand; public string Model => model;
    public Task<ThermostatStatus> GetStatusAsync(CancellationToken _) => Task.FromResult(_cur);
    public Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken _)
    {
      _cur = _cur with
      {
        Mode = req.Mode ?? _cur.Mode,
        SetpointCoolF = req.SetpointCoolF ?? _cur.SetpointCoolF,
        SetpointHeatF = req.SetpointHeatF ?? _cur.SetpointHeatF,
        LastUpdatedUtc = DateTimeOffset.UtcNow
      };
      return Task.FromResult(_cur);
    }
  }
}
