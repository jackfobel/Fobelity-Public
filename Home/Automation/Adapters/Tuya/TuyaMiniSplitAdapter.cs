using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.Adapters.Tuya;

public sealed class TuyaMiniSplitAdapter : IDeviceThermostat
{
  private readonly MiniSplitHttpClient _client;
  public TuyaMiniSplitAdapter(MiniSplitHttpClient client) => _client = client;

  public Task<ThermostatStatus> GetStatusAsync(CancellationToken ct) =>
      _client.GetStatusAsync(ct);

  public async Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct)
  {
    if (req.DryRun) return await GetStatusAsync(ct);

    // Preserve “off” and “on” fast paths if you like (optional)
    var m = req.Mode?.Trim().ToLowerInvariant();
    if (m is "off") return await _client.TurnOffAsync(ct);
    if (m is "on") return await _client.TurnOnAsync(ct);

    // Full capability path
    return await _client.SetAsync(req, ct);
  }
}

