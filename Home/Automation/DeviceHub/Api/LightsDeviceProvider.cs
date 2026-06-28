using Fobelity.Home.Automation.DeviceHub.Core;
using Fobelity.Home.Automation.DeviceHub.Core.Models;
using Fobelity.Home.Automation.Lights.Service.Core.Abstractions;

public sealed class LightsDeviceProvider : IDeviceProvider
{
  private readonly ILightService _lights;
  public LightsDeviceProvider(ILightService lights) => _lights = lights;

  public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default)
  {
    var infos = await _lights.ListAsync(ct);

    // Put the synonyms directly in Name so the agent matches “bay light”, “main bay light”, etc.
    return infos.Select(l => new Device(
        Id: l.Id,
        Name: $"{l.Name} (aliases: bay light, main bay light, lights)",
        Location: string.IsNullOrWhiteSpace(l.Location) ? "shop" : l.Location,
        Brand: "Zigbee2MQTT",
        Model: "Gen2 plug / relay",
        Capabilities: new[] { "light", "switch" } // keep these lower-case for agent matching
      ))
      .ToList();
  }
}
