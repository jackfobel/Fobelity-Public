using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Fobelity.Home.Avatar.VisemeStreamer;

var builder = WebApplication.CreateBuilder(args);

// SignalR (JSON camelCase)
builder.Services.AddSignalR().AddJsonProtocol(o =>
{
  o.PayloadSerializerOptions = new JsonSerializerOptions
  {
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };
});

var app = builder.Build();

// serve wwwroot/index.html
app.UseDefaultFiles();
app.UseStaticFiles();

// SignalR endpoint
app.MapHub<VisemeHub>("/visemeHub");

// Dev URL
app.Urls.Clear();
app.Urls.Add("http://localhost:5110");

// -----------------------------
// Azure Speech settings
// -----------------------------
string? key =
  Environment.GetEnvironmentVariable("SPEECH_KEY") ??
  builder.Configuration.GetSection("AzureSpeech").GetValue<string>("Key");

string? region =
  Environment.GetEnvironmentVariable("SPEECH_REGION") ??
  builder.Configuration.GetSection("AzureSpeech").GetValue<string>("Region");

string voiceName =
  Environment.GetEnvironmentVariable("SPEECH_VOICE") ??
  builder.Configuration.GetSection("AzureSpeech").GetValue<string>("Voice") ??
  "en-US-AriaNeural"; // safe default

string? device =
  Environment.GetEnvironmentVariable("SPEECH_DEVICE") ??
  builder.Configuration.GetSection("AzureSpeech").GetValue<string>("Device"); // "", "default", "pulse", "plughw:2,0", etc.

var hub = app.Services.GetRequiredService<IHubContext<VisemeHub>>();

static bool IsDefaultRoutingToken(string? d) =>
  string.IsNullOrWhiteSpace(d) ||
  d.Equals("default", StringComparison.OrdinalIgnoreCase) ||
  d.Equals("pulse", StringComparison.OrdinalIgnoreCase);

static AudioConfig BuildAudioConfig(string? device)
{
  var d = device?.Trim();

  // Empty/default = OS default routing (Pulse default sink on most Pi Desktop setups)
  if (string.IsNullOrWhiteSpace(d) || d.Equals("default", StringComparison.OrdinalIgnoreCase))
    return AudioConfig.FromDefaultSpeakerOutput();

  // If you have the ALSA pulse plugin, "pulse" works as an ALSA device name.
  //if (d.Equals("pulse", StringComparison.OrdinalIgnoreCase))
  //  return AudioConfig.FromSpeakerOutput("pulse");

  if (d.Equals("pulse", StringComparison.OrdinalIgnoreCase))
    return AudioConfig.FromDefaultSpeakerOutput();

  // Otherwise assume ALSA device string (e.g., "plughw:2,0")
  return AudioConfig.FromSpeakerOutput(d);
}

static string BuildVisemeSsml(string text, string voiceName)
{
  var safe = System.Net.WebUtility.HtmlEncode(text ?? string.Empty);

  // Put mstts namespace on <speak> so mstts:viseme is honored.
  return $@"<speak version='1.0' xml:lang='en-US' xmlns:mstts='https://www.w3.org/2001/mstts'>
  <voice name='{voiceName}'>
    <mstts:viseme type='FacialExpression' />
    {safe}
  </voice>
</speak>";
}

// -----------------------------
// Synth init (kept alive for app lifetime)
// -----------------------------
SpeechSynthesizer? synth = null;
AudioConfig? audio = null;
bool speechReady = false;

// Prevent overlapping calls (overlap can sound robotic/clipped)
var ttsGate = new SemaphoreSlim(1, 1);

if (string.IsNullOrWhiteSpace(key) || key.StartsWith("<") || string.IsNullOrWhiteSpace(region))
{
  Console.WriteLine("!! Azure Speech not configured. Set SPEECH_KEY/SPEECH_REGION or AzureSpeech:Key/Region.");
  Console.WriteLine("   Server will run, but /api/tts/say will return 503.");
}
else
{
  Console.WriteLine($"Azure Speech configured. Region={region} Voice={voiceName} Device={(IsDefaultRoutingToken(device) ? "<default>" : device)}");

  var cfg = SpeechConfig.FromSubscription(key, region);

  // IMPORTANT: configured voice everywhere (no hard-coded Jenny)
  cfg.SpeechSynthesisVoiceName = voiceName;

  // Match common Pulse sink rate on Pi (your sinks show 48kHz); reduces resample weirdness.
  cfg.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff48Khz16BitMonoPcm);

  audio = BuildAudioConfig(device);
  synth = new SpeechSynthesizer(cfg, audio);

  // Stream visemes to SignalR clients
  synth.VisemeReceived += (_, e) =>
  {
    var ms = TimeSpan.FromTicks((long)e.AudioOffset).TotalMilliseconds;
    var payload = new VisemeEvent(e.VisemeId, ms, e.Animation);
    _ = hub.Clients.All.SendAsync("viseme", payload);
  };

  speechReady = true;
}

// Dispose cleanly
app.Lifetime.ApplicationStopping.Register(() =>
{
  try { ttsGate.Dispose(); } catch { /* ignore */ }
  try { synth?.Dispose(); } catch { /* ignore */ }
  try { audio?.Dispose(); } catch { /* ignore */ }
});

// -----------------------------
// API
// -----------------------------
app.MapGet("/healthz", () => Results.Ok(new
{
  ok = true,
  speechReady,
  voice = voiceName,
  device = IsDefaultRoutingToken(device) ? "<default>" : device
}));

// Preferred endpoint used by Edge.Pi.Agent in your config
app.MapPost("/api/tts/say", async (HttpRequest req, CancellationToken ct) =>
{
  if (!speechReady || synth is null)
    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

  // Accept either JSON { text, ssml } OR raw text/plain
  string body;
  using (var sr = new StreamReader(req.Body, Encoding.UTF8))
    body = await sr.ReadToEndAsync(ct);

  var trimmed = (body ?? string.Empty).Trim();

  string? text = null;
  string? ssml = null;

  if (trimmed.StartsWith("{", StringComparison.Ordinal))
  {
    try
    {
      using var doc = JsonDocument.Parse(trimmed);
      if (doc.RootElement.TryGetProperty("ssml", out var se) && se.ValueKind == JsonValueKind.String)
        ssml = se.GetString();
      if (doc.RootElement.TryGetProperty("text", out var te) && te.ValueKind == JsonValueKind.String)
        text = te.GetString();
    }
    catch
    {
      text = trimmed;
    }
  }
  else
  {
    text = trimmed;
  }

  if (string.IsNullOrWhiteSpace(ssml) && string.IsNullOrWhiteSpace(text))
    return Results.BadRequest(new { error = "text or ssml required" });

  // If caller didn't provide SSML, build viseme SSML with the configured voice.
  if (string.IsNullOrWhiteSpace(ssml))
    ssml = BuildVisemeSsml(text!, voiceName);

  await ttsGate.WaitAsync(ct);
  try
  {
    var result = await synth.SpeakSsmlAsync(ssml);

    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
      return Results.Ok(new { ok = true, voice = voiceName });

    var cancel = SpeechSynthesisCancellationDetails.FromResult(result);
    return Results.Ok(new { ok = false, cancel.Reason, cancel.ErrorCode, cancel.ErrorDetails });
  }
  finally
  {
    ttsGate.Release();
  }
});

// Back-compat alias (your older curl)
app.MapPost("/say", async ([FromBody] SayRequest req, HttpRequest httpReq, CancellationToken ct) =>
{
  // Reuse the same logic: send JSON to /api/tts/say
  if (req is null || string.IsNullOrWhiteSpace(req.Text))
    return Results.BadRequest(new { error = "text required" });

  // Call local handler directly
  var ssml = BuildVisemeSsml(req.Text, voiceName);

  if (!speechReady || synth is null)
    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

  await ttsGate.WaitAsync(ct);
  try
  {
    var result = await synth.SpeakSsmlAsync(ssml);
    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
      return Results.Ok(new { ok = true, voice = voiceName });

    var cancel = SpeechSynthesisCancellationDetails.FromResult(result);
    return Results.Ok(new { ok = false, cancel.Reason, cancel.ErrorCode, cancel.ErrorDetails });
  }
  finally
  {
    ttsGate.Release();
  }
});

Console.WriteLine("Server:  http://localhost:5110   (Visualizer at /)");
Console.WriteLine("POST     http://localhost:5110/api/tts/say   JSON: { \"text\": \"Hello\" }");
Console.WriteLine("GET      http://localhost:5110/healthz");

await app.RunAsync();
