using System.Net.Http.Json;
using Fobelity.Home.Automation.DeviceHub.Core.Models;

namespace Fobelity.Home.Automation.Adapters.Tuya;

public sealed class MiniSplitHttpClient
{
  private readonly HttpClient _http;
  public MiniSplitHttpClient(HttpClient http) => _http = http;

  // Shape only what we need from your MiniSplit controller's DeviceStatus
  private sealed class MiniSplitStatusDto
  {
    public string? Mode { get; set; }           // "cold" / "heat" / ...
    public double? TempSetF { get; set; }
    public double? TempCurrentF { get; set; }
    public double? HumidityCurrent { get; set; }
    public bool Switch { get; set; }            // power state
  }

  private const string StatusPath = "/api/minisplit/status";
  private const string TurnOnPath = "/api/minisplit/turn-on";
  private const string TurnOffPath = "/api/minisplit/turn-off";
  private const string SetPath = "/api/minisplit/set";

  public async Task<ThermostatStatus> GetStatusAsync(CancellationToken ct)
  {
    var dto = await _http.GetFromJsonAsync<MiniSplitStatusDto>(StatusPath, ct)
              ?? new MiniSplitStatusDto();
    return Map(dto);
  }

  public async Task<ThermostatStatus> TurnOnAsync(CancellationToken ct)
  {
    using var res = await _http.PostAsync(TurnOnPath, content: null, ct);
    res.EnsureSuccessStatusCode();
    return await GetStatusAsync(ct);
  }

  public async Task<ThermostatStatus> TurnOffAsync(CancellationToken ct)
  {
    using var res = await _http.PostAsync(TurnOffPath, content: null, ct);
    res.EnsureSuccessStatusCode();
    return await GetStatusAsync(ct);
  }

  // Kept for convenience if you prefer a single entry point
  public Task<ThermostatStatus> TurnAsync(bool on, CancellationToken ct) =>
      on ? TurnOnAsync(ct) : TurnOffAsync(ct);

  // Call your controller's /api/minisplit/set endpoint with the richer MiniSplitSetRequest
  public async Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct)
  {
    using var httpRes = await _http.PostAsJsonAsync("/api/minisplit/set", req, ct);
    await EnsureSuccessOrThrowAsync(httpRes, "MiniSplit set");
    return await GetStatusAsync(ct); // single mapping path
  }

  private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage res, string purpose)
  {
    if (res.IsSuccessStatusCode) return;
    var body = await res.Content.ReadAsStringAsync();
    throw new HttpRequestException($"{purpose} failed: {(int)res.StatusCode} {res.ReasonPhrase}. Body: {body}");
  }


  private static ThermostatStatus Map(MiniSplitStatusDto dto)
  {
    static double? NormalizeF(double? v) => v is null ? null : (v > 120 ? v / 10.0 : v);

    var mode = dto.Mode?.ToLowerInvariant() switch
    {
      "cold" => "cool",
      "hot" => "heat",
      "off" => "off",
      _ => dto.Switch ? "cool" : "off"
    };

    double? humidity = (dto.HumidityCurrent.HasValue && dto.HumidityCurrent > 0) ? dto.HumidityCurrent : null;

    var tempSetF = NormalizeF(dto.TempSetF);
    var tempCurrentF = NormalizeF(dto.TempCurrentF);

    return new ThermostatStatus(
      Mode: mode,
      SetpointCoolF: mode == "cool" ? tempSetF : null,
      SetpointHeatF: mode == "heat" ? tempSetF : null,
      InsideTempF: tempCurrentF,
      HumidityPct: humidity,
      IsOn: dto.Switch,
      LastUpdatedUtc: DateTimeOffset.UtcNow
    );
  }
}
