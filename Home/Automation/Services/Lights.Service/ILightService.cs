using Fobelity.Home.Automation.Lights.Service.Core.Models;
using Fobelity.Home.Automation.Lights.Service.Core.Services;

namespace Fobelity.Home.Automation.Lights.Service.Core.Abstractions;

public interface ILightService
{
  Task<IReadOnlyList<LightInfo>> ListAsync(CancellationToken ct);

  Task<LightInfo?> GetAsync(string id, CancellationToken ct);

  /// <summary>
  /// Publishes a Zigbee2MQTT-compatible command to &lt;TopicRoot&gt;/set.
  /// Returns null when the id is unknown.
  /// </summary>
  Task<LightSetResult?> SetAsync(string id, LightSetRequest request, CancellationToken ct);
}
