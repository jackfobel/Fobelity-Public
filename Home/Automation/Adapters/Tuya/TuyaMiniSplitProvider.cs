using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.Adapters.Tuya;

public sealed class TuyaMiniSplitProvider : IThermostatProvider
{
  private readonly TuyaMiniSplitAdapter _adapter;
  private static readonly Device _device =
      new("shop", "Shop MiniSplit", "shop", "Della/Tuya", "MS-36K", new[] { "Thermostat" });

  public TuyaMiniSplitProvider(TuyaMiniSplitAdapter adapter) => _adapter = adapter;

  public Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default)
      => Task.FromResult<IReadOnlyList<Device>>(new[] { _device });

  public Task<IDeviceThermostat?> GetAsync(string id, CancellationToken ct = default)
      => Task.FromResult<IDeviceThermostat?>(id.Equals("shop", StringComparison.OrdinalIgnoreCase) ? _adapter : null);
}
