using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Api
{
  public sealed class InMemoryInventory : IDeviceInventory
  {
    // Later: replace the stub with DI registrations from Adapters.Tuya and Adapters.Seam

    private readonly Dictionary<string, Thermo> _thermos = new()
    {
      ["shop"] = new("shop", "Shop MiniSplit", "shop", "Della/Tuya", "MS-36K"),
      ["home"] = new("home", "Home Ecobee", "home", "Ecobee", "SmartThermostat")
    };
    public Task<IReadOnlyList<Device>> ListAsync(CancellationToken _) =>
      Task.FromResult<IReadOnlyList<Device>>(_thermos.Values.Select(t =>
        new Device(t.Id, t.Name, t.Location, t.Brand, t.Model, new[] { "Thermostat" })).ToList());

    public Task<IDeviceThermostat?> GetThermostatAsync(string id, CancellationToken _) =>
      Task.FromResult<IDeviceThermostat?>(_thermos.TryGetValue(id, out var t) ? t : null);

    public Task<IDeviceSensor?> GetSensorAsync(string id, CancellationToken _) =>
      Task.FromResult<IDeviceSensor?>(null);
  }

}