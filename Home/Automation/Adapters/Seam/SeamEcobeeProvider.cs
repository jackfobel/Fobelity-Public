using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;
using Microsoft.Extensions.Configuration;

namespace Fobelity.Home.Automation.Adapters.Seam;

public sealed class SeamEcobeeProvider : IThermostatProvider
{
  private readonly EcobeeThermostatAdapter _adapter;
  private readonly IConfiguration _cfg;

  // Stable logical device record (what /devices returns)
  private static readonly Device _home =
      new("home", "Home Ecobee", "home", "Ecobee", "SmartThermostat", new[] { "Thermostat" });

  public SeamEcobeeProvider(EcobeeThermostatAdapter adapter, IConfiguration cfg)
  {
    _adapter = adapter;
    _cfg = cfg;
  }

  public Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default)
      => Task.FromResult<IReadOnlyList<Device>>(new[] { _home });

  public Task<IDeviceThermostat?> GetAsync(string id, CancellationToken ct = default)
  {
    if (!id.Equals("home", StringComparison.OrdinalIgnoreCase))
      return Task.FromResult<IDeviceThermostat?>(null);

    // If Seam isn’t configured yet, return a harmless offline shim
    var seamKey = _cfg["SEAM_API_KEY"];
    var deviceId = _cfg["ECOBEE_DEVICE_ID"];  // Seam device_id (required for the real adapter)

    if (string.IsNullOrWhiteSpace(seamKey) || string.IsNullOrWhiteSpace(deviceId))
      return Task.FromResult<IDeviceThermostat?>(new OfflineThermostat());

    // Otherwise use the real adapter (constructed by DI with the device_id)
    return Task.FromResult<IDeviceThermostat?>(_adapter);
  }
}

// Minimal offline shim so /status doesn’t explode before Seam is wired
file sealed class OfflineThermostat : IDeviceThermostat
{
  public Task<ThermostatStatus> GetStatusAsync(CancellationToken ct) =>
    Task.FromResult(new ThermostatStatus(
      Mode: "off",
      SetpointCoolF: null,
      SetpointHeatF: null,
      InsideTempF: null,
      HumidityPct: null,
      IsOn: false,
      LastUpdatedUtc: DateTimeOffset.UtcNow));

  public Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct) =>
    GetStatusAsync(ct);
}
