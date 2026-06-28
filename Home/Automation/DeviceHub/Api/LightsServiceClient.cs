using System.Net;
using System.Net.Http.Json;
using Fobelity.Home.Automation.Lights.Service.Core.Abstractions;
using Fobelity.Home.Automation.Lights.Service.Core.Models;

namespace Fobelity.Home.Automation.DeviceHub.Api;

public sealed class LightsServiceClient : ILightService
{
  private readonly HttpClient _http;
  public LightsServiceClient(HttpClient http) => _http = http;

  public async Task<IReadOnlyList<LightInfo>> ListAsync(CancellationToken ct) =>
    await _http.GetFromJsonAsync<IReadOnlyList<LightInfo>>("/lights", ct) ?? [];

  public async Task<LightInfo?> GetAsync(string id, CancellationToken ct) =>
    await _http.GetFromJsonAsync<LightInfo>($"/lights/{Uri.EscapeDataString(id)}", ct);

  public async Task<LightSetResult?> SetAsync(string id, LightSetRequest request, CancellationToken ct)
  {
    var resp = await _http.PostAsJsonAsync($"/lights/{Uri.EscapeDataString(id)}/set", request, ct);
    if (resp.StatusCode == HttpStatusCode.NotFound) return null;

    resp.EnsureSuccessStatusCode();
    return await resp.Content.ReadFromJsonAsync<LightSetResult>(cancellationToken: ct);
  }
}
