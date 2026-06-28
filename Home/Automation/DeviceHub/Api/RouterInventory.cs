// DeviceHub.Api/RouterInventory.cs
using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Api;

public sealed class RouterInventory : IDeviceInventory
{
  private readonly IReadOnlyList<IThermostatProvider> _providers;

  public RouterInventory(IEnumerable<IThermostatProvider> providers)
    => _providers = providers.ToList();

  public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default)
  {
    var chunks = await Task.WhenAll(_providers.Select(p => p.ListAsync(ct)));
    return chunks.SelectMany(x => x).ToList();
  }

  public async Task<IDeviceThermostat?> GetThermostatAsync(string id, CancellationToken ct = default)
  {
    foreach (var p in _providers)
    {
      var t = await p.GetAsync(id, ct);
      if (t is not null) return t;
    }
    return null;
  }

  public Task<IDeviceSensor?> GetSensorAsync(string id, CancellationToken ct = default)
    => Task.FromResult<IDeviceSensor?>(null); // add when sensors arrive
}
