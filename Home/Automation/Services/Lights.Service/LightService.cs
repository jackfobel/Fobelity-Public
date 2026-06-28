using Fobelity.Home.Automation.Lights.Service.Core.Abstractions;
using Fobelity.Home.Automation.Lights.Service.Core.Models;
using Fobelity.Home.Automation.Lights.Service.Core.Options;
using Fobelity.Home.Automation.Lights.Service.Infra.Mqtt;
using Microsoft.Extensions.Options;

namespace Fobelity.Home.Automation.Lights.Service.Core.Services;

public sealed class LightService : ILightService
{
  private readonly LightsOptions _opts;
  private readonly IMqttPublisher _mqtt;

  public LightService(IOptions<LightsOptions> opts, IMqttPublisher mqtt)
  {
    _opts = opts.Value;
    _mqtt = mqtt;
  }

  public Task<IReadOnlyList<LightInfo>> ListAsync(CancellationToken ct)
  {
    var list = _opts.Registry.Lights.Select(kvp =>
      new LightInfo(kvp.Key, kvp.Value.Name, kvp.Value.Location, kvp.Value.TopicRoot)
    ).ToList();

    return Task.FromResult<IReadOnlyList<LightInfo>>(list);
  }

  public Task<LightInfo?> GetAsync(string id, CancellationToken ct)
  {
    if (!_opts.Registry.Lights.TryGetValue(id, out var reg))
      return Task.FromResult<LightInfo?>(null);

    return Task.FromResult<LightInfo?>(new LightInfo(id, reg.Name, reg.Location, reg.TopicRoot));
  }

  public async Task<LightSetResult?> SetAsync(string id, LightSetRequest request, CancellationToken ct)
  {
    if (!_opts.Registry.Lights.TryGetValue(id, out var reg))
      return null;

    if (request.DryRun)
    {
      return new LightSetResult(
        id,
        Topic: $"{reg.TopicRoot}/set",
        Payload: BuildPayload(request),
        SentAtUtc: DateTimeOffset.UtcNow
      );
    }

    var topic = $"{reg.TopicRoot}/set";
    var payload = BuildPayload(request);

    await _mqtt.PublishJsonAsync(topic, payload, ct);

    return new LightSetResult(id, topic, payload, DateTimeOffset.UtcNow);
  }

  private static object BuildPayload(LightSetRequest r)
  {
    // Only include fields that are present.
    var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    if (!string.IsNullOrWhiteSpace(r.State))
      d["state"] = r.State!.ToUpperInvariant(); // Zigbee2MQTT expects ON/OFF

    if (r.Brightness.HasValue)
      d["brightness"] = Math.Clamp(r.Brightness.Value, 0, 254);

    if (r.ColorTemp.HasValue)
      d["color_temp"] = r.ColorTemp.Value;

    if (d.Count == 0)
      throw new InvalidOperationException("No command fields provided (state/brightness/color_temp).");

    return d;
  }
}
