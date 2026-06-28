using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Fobelity.Home.Automation.Adapters.Seam;

public sealed class SeamThermostatClient
{
  private readonly HttpClient _http;
  public bool IsConfigured { get; }

  public SeamThermostatClient(HttpClient http, IConfiguration cfg)
  {
    _http = http;
    IsConfigured = !string.IsNullOrWhiteSpace(cfg["SEAM_API_KEY"]);
  }

  public async Task<IReadOnlyList<SeamThermostatDto>> ListAsync(CancellationToken ct)
  {
    if (!IsConfigured) return Array.Empty<SeamThermostatDto>();

    var resp = await _http.PostAsJsonAsync("thermostats/list", new { }, ct);
    resp.EnsureSuccessStatusCode();

    var json = await resp.Content.ReadFromJsonAsync<SeamListResponse>(cancellationToken: ct);
    var list = json?.thermostats ?? new List<SeamThermostatDto>();
    return list;

  }

  public Task SetModeAsync(string thermostatId, string mode, CancellationToken ct) =>
      PostAsync("thermostats/set_mode", new { thermostat_id = thermostatId, hvac_mode = mode }, ct);

  public Task HeatCoolAsync(string thermostatId, double heatF, double coolF, CancellationToken ct) =>
      PostAsync("thermostats/heat_cool", new
      {
        thermostat_id = thermostatId,
        heat_temperature_f = heatF,
        cool_temperature_f = coolF
      }, ct);

  private async Task PostAsync(string path, object body, CancellationToken ct)
  {
    if (!IsConfigured) return;
    var resp = await _http.PostAsJsonAsync(path, body, ct);
    resp.EnsureSuccessStatusCode();
  }
}

public sealed class SeamListResponse
{
  public List<SeamThermostatDto> thermostats { get; set; } = new();
}

public sealed class SeamThermostatDto
{
  public string? thermostat_id { get; set; }
  public string? hvac_mode { get; set; }
  public double? current_temperature_f { get; set; }
  public string? model { get; set; }
  public string? name { get; set; }
}
