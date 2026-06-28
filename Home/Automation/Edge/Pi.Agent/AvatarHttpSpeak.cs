using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class AvatarHttpSpeak : IAvatarSpeak
  {
    private readonly HttpClient _http;
    private readonly AvatarOptions _opt;

    public AvatarHttpSpeak(HttpClient http, IOptions<AvatarOptions> opt)
    {
      _http = http;
      _opt = opt.Value;
    }

    public async Task<bool> SpeakAsync(string text, string? voice = null, string? clientId = null, CancellationToken ct = default)
    {
      var url = $"{_opt.Url.TrimEnd('/')}/api/tts/say";
      var payload = new { text, voice, clientId = clientId ?? _opt.DefaultClientId };

      using var req = new HttpRequestMessage(HttpMethod.Post, url)
      {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
      };

      if (!string.IsNullOrWhiteSpace(_opt.ApiKey))
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiKey);

      using var resp = await _http.SendAsync(req, ct);
      return resp.IsSuccessStatusCode;
    }
  }

}
