using Fobelity.Home.Automation.Edge;
using Fobelity.Home.Automation.Edge.Abstractions;
using Fobelity.Home.Automation.Edge.Pi.Agent;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System.Runtime.Intrinsics.X86;
using Serilog.Debugging;
using Microsoft.AspNetCore.SignalR.Client;
using Serilog.Events;


var builder = WebApplication.CreateBuilder(args);



// --- Avatar options + transport selection ---
builder.Services.Configure<AvatarOptions>(builder.Configuration.GetSection("Avatar"));
var useHub = builder.Configuration.GetValue<bool>("Avatar:UseHub");

if (useHub)
{
  // SignalR hub client (persistent connection)
  builder.Services.AddSingleton<AvatarHubSpeak>();
  builder.Services.AddSingleton<IAvatarSpeak>(sp => sp.GetRequiredService<AvatarHubSpeak>());
  builder.Services.AddHostedService(sp => sp.GetRequiredService<AvatarHubSpeak>());
}
else
{
  // Single typed HttpClient for /api/tts/say
  builder.Services
    .AddHttpClient<AvatarHttpSpeak>()
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
      // Accept dev certs if the kiosk uses HTTPS with an untrusted cert (dev only).
      return new HttpClientHandler
      {
#if DEBUG
        ServerCertificateCustomValidationCallback =
          HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
#endif
      };
    });

  builder.Services.AddSingleton<IAvatarSpeak>(sp => sp.GetRequiredService<AvatarHttpSpeak>());
}




builder.Services.Configure<GpioOptions>(builder.Configuration.GetSection("Gpio"));
builder.Services.AddSingleton<IGpioSwitch, PiGpioSwitch>();
builder.Services.AddSingleton<PirMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PirMonitor>());

// config binding
builder.Services.Configure<AzureFaceOptions>(builder.Configuration.GetSection("AzureFace"));
builder.Services.Configure<AzureSpeechOptions>(builder.Configuration.GetSection("AzureSpeech"));
builder.Services.Configure<A2AOptions>(builder.Configuration.GetSection("A2A"));
builder.Services.Configure<MochaOptions>(builder.Configuration.GetSection("Mocha"));

// services
builder.Services.AddSingleton<CameraService>();
builder.Services.AddSingleton<AzureFaceClient>();
builder.Services.AddSingleton<AzureSpeechClient>();
builder.Services.AddHostedService<MotionGreeter>();

builder.Services.AddHttpClient();                       // for /say + A2A discovery
builder.Services.AddSingleton<MochaVoice>();            // the voice driver
//builder.Services.AddHostedService<MochaWakeService>();
builder.Services.AddSingleton<MochaWakeService>();
builder.Services.AddSingleton<IWakeLoopControl>(sp => sp.GetRequiredService<MochaWakeService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MochaWakeService>());



builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

SelfLog.Enable(msg => Console.Error.WriteLine($"[Serilog] {msg}"));




var app = builder.Build();

var apiKey = builder.Configuration["AzureFace:Key"];

//app.Use(async (ctx, next) =>
//{
//  if (!string.IsNullOrEmpty(apiKey))
//  {
//    var ok = ctx.Request.Headers.TryGetValue("X-API-Key", out var k) && k == apiKey;
//    if (!ok) { ctx.Response.StatusCode = 401; await ctx.Response.WriteAsync("Unauthorized"); return; }
//  }
//  await next();
//});

app.UseSerilogRequestLogging(opts =>
{
  opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
  // Optional: reduce noise
  opts.GetLevel = (_, _, _) => LogEventLevel.Information;
});


app.MapGet("/status", async (IGpioSwitch sw, CancellationToken ct) =>
{
  var on = await sw.IsOnAsync(ct);
  return Results.Ok(new DeviceStatus(on, DateTimeOffset.Now));
});

app.MapPost("/led/on", async (IGpioSwitch sw, CancellationToken ct) =>
{
  await sw.SetOnAsync(ct);
  return Results.NoContent();
});

app.MapPost("/led/off", async (IGpioSwitch sw, CancellationToken ct) =>
{
  await sw.SetOffAsync(ct);
  return Results.NoContent();
});

app.MapGet("/motion/status", (PirMonitor pir) =>
{
  var s = pir.Status;
  return Results.Ok(s);
});

app.MapGet("/motion/raw", (PirMonitor pir) =>
{
  var d = pir.Status.Debug;
  return Results.Ok(new
  {
    raw_stable = d.RawStable,
    raw_since_s = d.RawSinceAgeSeconds,
    last_rise_age_s = d.LastRiseAgeSeconds,
    present = pir.Status.Present
  });
});

app.MapPost("/camera/snap", async (CameraService cam, CancellationToken ct) =>
{
  var ok = await cam.SnapAsync(delayMs: 250, ct: ct);
  return ok ? Results.Ok(new { ok = true, path = cam.LastJpegPath }) : Results.StatusCode(429);
});

app.MapGet("/camera/last", (CameraService cam) =>
    File.Exists(cam.LastJpegPath)
        ? Results.File(cam.LastJpegPath, "image/jpeg")
        : Results.NotFound());

app.MapPost("/identity/identify", async (CameraService cam, AzureFaceClient face, CancellationToken ct) =>
{
  if (!File.Exists(cam.LastJpegPath)) return Results.BadRequest(new { error = "no image" });
  var (name, conf) = await face.IdentifyAsync(cam.LastJpegPath, ct);
  return Results.Ok(new { name, confidence = conf });
});

// POST /identity/persons  { "name": "Jack" } -> { personId }
app.MapPost("/identity/persons", async (HttpRequest req, AzureFaceClient face, CancellationToken ct) =>
{
  var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
  var name = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("name").GetString();
  if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { error = "name required" });
  var id = await face.CreatePersonAsync(name!, ct); // I'll wire this method if you want
  return Results.Ok(new { personId = id });
});

// POST /identity/persons/{id}/faces  (uses last snapshot)
app.MapPost("/identity/persons/{id}/faces", async (string id, CameraService cam, AzureFaceClient face, CancellationToken ct) =>
{
  if (!File.Exists(cam.LastJpegPath)) return Results.BadRequest(new { error = "no image" });
  var faceId = await face.AddFaceAsync(id, cam.LastJpegPath, ct); // I'll add this too
  return Results.Ok(new { persistedFaceId = faceId });
});

// POST /identity/train
app.MapPost("/identity/train", async (AzureFaceClient face, CancellationToken ct) =>
{
  await face.TrainAsync(ct); // simple wrapper
  return Results.Accepted();
});


// POST /say  (raw body = text)
app.MapPost("/api/tts/say", async (AzureSpeechClient sp, HttpRequest req, CancellationToken ct) =>
{
  Log.Information("Received request for /api/tts/say");

  string body;
  using (var sr = new StreamReader(req.Body))
    body = await sr.ReadToEndAsync(ct);

  var trimmed = body?.Trim() ?? string.Empty;

  string? text = null;
  string? ssml = null;

  // Accept either {"text":"..."} / {"ssml":"..."} or raw "..."
  if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("{", StringComparison.Ordinal))
  {
    try
    {
      using var doc = System.Text.Json.JsonDocument.Parse(trimmed);

      if (doc.RootElement.TryGetProperty("ssml", out var se) && se.ValueKind == System.Text.Json.JsonValueKind.String)
        ssml = se.GetString();

      if (doc.RootElement.TryGetProperty("text", out var te) && te.ValueKind == System.Text.Json.JsonValueKind.String)
        text = te.GetString();
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "Failed to parse JSON body for /api/tts/say; falling back to raw text.");
      text = trimmed;
    }
  }
  else
  {
    text = trimmed;
  }

  // Default safety
  if (string.IsNullOrWhiteSpace(ssml) && string.IsNullOrWhiteSpace(text))
    text = "This is a test from the Raspberry Pi.";

  // Prefer SSML when provided
  if (!string.IsNullOrWhiteSpace(ssml))
  {
    var (ok, reason, error) = await sp.SpeakSsmlAsync(ssml);
    Log.Information("[/api/tts/say] mode=ssml ok={Ok} reason={Reason} error={Error}", ok, reason, error);
    return Results.Ok(new { ok, reason, error, mode = "ssml" });
  }
  else
  {
    var (ok, reason, error) = await sp.SpeakAsync(text!);
    Log.Information("[/api/tts/say] mode=text ok={Ok} reason={Reason} error={Error} text='{Text}'", ok, reason, error, text);
    return Results.Ok(new { ok, reason, error, mode = "text" });
  }
});



// POST /say/file?name=tts.wav   (returns the wav so you can play/download)
app.MapPost("/say/file", async (AzureSpeechClient sp, HttpRequest req) =>
{
  var name = req.Query["name"].ToString();
  if (string.IsNullOrWhiteSpace(name)) name = "tts.wav";
  var path = Path.Combine("/tmp", name);

  string text;
  using (var sr = new StreamReader(req.Body))
    text = await sr.ReadToEndAsync();
  if (string.IsNullOrWhiteSpace(text)) text = "File synthesis test.";

  var (ok, p, reason, error) = await sp.SynthesizeToFileAsync(text, path);
  if (!ok) return Results.Ok(new { ok, reason, error, path = p });

  // serve the WAV so you can verify easily
  return Results.File(p, "audio/wav", enableRangeProcessing: true);
});

app.MapPost("/who", async (CameraService cam, AzureFaceClient face, CancellationToken ct) =>
{
  if (!File.Exists(cam.LastJpegPath)) return Results.BadRequest(new { error = "no image" });
  var (name, conf) = await face.IdentifyAsync(cam.LastJpegPath, ct);
  return Results.Ok(new { name, confidence = conf });
});

app.MapPost("/voice/test", (MochaVoice voice, IHostApplicationLifetime life) =>
{
  _ = Task.Run(() => voice.ListenAfterGreetAsync(life.ApplicationStopping));
  return Results.Accepted(value: new { started = true, mode = "wake-window" });
});

app.MapPost("/voice/listenOnce", (HttpRequest req, MochaVoice voice, IHostApplicationLifetime life) =>
{
  var dry = string.Equals(req.Query["dryRun"], "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(req.Query["dryRun"], "true", StringComparison.OrdinalIgnoreCase);

  _ = Task.Run(() => voice.ListenOneShotAsync(forceDryRun: dry, ct: life.ApplicationStopping));
  return Results.Accepted(value: new { started = true, mode = "listen-once", dryRun = dry });
});


app.MapPost("/voice/oneshot", (MochaVoice voice) =>
{
  _ = Task.Run(() => voice.ListenOneShotAsync(false, CancellationToken.None));
  return Results.Accepted(value: new { started = true, mode = "one-shot" });
});

// POST /voice/kwsTest  -> run keyword-only wake window
app.MapPost("/voice/kwsTest", (MochaVoice voice, IHostApplicationLifetime life) =>
{
  _ = Task.Run(() => voice.ListenWithKeywordAsync(life.ApplicationStopping));
  return Results.Accepted(value: new { started = true, mode = "kws" });
});

// --- Optional: test endpoint so you can trigger kiosk speech from your laptop ---
app.MapPost("/kiosk/say", async (IAvatarSpeak speak, HttpRequest req, CancellationToken ct) =>
{
  using var sr = new StreamReader(req.Body);
  var text = await sr.ReadToEndAsync(ct);
  Log.Information("[KIOSK] /kiosk/say payload='{Text}'", text);

  if (string.IsNullOrWhiteSpace(text)) text = "Edge says hello via kiosk.";
  var ok = await speak.SpeakAsync(text, clientId: null, ct: ct);
  return ok ? Results.Ok(new { ok = true }) : Results.StatusCode(502);
});




app.MapGet("/healthz", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));

app.Run();
