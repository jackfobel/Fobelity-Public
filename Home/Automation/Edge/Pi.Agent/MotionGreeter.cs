using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Threading;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

internal sealed class MotionGreeter : BackgroundService
{
  private readonly PirMonitor _pir;
  private readonly CameraService _cam;
  private readonly AzureFaceClient _face;
  private readonly AzureSpeechClient _speech;
  private readonly MochaVoice _voice;
  private readonly ILogger<MotionGreeter> _log;
  private readonly IAvatarSpeak _avatarSpeak;
  private readonly int _cooldownSeconds;
  private readonly MochaOptions _mocha;
  private readonly IWakeLoopControl _wake;

  private DateTime _lastGreetingUtc = DateTime.MinValue;
  private int _processingFlag; // 0 = not processing, 1 = processing

  public MotionGreeter(
    PirMonitor pir,
    CameraService cam,
    AzureFaceClient face,
    AzureSpeechClient speech,
    IOptions<AzureSpeechOptions> speechOpts,
    MochaVoice voice,
    IAvatarSpeak avatarSpeak,
    ILogger<MotionGreeter> log,
    IWakeLoopControl wake)
  {
    _pir = pir;
    _cam = cam;
    _face = face;
    _speech = speech;
    _avatarSpeak = avatarSpeak;
    _log = log;
    _voice = voice;
    _cooldownSeconds = speechOpts.Value.GreetCooldownSeconds <= 0 ? 20 : speechOpts.Value.GreetCooldownSeconds;
    _wake = wake;
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    _log.LogInformation("MotionGreeter started.");
    while (!stoppingToken.IsCancellationRequested)
    {
      try
      {
        var s = _pir.Status; // cheap poll (50ms cadence inside PirMonitor)
        var now = DateTime.UtcNow;

        if (s.Present &&
            (now - _lastGreetingUtc).TotalSeconds >= _cooldownSeconds)
        {
          // Attempt to "claim" processing. If already running, do nothing.
          if (Interlocked.CompareExchange(ref _processingFlag, 1, 0) == 0)
          {
            _ = HandleMotionAsync(stoppingToken); // fire-and-forget; flag resets in finally
          }
        }
      }
      catch (Exception ex)
      {
        _log.LogError(ex, "MotionGreeter loop error");
      }
      await Task.Delay(75, stoppingToken);
    }
  }

  private async Task HandleMotionAsync(CancellationToken ct)
  {
    try
    {
      _log.LogInformation("Motion detected. Capturing image...");

      var ok = await _cam.SnapAsync(delayMs: 250, ct: ct);
      if (!ok)
      {
        _log.LogWarning("Snapshot failed.");
        return;
      }

      // Detect faces first (before identifying)
      var detected = await _face.DetectAsync(_cam.LastJpegPath, ct);
      if (detected == 0)
      {
        _log.LogInformation("No faces detected in frame. Skipping greeting.");
        return;
      }

      var (name, conf) = await _face.IdentifyAsync(_cam.LastJpegPath, ct);
      _lastGreetingUtc = DateTime.UtcNow;

      if (!string.IsNullOrWhiteSpace(name) && conf is double c)
      {
        _log.LogInformation("Identified {Name} (conf {Conf:P0})", name, c);

        //await _avatarSpeak.SpeakAsync($"Welcome back, {name}.");
        //await _speech.SpeakAsync($"Welcome back, {name}.");
        //await _voice.SafeSayAsync($"Welcome back, {name}..");
        //await _voice.ListenAfterGreetingAsync(prompt: "What can I do for you?", ct: ct);

        _wake.PauseFor(TimeSpan.FromSeconds(15)); // greet + listen window; tune as needed

        //await _voice.SafeSayAsync($"Hi, {name}.");
        await _voice.SayMochaAsync($"Hi, {name}.", ct, preset: MochaSsmlPreset.Neutral);

        // Optional but recommended: a short settle so you don’t capture TTS tail.
        await Task.Delay(600, ct);

        // Silent-first, then prompt fallback.
        await _voice.ListenAfterGreetingAsync(
          prompt: "What can I do for you?",
          ct: ct);

      }
      else
      {
        _log.LogInformation("Face detected but not recognized. Skipping generic greeting.");
        // If you ever want unknown-face behavior, add it here.
        // await _speech.SpeakAsync("Hello there.");
        // await _voice.ListenAfterGreetAsync(ct);

        //await _voice.SafeSayAsync($"Hey there.");
        await _voice.SayMochaAsync($"Hey there.", ct, preset: MochaSsmlPreset.Neutral);
      }
    }
    catch (Exception ex)
    {
      _log.LogError(ex, "HandleMotionAsync failed");
    }
    finally
    {
      Interlocked.Exchange(ref _processingFlag, 0);
    }

  }
}
