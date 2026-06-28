using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Core.Models
{
  // Simple in-memory thermostat used for tests/demos.
  public sealed class InMemoryThermostat : IDeviceThermostat
  {
    private readonly object _lock = new();
    private ThermostatStatus _state;

    // Optional identifiers (keep only if you use them elsewhere)
    public string Id { get; }
    public string Name { get; }
    public string Location { get; }

    public InMemoryThermostat(string id, string name, string location)
    {
      Id = id; Name = name; Location = location;

      // ThermostatStatus is a record -> use positional ctor
      _state = new ThermostatStatus(
        Mode: "cool",
        SetpointCoolF: 78,
        SetpointHeatF: null,
        InsideTempF: 80,
        HumidityPct: 55,
        IsOn: true,
        LastUpdatedUtc: DateTimeOffset.UtcNow
      );
    }

    public Task<ThermostatStatus> GetStatusAsync(CancellationToken ct = default)
      => Task.FromResult(_state);

    public Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct = default)
    {
      lock (_lock)
      {
        var next = _state;

        if (!string.IsNullOrWhiteSpace(req.Mode))
          next = next with { Mode = req.Mode };

        if (req.SetpointCoolF.HasValue)
          next = next with { SetpointCoolF = req.SetpointCoolF };

        if (req.SetpointHeatF.HasValue)
          next = next with { SetpointHeatF = req.SetpointHeatF };

        next = next with { LastUpdatedUtc = DateTimeOffset.UtcNow };

        _state = next;
        return Task.FromResult(_state);
      }
    }
  }
}
