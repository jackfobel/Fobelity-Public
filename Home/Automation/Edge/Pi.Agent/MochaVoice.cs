// MochaVoice.cs
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Parsing;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
// ---------- A2A 0.3.1-preview aliases ----------
using A2AAgentTask = A2A.AgentTask;
using A2AMessage = A2A.AgentMessage;
using A2AMessageRole = A2A.MessageRole;
using A2AMessageSendParams = A2A.MessageSendParams;
using A2ATaskState = A2A.TaskState;
using A2ATextPart = A2A.TextPart;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

public sealed class MochaVoice
{
  private readonly A2AOptions _a2a;
  private readonly MochaOptions _mocha;
  private readonly AzureSpeechOptions _speech;
  private readonly IHttpClientFactory _http;
  private readonly string _speechKey;
  private readonly AzureSpeechClient _localTts;
  public int KwsOneShotLingerMs { get; set; } = 900;

  private A2A.A2AClient? _client;

  // Single-owner mic gate to avoid SPXERR_MIC_NOT_AVAILABLE
  private readonly SemaphoreSlim _micGate = new(1, 1);

  private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

  // Allow env override for quick tests on-device
  private string? KeywordModelPath =>
      _mocha.KeywordModelPath ??
      Environment.GetEnvironmentVariable("MOCHA_KEYWORD_MODEL");

  private string DefaultVoice =>
    string.IsNullOrWhiteSpace(_speech.Voice) ? "en-US-JennyNeural" : _speech.Voice;

  public MochaVoice(
      IOptions<A2AOptions> a2a,
      IOptions<MochaOptions> mocha,
      IOptions<AzureSpeechOptions> speech,
      IHttpClientFactory http,
      AzureSpeechClient localTts)
  {
    _a2a = a2a.Value;
    _mocha = mocha.Value;
    _speech = speech.Value;
    _http = http;
    _localTts = localTts;

    _speechKey = Environment.GetEnvironmentVariable("SPEECH_KEY") ?? _speech.Key
                 ?? throw new InvalidOperationException("Missing Speech key");
  }

  // ---------------- helpers ----------------

  private SpeechConfig MakeSpeechConfig()
  {
    var cfg = SpeechConfig.FromSubscription(_speechKey, _speech.Region);
    cfg.SpeechRecognitionLanguage = "en-US";

    // Endpointing (when to start / stop)
    cfg.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, _mocha.InitialSilenceMs.ToString());
    cfg.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, _mocha.EndSilenceMs.ToString());

    // Write the SDK trace to /tmp for field diagnostics on the Pi
    cfg.SetProperty(PropertyId.Speech_LogFilename, "/tmp/speechsdk.log");
    return cfg;
  }

  private static void WireRecognizerLogging(SpeechRecognizer r, string tag)
  {
    r.SessionStarted += (_, __) => Log.Information("[{Tag}] session started", tag);
    r.SessionStopped += (_, __) => Log.Information("[{Tag}] session stopped", tag);
    r.SpeechStartDetected += (_, __) => Log.Debug("[{Tag}] speech start", tag);
    r.SpeechEndDetected += (_, __) => Log.Debug("[{Tag}] speech end", tag);
    r.Canceled += (_, e) =>
      Log.Error("[{Tag}] canceled: Reason={Reason} ErrorCode={Code} Details={Details}",
                tag, e.Reason, e.ErrorCode, e.ErrorDetails);
    r.Recognizing += (_, e) =>
      Log.Debug("[{Tag}] recognizing: Reason={Reason} Text='{Text}'",
                tag, e.Result.Reason, e.Result.Text);
    r.Recognized += (_, e) =>
      Log.Information("[{Tag}] recognized: Reason={Reason} Text='{Text}'",
                      tag, e.Result.Reason, e.Result.Text);
  }

  /// <summary>
  /// Convenience wrapper so your SSML shaping is applied consistently.
  /// </summary>
  public Task SayMochaAsync(
    string text,
    CancellationToken ct,
    MochaSsmlPreset? preset = null,
    string style = "chat",
    double styleDegree = 1.10,
    string rate = "-7%",
    string pitch = "0%",
    string volume = "default",
    int ackPauseMs = 200,
    string? role = null,
    string? avatarAnimation = null)
  {
    _ = role; // MochaSsml.Build currently does not accept a "role" arg; keep for call-site compatibility.

    var cleaned = CleanForTts(text);

    string ssml = preset.HasValue
        ? MochaSsml.BuildPreset(cleaned, DefaultVoice, preset.Value)
        : MochaSsml.Build(
            plainText: cleaned,
            voiceName: DefaultVoice,
            style: style,
            styleDegree: styleDegree,
            rate: rate,
            pitch: pitch,
            volume: volume,
            contour: null,
            ackPauseMs: ackPauseMs);

    return SafeSayAsync(cleaned, ssml, ct, avatarAnimation);
  }

  public Task SafeSayAsync(string text, string? ssml, CancellationToken ct = default)
    => SafeSayAsync(text, ssml, ct, avatarAnimation: null);

  private async Task SafeSayAsync(string text, string? ssml, CancellationToken ct, string? avatarAnimation)
  {
    var toSpeak = SanitizeForSpeech(text);
    var url = _a2a.SayEndpoint?.Trim();
    var hasSsml = !string.IsNullOrWhiteSpace(ssml);

    if (string.IsNullOrWhiteSpace(url))
    {
      Log.Information("[TTS] no SayEndpoint configured; using local TTS only");

      if (hasSsml)
      {
        var (okS, reasonS, errorS) = await _localTts.SpeakSsmlAsync(ssml!);
        Log.Information("[TTS] route=local-ssml ok={Ok} reason={Reason} error={Error}", okS, reasonS, errorS);
      }
      else
      {
        var (ok0, reason0, error0) = await _localTts.SpeakAsync(toSpeak);
        Log.Information("[TTS] route=local ok={Ok} reason={Reason} error={Error}", ok0, reason0, error0);
      }

      return;
    }

    var http = _http.CreateClient();

    // avatar-server uses /api/tts/say and expects JSON.
    var expectsJson = url.Contains("/api/tts/say", StringComparison.OrdinalIgnoreCase);

    object payload = expectsJson
        ? new
        {
          text = toSpeak,
          ssml = hasSsml ? ssml : null,
          voice = DefaultVoice,
          animation = avatarAnimation // <--- new
        }
        : toSpeak;

    using var content = expectsJson
      ? new StringContent(JsonSerializer.Serialize(payload, WebJson), Encoding.UTF8, "application/json")
      : new StringContent(toSpeak, Encoding.UTF8, "text/plain");

    Log.Information("[TTS] route={Route} url={Url} hasSsml={HasSsml} len={Len}",
      expectsJson ? "avatar-json" : "raw",
      url,
      hasSsml,
      toSpeak.Length);

    try
    {
      using var resp = await http.PostAsync(url, content, ct);

      var bodyString = await resp.Content.ReadAsStringAsync(ct);
      Log.Debug("[TTS] server reply: {Body}", bodyString);

      if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
      {
        Log.Warning("[TTS] 404 from {Url}; falling back to local", url);
        await SpeakLocalFallbackAsync(toSpeak, ssml);
        return;
      }

      resp.EnsureSuccessStatusCode();
      Log.Information("[TTS] route=http ok (len {Len})", toSpeak.Length);
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "[TTS] http failed; falling back to local");
      await SpeakLocalFallbackAsync(toSpeak, ssml);
    }
  }

  public Task DelayAfterTts(CancellationToken ct) =>
      Task.Delay(Math.Max(0, _mocha.PostTtsDelayMs), ct);

  public static async Task<string> RecognizeOnceWithTimeoutAsync(
    SpeechRecognizer recognizer, int seconds, CancellationToken ct)
  {
    var s = Math.Max(1, seconds);
    var initialMs = (s * 1000).ToString();

    recognizer.Properties.SetProperty(
      PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs,
      initialMs);

    // End the utterance quickly once the user stops talking.
    recognizer.Properties.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "800");
    recognizer.Properties.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "800");

    if (ct.IsCancellationRequested)
      return string.Empty;

    SpeechRecognitionResult? r;
    try
    {
      r = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);
    }
    catch
    {
      return string.Empty;
    }

    if (ct.IsCancellationRequested)
      return string.Empty;

    return r?.Reason == ResultReason.RecognizedSpeech ? (r.Text ?? string.Empty) : string.Empty;
  }

  private async Task<string> SendToActuatorAsync(string userText, CancellationToken ct)
  {
    if (_client is null)
    {
      try
      {
        var card = await new A2A.A2ACardResolver(new Uri(_a2a.BaseUrl)).GetAgentCardAsync(ct);
        _client = new A2A.A2AClient(new Uri(card.Url));
      }
      catch
      {
        _client = new A2A.A2AClient(new Uri(_a2a.BaseUrl));
      }
    }

    var send = new A2AMessageSendParams
    {
      Message = new A2AMessage
      {
        Role = A2AMessageRole.User,
        Parts = [new A2ATextPart { Text = userText }]
      }
    };

    var result = await _client.SendMessageAsync(send, ct);

    if (result is A2AMessage m)
    {
      Log.Information("[A2A] A2AMessage: Done.");
      return m.Parts.OfType<A2ATextPart>().FirstOrDefault()?.Text ?? "Done.";
    }

    if (result is A2AAgentTask t)
    {
      while (t.Status.State is not A2ATaskState.Completed and not A2ATaskState.Failed)
      {
        await Task.Delay(350, ct);
        t = await _client.GetTaskAsync(t.Id, ct);
      }

      var text = t.Artifacts?.LastOrDefault()?
                   .Parts?.OfType<A2ATextPart>()
                   .FirstOrDefault()?.Text;

      Log.Information("[A2A] A2AAgentTask: Done.");
      return string.IsNullOrWhiteSpace(text) ? "Done." : text!;
    }

    Log.Information("[A2A] SendToActuatorAsync: Done.");
    return "Done.";
  }

  private static string CleanForTts(string? s)
  {
    if (string.IsNullOrWhiteSpace(s)) return string.Empty;
    var t = s.Trim();

    if (t.StartsWith("text:", StringComparison.OrdinalIgnoreCase))
      t = t.Substring(5).TrimStart();

    // Prefer a short pause instead of literally saying "colon"
    t = t.Replace(':', ',').Replace(';', ',');
    while (t.Contains("  ")) t = t.Replace("  ", " ");

    return t;
  }

  private static bool LooksLikeFailure(string text)
  {
    if (string.IsNullOrWhiteSpace(text)) return true;
    var t = text.ToLowerInvariant();
    return t.Contains("failed") ||
           t.Contains("error") ||
           t.Contains("unable") ||
           t.Contains("timed out") ||
           t.Contains("timeout") ||
           t.Contains("exception") ||
           t.Contains("couldn't") ||
           t.Contains("cannot");
  }

  private static string MakeMochaLine(string rawCommand, string actuatorReply)
  {
    var r = CleanForTts(actuatorReply);

    if (string.Equals(r, "done.", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(r, "done", StringComparison.OrdinalIgnoreCase))
      r = "All set.";

    if (LooksLikeFailure(r))
      return "I couldn’t reach it. Want me to try again?";

    var parts = r.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                 .Select(p => p.Trim())
                 .Where(p => p.Length > 0)
                 .ToList();

    if (parts.Count >= 2)
      r = $"{parts[0]}. {parts[1]}.";

    if (!Regex.IsMatch(r, @"^(okay|got it|alright|sure|no problem|on it|show thing|all good|cool|makes sense|affirmative|no worries)\b", RegexOptions.IgnoreCase))
      r = $"Got it. {r}";

    return r.Trim();
  }

  public async Task ListenAfterGreetingAsync(string? prompt, CancellationToken ct)
  {
    await _micGate.WaitAsync(ct);
    try
    {
      // Stage 1: silent listen (user often speaks immediately after greeting)
      const int silentSeconds = 2;

      Log.Information("[POSTGREET] silent listen starting ({Seconds}s)", silentSeconds);

      {
        var cfg1 = MakeSpeechConfig();
        using var mic1 = CreateRecognizerWithMicFallback(cfg1, "postgreet-1");
        var rec1 = mic1.Recognizer;
        WireRecognizerLogging(rec1, "postgreet-1");

        var cmd1 = await RecognizeOnceWithTimeoutAsync(rec1, silentSeconds, ct);
        cmd1 = StripWakeVariants(cmd1 ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(cmd1))
        {
          Log.Information("[POSTGREET] captured during silent window: {Cmd}", cmd1);
          await ExecuteCommandAsync(cmd1, ct);
          return;
        }
      }

      // Stage 2: prompt then listen
      if (!string.IsNullOrWhiteSpace(prompt))
      {
        // (Optional) speak prompt with neutral preset, if you want it to sound consistent:
        // await SayMochaAsync(prompt, ct, preset: MochaSsmlPreset.Neutral);

        //await SafeSayAsync(prompt);
        await SayMochaAsync(prompt, ct, preset: MochaSsmlPreset.Neutral);

        await DelayAfterTts(ct);
        await Task.Delay(Math.Max(0, _mocha.PostGreetSettleMs), ct);
      }

      var listenSeconds = Math.Max(3, _mocha.ListenTimeoutSeconds);
      Log.Information("[POSTGREET] prompted listen starting ({Seconds}s)", listenSeconds);

      var cfg2 = MakeSpeechConfig();
      using var mic2 = CreateRecognizerWithMicFallback(cfg2, "postgreet-2");
      var rec2 = mic2.Recognizer;
      WireRecognizerLogging(rec2, "postgreet-2");

      var cmd2 = await RecognizeOnceWithTimeoutAsync(rec2, listenSeconds, ct);
      cmd2 = StripWakeVariants(cmd2 ?? string.Empty);

      if (string.IsNullOrWhiteSpace(cmd2))
      {
        await SayMochaAsync("Sorry, I didn't catch that.", ct, preset: MochaSsmlPreset.Neutral);
        await DelayAfterTts(ct);

        // Small settle so you don't capture TTS tail.
        await Task.Delay(250, ct);

        // Retry once, immediately.
        cmd2 = await RecognizeOnceWithTimeoutAsync(rec2, listenSeconds, ct);
        cmd2 = StripWakeVariants(cmd2 ?? string.Empty);

        if (string.IsNullOrWhiteSpace(cmd2))
          return;

        await ExecuteCommandAsync(cmd2, ct);
        return;
      }


      await ExecuteCommandAsync(cmd2, ct);
    }
    finally
    {
      _micGate.Release();
    }
  }

  // ---------------- public modes ----------------

  public async Task ListenAfterGreetAsync(CancellationToken ct)
  {
    await SayMochaAsync("If you need me, say Hey Mocha.", ct, style: "chat", styleDegree: 1.05, rate: "-8%", pitch: "+0%");
    await DelayAfterTts(ct);

    if (!string.IsNullOrWhiteSpace(KeywordModelPath))
      await ListenWithKeywordAsync(ct);
    else
      await ListenWithPhraseBoostAsync(ct);
  }

  public async Task ListenOneShotAsync(bool forceDryRun = false, CancellationToken ct = default)
  {
    await _micGate.WaitAsync(ct);
    try
    {
      var cfg = MakeSpeechConfig();

      using var mic = CreateRecognizerWithMicFallback(cfg, "oneshot");
      var recognizer = mic.Recognizer;
      WireRecognizerLogging(recognizer, "oneshot");

      if (string.IsNullOrWhiteSpace(KeywordModelPath))
      {
        await SayMochaAsync("Listening for one command.", ct, style: "chat", styleDegree: 1.05, rate: "-8%", pitch: "+0%");
        await DelayAfterTts(ct);

        var cmdFallback = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
        if (string.IsNullOrWhiteSpace(cmdFallback))
        {
          await SayMochaAsync("Sorry, I didn't catch that.", ct, preset: MochaSsmlPreset.Neutral);
          await DelayAfterTts(ct);
          await Task.Delay(250, ct);

          // Retry once.
          cmdFallback = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
          if (string.IsNullOrWhiteSpace(cmdFallback))
            return;
        }


        var textToSend = (forceDryRun || _a2a.AppendDryRunPrefix || _mocha.RequireConfirmation)
          ? $"Dry run: {cmdFallback}"
          : cmdFallback;

        var replyFallback = await SendToActuatorAsync(textToSend, ct);
        await SayMochaAsync(replyFallback, ct, style: "chat", styleDegree: 1.10, rate: "-7%", pitch: "+0%");
        return;
      }

      if (!File.Exists(KeywordModelPath))
      {
        Log.Warning("[KWS] Model not found at {Path}. Falling back to phrase boost.", KeywordModelPath);
        await ListenWithPhraseBoostAsync(ct);
        return;
      }

      var model = KeywordRecognitionModel.FromFile(KeywordModelPath);
      var hotwordHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

      EventHandler<SpeechRecognitionEventArgs>? onRecognized = (_, e) =>
      {
        if (e.Result.Reason == ResultReason.RecognizedKeyword)
          hotwordHit.TrySetResult(true);
      };

      recognizer.Recognized += onRecognized;

      try
      {
        await recognizer.StartKeywordRecognitionAsync(model);
        Log.Information("[KWS] listening for wake word for {Secs}s", _mocha.WakeWindowSeconds);

        using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        windowCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _mocha.WakeWindowSeconds)));

        try { await Task.WhenAny(hotwordHit.Task, Task.Delay(-1, windowCts.Token)); }
        catch { /* timeout */ }

        try { await recognizer.StopKeywordRecognitionAsync(); }
        catch (Exception ex) { Log.Warning(ex, "[KWS] stop keyword recognition failed"); }

        if (!(hotwordHit.Task.IsCompletedSuccessfully && hotwordHit.Task.Result))
        {
          Log.Warning("[KWS] timed out without hotword");
          return;
        }

        await Task.Delay(200, ct);

        var cmd = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
        cmd = StripWakeVariants(cmd ?? string.Empty);

        if (string.IsNullOrWhiteSpace(cmd))
        {
          await SayMochaAsync("Sorry, I didn't catch that.", ct, preset: MochaSsmlPreset.Neutral);
          await DelayAfterTts(ct);
          await Task.Delay(250, ct);

          // Retry once (still in the same "wake" interaction).
          cmd = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
          cmd = StripWakeVariants(cmd ?? string.Empty);

          if (string.IsNullOrWhiteSpace(cmd))
            return;
        }


        var toSend = (forceDryRun || _a2a.AppendDryRunPrefix || _mocha.RequireConfirmation)
          ? $"Dry run: {cmd}"
          : cmd;

        var replyFromActuator = await SendToActuatorAsync(toSend, ct);
        await SayMochaAsync(replyFromActuator, ct, style: "chat", styleDegree: 1.10, rate: "-7%", pitch: "+0%");
      }
      finally
      {
        try { await recognizer.StopKeywordRecognitionAsync(); }
        catch (Exception ex) { Log.Warning(ex, "[KWS] stop keyword recognition failed"); }

        recognizer.Recognized -= onRecognized;
      }
    }
    finally
    {
      _micGate.Release();
    }
  }

  private async Task ListenWithPhraseBoostAsync(CancellationToken ct)
  {
    await _micGate.WaitAsync(ct);
    try
    {
      var cfg = MakeSpeechConfig();

      using var mic = CreateRecognizerWithMicFallback(cfg, "kws");
      var recognizer = mic.Recognizer;
      WireRecognizerLogging(recognizer, "phrase-boost");

      var pl = PhraseListGrammar.FromRecognizer(recognizer);
      pl.AddPhrase(_mocha.WakeWord);
      pl.AddPhrase("Mocha");
      pl.AddPhrase("Ecobee");
      pl.AddPhrase("mini split");
      pl.AddPhrase("shop");
      pl.AddPhrase("house");

      var until = DateTime.UtcNow.AddSeconds(_mocha.WakeWindowSeconds);
      while (DateTime.UtcNow < until && !ct.IsCancellationRequested)
      {
        var heard = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
        if (string.IsNullOrWhiteSpace(heard)) continue;

        if (heard.ToLowerInvariant().Contains("mocha"))
        {
          var direct = StripWakeVariants(heard ?? string.Empty);
          if (!string.IsNullOrWhiteSpace(direct))
          {
            await ExecuteCommandAsync(direct, ct);
            return;
          }

          await SayMochaAsync("Yes?", ct, style: "chat", styleDegree: 1.05, rate: "-8%", pitch: "+0%");
          await DelayAfterTts(ct);
          await Task.Delay(Math.Max(0, _mocha.KwsFollowSettleMs), ct);

          var cmd = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
          cmd = StripWakeVariants(cmd ?? string.Empty);

          if (string.IsNullOrWhiteSpace(cmd))
          {
            await SayMochaAsync("Sorry, I didn't catch that.", ct, preset: MochaSsmlPreset.Neutral);
            await DelayAfterTts(ct);
            await Task.Delay(250, ct);

            // Retry once (still in the same "wake" interaction).
            cmd = await RecognizeOnceWithTimeoutAsync(recognizer, _mocha.ListenTimeoutSeconds, ct);
            cmd = StripWakeVariants(cmd ?? string.Empty);

            if (string.IsNullOrWhiteSpace(cmd))
              return;
          }


          await ExecuteCommandAsync(cmd, ct);
          return;
        }
      }
    }
    finally
    {
      _micGate.Release();
    }
  }

  public async Task ListenWithKeywordAsync(CancellationToken ct)
  {
    var sw = Stopwatch.StartNew();
    await _micGate.WaitAsync(ct);
    Log.Information("[POSTGREET] ListenWithKeywordAsync: mic gate acquired after {Ms}ms", sw.ElapsedMilliseconds);

    try
    {
      var path = KeywordModelPath;
      if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
      {
        Log.Warning("[KWS] Model path missing/invalid. Falling back. Path={Path}", path);
        await ListenWithPhraseBoostAsync(ct);
        return;
      }

      var cfg = MakeSpeechConfig();

      using var mic = CreateRecognizerWithMicFallback(cfg, "kws");
      var rec = mic.Recognizer;
      WireRecognizerLogging(rec, "kws");

      var model = KeywordRecognitionModel.FromFile(path);

      var kwHit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
      var oneShot = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

      rec.Recognized += (_, e) =>
      {
        if (e.Result.Reason == ResultReason.RecognizedKeyword)
        {
          Log.Information("[KWS] recognized keyword Text='{Text}'", e.Result.Text);
          kwHit.TrySetResult(true);
          return;
        }

        if (e.Result.Reason == ResultReason.RecognizedSpeech)
        {
          var raw = e.Result.Text?.Trim() ?? string.Empty;
          if (string.IsNullOrEmpty(raw)) return;

          var lower = raw.ToLowerInvariant();
          if (LooksLikeWake(lower))
          {
            var cleaned = StripWakeVariants(raw ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(cleaned))
            {
              Log.Information("[KWS] one-shot captured: '{Cmd}'", cleaned);
              oneShot.TrySetResult(cleaned);
            }
          }
        }
      };

      rec.Canceled += (_, e) =>
        Log.Warning("[KWS] canceled: Reason={Reason} ErrorCode={Code} Details={Details}",
                    e.Reason, e.ErrorCode, e.ErrorDetails);

      Log.Information("[KWS] starting with model {Path} for {Secs}s", path, _mocha.WakeWindowSeconds);
      await rec.StartKeywordRecognitionAsync(model);

      using var window = CancellationTokenSource.CreateLinkedTokenSource(ct);
      window.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _mocha.WakeWindowSeconds)));

      string? direct = null;

      Task first = await Task.WhenAny(kwHit.Task, oneShot.Task, Task.Delay(Timeout.Infinite, window.Token));

      if (first == oneShot.Task)
      {
        direct = oneShot.Task.Result;
        Log.Information("[KWS] decision = one-shot (arrived first)");
      }
      else if (first == kwHit.Task)
      {
        var lingerMs = Math.Clamp(_mocha.KwsOneShotLingerMs, 300, 2000);
        Log.Information("[KWS] keyword hit first; lingering {Linger} ms for single-breath text", lingerMs);

        try { await Task.WhenAny(oneShot.Task, Task.Delay(lingerMs, window.Token)); } catch { }

        if (oneShot.Task.IsCompletedSuccessfully && !string.IsNullOrWhiteSpace(oneShot.Task.Result))
        {
          direct = oneShot.Task.Result;
          Log.Information("[KWS] decision = one-shot (arrived during linger)");
        }
        else
        {
          Log.Information("[KWS] decision = keyword-only (no one-shot during linger)");
        }
      }
      else
      {
        Log.Warning("[KWS] window timeout without keyword or one-shot");
      }

      try { await rec.StopKeywordRecognitionAsync(); }
      catch (Exception ex) { Log.Warning(ex, "[KWS] stop keyword recognition failed-2"); }

      if (string.IsNullOrWhiteSpace(direct) &&
          oneShot.Task.IsCompletedSuccessfully &&
          !string.IsNullOrWhiteSpace(oneShot.Task.Result))
      {
        direct = oneShot.Task.Result;
        Log.Information("[KWS] decision = one-shot (arrived during stop)");
      }

      if (!kwHit.Task.IsCompleted && string.IsNullOrWhiteSpace(direct))
      {
        Log.Warning("[KWS] no hotword detected within window");
        return;
      }

      if (!string.IsNullOrWhiteSpace(direct))
      {
        await ExecuteCommandAsync(direct!, ct);
        return;
      }

      await SayMochaAsync("Yes?", ct, style: "chat", styleDegree: 1.05, rate: "-8%", pitch: "+0%");
      await DelayAfterTts(ct);

      await Task.Delay(Math.Max(0, _mocha.KwsFollowSettleMs), ct);

      Log.Information("[KWS] follow-up capture starting");
      var cmd = await RecognizeOnceWithTimeoutAsync(rec, _mocha.ListenTimeoutSeconds, ct);
      cmd = StripWakeVariants(cmd ?? string.Empty);

      if (LooksLikePromptEcho(cmd))
      {
        Log.Warning("[KWS] ignored likely prompt echo: '{Cmd}'", cmd);
        await Task.Delay(250, ct);

        cmd = await RecognizeOnceWithTimeoutAsync(rec, _mocha.ListenTimeoutSeconds, ct);
        cmd = StripWakeVariants(cmd ?? string.Empty);
      }

      if (string.IsNullOrWhiteSpace(cmd))
      {
        Log.Warning("[KWS] follow-up empty");
        return;
      }

      await ExecuteCommandAsync(cmd, ct);
    }
    finally
    {
      _micGate.Release();
    }
  }

  private static bool LooksLikeWake(string textLower)
  {
    if (textLower.StartsWith("hey mocha") || textLower.StartsWith("hi mocha") || textLower.StartsWith("mocha"))
      return true;

    if (textLower.StartsWith("hey moco") || textLower.StartsWith("hi moco") || textLower.StartsWith("moco"))
      return true;

    if (textLower.StartsWith("hey mocho") || textLower.StartsWith("hi mocho") || textLower.StartsWith("mocho"))
      return true;

    return false;
  }

  private static string StripWakeVariants(string raw)
  {
    var t = raw.Trim();
    var lower = t.ToLowerInvariant();

    foreach (var wake in new[] { "hey mocha", "hi mocha", "mocha", "hey moco", "hi moco", "moco", "hey mocho", "hi mocho", "mocho" })
    {
      if (lower.StartsWith(wake))
        return t.Substring(wake.Length).TrimStart(' ', ',', '.', '!', '?', ':', ';', '-');
    }
    return t;
  }

  private static string SanitizeForSpeech(string raw)
  {
    if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
    var t = raw.Trim();

    // If a JSON body was passed, pull the "text" property out
    if (t.StartsWith("{") && t.IndexOf("\"text\"", StringComparison.OrdinalIgnoreCase) >= 0)
    {
      try
      {
        using var doc = JsonDocument.Parse(t);
        if (doc.RootElement.TryGetProperty("text", out var te) && te.ValueKind == JsonValueKind.String)
          t = te.GetString() ?? t;
      }
      catch { /* ignore parse errors */ }
    }

    // Strip dev-ish prefixes like "text: ..." / "say = ..."
    t = Regex.Replace(t, @"^\s*(?:text|say|speech)\s*[:=\-]\s*", "", RegexOptions.IgnoreCase);
    return t;
  }

  private async Task SpeakLocalFallbackAsync(string text, string? ssml)
  {
    var toSpeak = SanitizeForSpeech(text);

    if (!string.IsNullOrWhiteSpace(ssml))
    {
      var (okS, reasonS, errorS) = await _localTts.SpeakSsmlAsync(ssml);
      Log.Information("[TTS] route=local-ssml ok={Ok} reason={Reason} error={Error}", okS, reasonS, errorS);

      if (okS) return;

      Log.Warning("[TTS] local SSML failed; falling back to plain text");
    }

    var (ok, reason, error) = await _localTts.SpeakAsync(toSpeak);
    Log.Information("[TTS] route=local ok={Ok} reason={Reason} error={Error}", ok, reason, error);
  }

  public MicSession CreateRecognizerWithMicFallback(SpeechConfig cfg, string tag)
  {
    var candidates = new[]
    {
      _speech.Mic,                 // if you set it
      "pulse",                     // if available (shared capture)
      "default",
      "dsnoop:CARD=Device,DEV=0",
      "plughw:2,0",
      "hw:2,0",
      "sysdefault:CARD=Device",
      "plughw:CARD=Device,DEV=0",
      "hw:CARD=Device,DEV=0",
      null
    }
    .Where(s => s == null || !string.IsNullOrWhiteSpace(s))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

    Log.Information("[{Tag}] Mic candidate order: {List}",
      tag, string.Join(" -> ", candidates.Select(c => c ?? "<default>")));

    foreach (var candidate in candidates)
    {
      AudioConfig audio;
      try
      {
        audio = candidate is null
          ? AudioConfig.FromDefaultMicrophoneInput()
          : AudioConfig.FromMicrophoneInput(candidate);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[{Tag}] AudioConfig failed for mic '{Mic}'", tag, candidate ?? "<default>");
        continue;
      }

      try
      {
        var rec = new SpeechRecognizer(cfg, audio);
        Log.Information("[{Tag}] Using mic: '{Mic}'", tag, candidate ?? "default");
        return new MicSession(audio, rec);
      }
      catch (Exception ex)
      {
        Log.Warning(ex, "[{Tag}] SpeechRecognizer open failed for mic '{Mic}'", tag, candidate ?? "<default>");
        try { audio.Dispose(); } catch { }
        continue;
      }
    }

    throw new InvalidOperationException("No working microphone device found (AudioConfig or recognizer open failed for all candidates).");
  }

  private sealed record ToneDecision(MochaSsmlPreset Preset, string Animation);


  //private async Task<ToneDecision> DecideToneAsync(string mochaText, CancellationToken ct)
  //{
  //  // 1) Hard rules first (fast, predictable)
  //  var t = (mochaText ?? string.Empty).Trim();
  //  var lower = t.ToLowerInvariant();

  //  // Your existing failure heuristic is already good:
  //  if (LooksLikeFailure(t))
  //  {
  //    // Usually apology, not anger:
  //    return new ToneDecision(MochaSsmlPreset.Sad, "Dying");  // or "Idle" if you dislike Dying
  //  }

  //  if (lower.Contains("sorry") || lower.Contains("i didn't catch that"))
  //    return new ToneDecision(MochaSsmlPreset.Sad, "Idle");

  //  if (lower.Contains("warning") || lower.Contains("urgent") || lower.Contains("right now"))
  //    return new ToneDecision(MochaSsmlPreset.Urgent, "StandingUp");

  //  // 2) Sentiment (only if text is long enough to be meaningful)
  //  if (t.Length < 12)
  //    return new ToneDecision(MochaSsmlPreset.Neutral, "Idle");

  //  // Pick ONE implementation approach below:
  //  var sentiment = await _sentiment.GetAsync(t, ct); // returns (pos, neu, neg) or label
  //                                                    // Example thresholds:
  //  if (sentiment.Positive >= 0.65)
  //    return new ToneDecision(MochaSsmlPreset.Cheerful, "Laughing");

  //  if (sentiment.Positive >= 0.55)
  //    return new ToneDecision(MochaSsmlPreset.Friendly, "Salute");

  //  if (sentiment.Negative >= 0.65)
  //  {
  //    // Only use Angry when it’s a *scolding / boundary* message
  //    // rather than “system couldn’t do it”.
  //    if (lower.Contains("don’t") || lower.Contains("stop") || lower.Contains("not allowed"))
  //      return new ToneDecision(MochaSsmlPreset.Angry, "Angry");

  //    return new ToneDecision(MochaSsmlPreset.Sad, "Idle");
  //  }

  //  return new ToneDecision(MochaSsmlPreset.Neutral, "Idle");
  //}

  // ---------------- tone + animation routing ----------------


  // Map "voice preset" -> "avatar animation" (no file extension; client chooses clip)
  private static readonly IReadOnlyDictionary<MochaSsmlPreset, string> PresetToAnim =
    new Dictionary<MochaSsmlPreset, string>
    {
      [MochaSsmlPreset.Ack] = "Salute",
      [MochaSsmlPreset.Confirm] = "Pointing",
      [MochaSsmlPreset.Status] = "Pointing",
      [MochaSsmlPreset.Friendly] = "Laughing",
      [MochaSsmlPreset.Cheerful] = "Laughing",
      [MochaSsmlPreset.Excited] = "SlideHipHopDance",
      [MochaSsmlPreset.Sad] = "Dying",        // consider "Idle" if Dying feels too strong
      [MochaSsmlPreset.Angry] = "Angry",
      [MochaSsmlPreset.Neutral] = "Idle",
    };

  // Keep these lists small at first; expand as you observe behavior.
  private static readonly HashSet<string> PositiveTokens = new(StringComparer.OrdinalIgnoreCase)
  {
    "okay", "ok", "alright", "sure", "great", "good", "awesome", "perfect", "excellent",
    "nice", "cool", "done", "ready", "set", "completed", "success", "working", "fixed",
    "thanks", "thank", "welcome", "glad", "happy", "yep", "yes",

    // Humor/positive affect
    "joke", "funny", "hilarious", "haha", "lol", "lmao"
  };


  private static readonly HashSet<string> NegativeTokens = new(StringComparer.OrdinalIgnoreCase)
  {
    // Keep true negative affect / failure language:
    "failed", "failure", "error", "issue", "problem", "unable", "timeout", "timed", "out",
    "sorry", "apologize", "missed",

    // Optional: frustration words
    "annoying", "frustrating", "ugh", "ridiculous"
  };


  // Tokenizer: words + apostrophes; cheap and consistent.
  private static readonly Regex WordRx = new(@"[a-zA-Z']+", RegexOptions.Compiled);

  private static double LexiconSentimentScore(string text)
  {
    if (string.IsNullOrWhiteSpace(text))
      return 0;

    int pos = 0, neg = 0;

    foreach (Match m in WordRx.Matches(text))
    {
      var w = m.Value;

      // normalize common apostrophe variants
      if (string.Equals(w, "didn’t", StringComparison.OrdinalIgnoreCase)) w = "didn't";
      if (string.Equals(w, "don’t", StringComparison.OrdinalIgnoreCase)) w = "don't";
      if (string.Equals(w, "won’t", StringComparison.OrdinalIgnoreCase)) w = "won't";
      if (string.Equals(w, "can’t", StringComparison.OrdinalIgnoreCase)) w = "can't";

      // Treat contractions without apostrophes as well
      if (string.Equals(w, "dont", StringComparison.OrdinalIgnoreCase)) w = "don't";
      if (string.Equals(w, "didnt", StringComparison.OrdinalIgnoreCase)) w = "didn't";
      if (string.Equals(w, "wont", StringComparison.OrdinalIgnoreCase)) w = "won't";
      if (string.Equals(w, "cant", StringComparison.OrdinalIgnoreCase)) w = "can't";

      if (PositiveTokens.Contains(w)) pos++;
      if (NegativeTokens.Contains(w)) neg++;
    }

    // Light smoothing so a single hit doesn’t swing too hard.
    // Score is ~ [-1..+1].
    var denom = (pos + neg + 3.0);
    return (pos - neg) / denom;
  }

  private static ToneDecision DecideToneFromText(string mochaText, string? userPrompt = null)
  {
    Log.Information("[A2A] Entered DecideToneFromText.");

    var t = (mochaText ?? string.Empty).Trim();
    var lower = t.ToLowerInvariant();

    // 0) Explicit animation markers from the agent (if you add them)
    var markerAnim = TryExtractAnimMarker(ref t, ref lower);
    if (markerAnim is not null)
    {
      // choose a preset that matches (Cheerful is a good default for Laughing)
      var preset = MochaSsmlPreset.Cheerful;
      return new ToneDecision(preset, markerAnim);
    }

    // 1) Hard rules always win.
    if (LooksLikeFailure(t))
    {
      var preset = MochaSsmlPreset.Sad;
      Log.Information($"[A2A] preset: {preset}");
      return new ToneDecision(preset, PresetToAnim[preset]);
    }

    if (lower.Contains("sorry") || lower.Contains("didn't catch that") || lower.Contains("didnt catch that"))
    {
      var preset = MochaSsmlPreset.Sad;
      Log.Information($"[A2A] preset: {preset}");
      return new ToneDecision(preset, PresetToAnim[preset]);
    }

    // NEW: Humor rule (force Cheerful + Laughing)
    if (LooksLikeHumor(t, userPrompt))
    {
      var preset = MochaSsmlPreset.Cheerful;

      // Override animation explicitly for humor.
      // Make sure the clip name matches exactly: "Laughing"
      Log.Information($"[A2A] preset: {preset} (humor override)");
      return new ToneDecision(preset, "Laughing");
    }

    // 2) Lexicon sentiment for everything else
    var s = LexiconSentimentScore(t);

    if (s >= 0.20)
    {
      var preset = MochaSsmlPreset.Cheerful;
      Log.Information($"[A2A] preset: {preset}");
      return new ToneDecision(preset, PresetToAnim[preset]);
    }

    if (s >= 0.08)
    {
      var preset = MochaSsmlPreset.Friendly;
      Log.Information($"[A2A] preset: {preset}");
      return new ToneDecision(preset, PresetToAnim[preset]);
    }

    if (s <= -0.22)
    {
      if (lower.Contains("don't") || lower.Contains("dont") || lower.Contains("stop") || lower.Contains("not allowed"))
      {
        var preset = MochaSsmlPreset.Angry;
        Log.Information($"[A2A] preset: {preset}");
        return new ToneDecision(preset, PresetToAnim[preset]);
      }

      var presetSad = MochaSsmlPreset.Sad;
      Log.Information($"[A2A] preset: {presetSad}");
      return new ToneDecision(presetSad, PresetToAnim[presetSad]);
    }

    var neutral = MochaSsmlPreset.Neutral;
    Log.Information($"[A2A] preset: {neutral}");
    return new ToneDecision(neutral, PresetToAnim[neutral]);
  }

  private static string? TryExtractAnimMarker(ref string text, ref string lower)
  {
    // Example marker format: [[ANIM:Laughing]]
    var m = Regex.Match(text, @"\[\[ANIM:(?<a>[A-Za-z0-9_ -]+)\]\]", RegexOptions.IgnoreCase);
    if (!m.Success) return null;

    var anim = m.Groups["a"].Value.Trim();

    // remove marker so it won't be spoken
    text = Regex.Replace(text, @"\s*\[\[ANIM:[A-Za-z0-9_ -]+\]\]\s*", " ").Trim();
    lower = text.ToLowerInvariant();

    return anim;
  }


  private static bool ContainsAny(string s, params string[] needles)
  {
    foreach (var n in needles)
      if (s.Contains(n, StringComparison.OrdinalIgnoreCase))
        return true;
    return false;
  }

  /// <summary>
  /// Detect jokes/humor in Mocha output OR optionally in the user prompt.
  /// This prevents “neutral joke setups” from staying Neutral.
  /// </summary>
  private static bool LooksLikeHumor(string mochaText, string? userPrompt = null)
  {
    var t = (mochaText ?? string.Empty).Trim();
    var u = (userPrompt ?? string.Empty).Trim();

    Log.Information($"[A2A] Checking for humor in MochaText='{t}' UserPrompt='{u}'");

    // Strong indicators from the user
    if (!string.IsNullOrEmpty(u) &&
        ContainsAny(u, "tell me a joke", "joke", "something funny", "make me laugh", "pun"))
      return true;

    // Strong indicators in Mocha’s response
    if (ContainsAny(t, "here's a joke", "here is a joke", "knock knock", "pun intended"))
      return true;

    // Common joke setup patterns
    // (“Why did … ?” is often neutral lexically; treat it as humor.)
    if (ContainsAny(t, "why did ", "why do ", "what do you call ", "did you hear about "))
      return true;

    // Laughter tokens / explicit “funny” words
    if (ContainsAny(t, "haha", "ha ha", "lol", "lmao", "😂", "🤣", "funny", "hilarious"))
      return true;

    return false;
  }



  private async Task ExecuteCommandAsync(string rawCommand, CancellationToken ct)
  {
    // Immediate ack: slightly brighter pitch and slightly slower rate helps clarity.
    var ackText = CleanForTts("Okay.");
    await SayMochaAsync(ackText, ct, preset: MochaSsmlPreset.Ack);
    await DelayAfterTts(ct);

    var textToSend =
        (_a2a.AppendDryRunPrefix || _mocha.RequireConfirmation)
        ? $"Dry run: {rawCommand}"
        : rawCommand;

    Log.Information("[A2A] sending: {Text}", textToSend);

    string reply = "Done.";
    try
    {
      Log.Information("[VOICE] sending to actuator: {Text}", textToSend);

      var sendTask = SendToActuatorAsync(textToSend, ct);

      var done = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(8), ct));
      if (done != sendTask)
      {
        var ackMoment = CleanForTts("One moment.");
        await SayMochaAsync(ackMoment, ct, preset: MochaSsmlPreset.Ack);
        await DelayAfterTts(ct);

        done = await Task.WhenAny(sendTask, Task.Delay(TimeSpan.FromSeconds(20), ct));
        if (done != sendTask)
        {
          Log.Warning("[A2A] still not done after 28s total");
          await SayMochaAsync("I’m still waiting on that. Try again in a moment.", ct, style: "chat", styleDegree: 0.95, rate: "-8%", pitch: "-1%");
          return;
        }
      }

      reply = await sendTask;
    }
    catch (Exception ex)
    {
      Log.Warning(ex, "[A2A] failed");
    }

    if (string.IsNullOrWhiteSpace(reply))
    {
      Log.Warning("[A2A] reply is empty.");
      reply = "Done.";
    }

    var mochaText = MakeMochaLine(rawCommand, reply);

    // Failure tone: slightly lower pitch + slightly slower.
    var failure = LooksLikeFailure(mochaText);
    //var style = "chat";
    //var styleDegree = failure ? 0.95 : 1.15;
    //var rate = failure ? "-9%" : "-7%";
    //var pitch = failure ? "-2%" : "+1%";



    // Mocha's main reply.
    var decision = DecideToneFromText(mochaText);

    // If you added avatarAnimation plumbing (recommended), pass it here:
    await SayMochaAsync(
      mochaText,
      ct,
      preset: decision.Preset,
      avatarAnimation: decision.Animation);


  }

  private static bool LooksLikePromptEcho(string? raw)
  {
    if (string.IsNullOrWhiteSpace(raw)) return false;

    var t = raw.Trim()
               .Trim('.', '!', '?', ',', ';', ':')
               .ToLowerInvariant();

    return t is "yes"
        or "yeah"
        or "yep"
        or "sure"
        or "okay"
        or "ok"
        or "what can i do for you"
        or "what can i do for you today";
  }

}
