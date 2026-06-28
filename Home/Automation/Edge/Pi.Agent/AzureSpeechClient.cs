using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using Serilog;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

public sealed class AzureSpeechClient : IDisposable
{
  private readonly AzureSpeechOptions _opts;

  private SpeechSynthesizer? _synth;
  private AudioConfig? _audio;
  private bool _failed;

  // Prevent overlapping TTS (overlaps often sound “robotic” / clipped)
  private readonly SemaphoreSlim _ttsGate = new(1, 1);

  public AzureSpeechClient(IOptions<AzureSpeechOptions> opts) => _opts = opts.Value;

  private (string key, string region, string? voice, string? device) Resolve()
  {
    var key = Environment.GetEnvironmentVariable("SPEECH_KEY") ?? _opts.Key ?? string.Empty;
    var region = Environment.GetEnvironmentVariable("SPEECH_REGION") ?? _opts.Region ?? string.Empty;

    // Optional overrides
    var voice = Environment.GetEnvironmentVariable("SPEECH_VOICE") ?? _opts.Voice;
    var device = Environment.GetEnvironmentVariable("SPEECH_DEVICE") ?? _opts.Device;

    return (key, region, voice, device);
  }

  private static bool IsDefaultRoutingToken(string? device) =>
    string.IsNullOrWhiteSpace(device) ||
    device.Equals("default", StringComparison.OrdinalIgnoreCase) ||
    device.Equals("pulse", StringComparison.OrdinalIgnoreCase);

  private AudioConfig BuildAudioConfig(string? device)
  {
    var d = device?.Trim();

    // Treat "pulse" as "use default routing" for SPEAKER output on Linux
    if (string.Equals(d, "pulse", StringComparison.OrdinalIgnoreCase))
      return AudioConfig.FromDefaultSpeakerOutput();

    if (string.IsNullOrWhiteSpace(d) || string.Equals(d, "default", StringComparison.OrdinalIgnoreCase))
      return AudioConfig.FromDefaultSpeakerOutput();

    return AudioConfig.FromSpeakerOutput(d);
  }


  private static SpeechConfig BuildSpeechConfig(string key, string region, string? voice)
  {
    var cfg = SpeechConfig.FromSubscription(key, region);

    if (!string.IsNullOrWhiteSpace(voice))
      cfg.SpeechSynthesisVoiceName = voice;

    // Match common Pulse sink rate on Pi (48kHz) to reduce resample artifacts
    cfg.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff48Khz16BitMonoPcm);

    // Optional diagnostics:
    // cfg.SetProperty(PropertyId.Speech_LogFilename, "/tmp/speechsdk-tts.log");

    return cfg;
  }

  private bool EnsureSynth()
  {
    if (_failed) return false;
    if (_synth != null) return true;

    try
    {
      var (key, region, voice, device) = Resolve();
      if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
        throw new InvalidOperationException("AzureSpeech key/region not configured (AzureSpeech:Key/Region or SPEECH_KEY/SPEECH_REGION)");

      var cfg = BuildSpeechConfig(key, region, voice);

      // Try configured device first (if any), then fall back to default output once.
      var requestedDevice = device?.Trim();

      try
      {
        _audio = BuildAudioConfig(requestedDevice);
        _synth = new SpeechSynthesizer(cfg, _audio);

        Log.Information("[Speech] TTS initialized. Voice={Voice} Device={Device}",
          voice ?? "<default-voice>",
          IsDefaultRoutingToken(requestedDevice) ? "<default>" : requestedDevice);

        return true;
      }
      catch (Exception exDevice)
      {
        Log.Warning(exDevice, "[Speech] TTS init failed for device '{Device}'. Falling back to default speaker output.",
          requestedDevice);

        _audio?.Dispose();
        _audio = AudioConfig.FromDefaultSpeakerOutput();
        _synth = new SpeechSynthesizer(cfg, _audio);

        Log.Information("[Speech] TTS initialized with default speaker output. Voice={Voice}",
          voice ?? "<default-voice>");

        return true;
      }
    }
    catch (Exception ex)
    {
      Log.Error(ex, "[Speech] init failed");
      _failed = true;
      return false;
    }
  }

  public async Task<(bool ok, string reason, string? error)> SpeakAsync(string text)
  {
    if (!EnsureSynth()) return (false, "InitFailed", "Speech init failed");

    await _ttsGate.WaitAsync().ConfigureAwait(false);
    try
    {
      var res = await _synth!.SpeakTextAsync(text ?? string.Empty).ConfigureAwait(false);

      if (res.Reason == ResultReason.SynthesizingAudioCompleted)
        return (true, nameof(ResultReason.SynthesizingAudioCompleted), null);

      var cancel = SpeechSynthesisCancellationDetails.FromResult(res);
      return (false, cancel.Reason.ToString(), cancel.ErrorDetails);
    }
    finally
    {
      _ttsGate.Release();
    }
  }

  public async Task<(bool ok, string reason, string? error)> SpeakSsmlAsync(string ssml)
  {
    if (!EnsureSynth()) return (false, "InitFailed", "Speech init failed");

    await _ttsGate.WaitAsync().ConfigureAwait(false);
    try
    {
      var res = await _synth!.SpeakSsmlAsync(ssml ?? string.Empty).ConfigureAwait(false);

      if (res.Reason == ResultReason.SynthesizingAudioCompleted)
        return (true, nameof(ResultReason.SynthesizingAudioCompleted), null);

      var cancel = SpeechSynthesisCancellationDetails.FromResult(res);
      return (false, cancel.Reason.ToString(), cancel.ErrorDetails);
    }
    finally
    {
      _ttsGate.Release();
    }
  }

  public async Task<(bool ok, string path, string reason, string? error)> SynthesizeToFileAsync(string text, string path)
  {
    var (key, region, voice, _) = Resolve();
    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
      return (false, path, "InitFailed", "Speech key/region not configured");

    var cfg = BuildSpeechConfig(key, region, voice);

    using var audio = AudioConfig.FromWavFileOutput(path);
    using var synth = new SpeechSynthesizer(cfg, audio);

    var res = await synth.SpeakTextAsync(text ?? string.Empty).ConfigureAwait(false);

    if (res.Reason == ResultReason.SynthesizingAudioCompleted)
      return (true, path, nameof(ResultReason.SynthesizingAudioCompleted), null);

    var cancel = SpeechSynthesisCancellationDetails.FromResult(res);
    return (false, path, cancel.Reason.ToString(), cancel.ErrorDetails);
  }

  public void Dispose()
  {
    _synth?.Dispose();
    _audio?.Dispose();
    _ttsGate.Dispose();
  }
}
