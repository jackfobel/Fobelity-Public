using System.Security;
using System.Text;
using System.Text.RegularExpressions;

public enum MochaSsmlPreset
{
  Ack,
  Neutral,
  Confirm,
  Status,
  Friendly,
  Cheerful,
  Excited,
  Whisper,
  Urgent,
  Sad,
  Angry
}

internal static class MochaSsml
{
  // NOTE: parameter names matter for named args (Pitch/Volume/etc.)
  private sealed record Profile(
    string Style,
    string Rate,
    string Pitch = "0%",
    string Volume = "default",
    double? StyleDegree = null,
    string? Role = null,
    string? Contour = null,
    int AckPauseMs = 200);

  // Conservative baselines for en-US-JennyNeural
  private static readonly IReadOnlyDictionary<MochaSsmlPreset, Profile> Profiles =
    new Dictionary<MochaSsmlPreset, Profile>
    {
      [MochaSsmlPreset.Ack] = new("chat", "-10%", Pitch: "-1%", AckPauseMs: 200),
      [MochaSsmlPreset.Neutral] = new("assistant", "-4%", Pitch: "0%"),
      [MochaSsmlPreset.Confirm] = new("customerservice", "-6%", Pitch: "-1%"),
      [MochaSsmlPreset.Status] = new("newscast", "-3%", Pitch: "-2%"),

      [MochaSsmlPreset.Friendly] = new("friendly", "-3%", Pitch: "+2%"),
      [MochaSsmlPreset.Cheerful] = new("cheerful", "-1%", Pitch: "+2%"),
      [MochaSsmlPreset.Excited] = new("excited", "+1%", Pitch: "+3%"),

      [MochaSsmlPreset.Whisper] = new("whispering", "-8%", Pitch: "-1%", Volume: "-3dB"),
      [MochaSsmlPreset.Urgent] = new("shouting", "+4%", Pitch: "+2%", Volume: "+2dB"),

      [MochaSsmlPreset.Sad] = new("sad", "-10%", Pitch: "-2%"),
      [MochaSsmlPreset.Angry] = new("angry", "-2%", Pitch: "+1%")
    };

  public static string BuildPreset(string plainText, string? voiceName, MochaSsmlPreset preset)
  {
    var p = Profiles.TryGetValue(preset, out var prof)
      ? prof
      : Profiles[MochaSsmlPreset.Neutral];

    return Build(
      plainText: plainText,
      voiceName: voiceName,
      style: p.Style,
      styleDegree: p.StyleDegree ?? 1.0,
      role: p.Role,
      rate: p.Rate,
      pitch: p.Pitch,
      volume: p.Volume,
      contour: p.Contour,
      ackPauseMs: p.AckPauseMs);
  }

  // Keep signature compatible with your existing SayMochaAsync callsite.
  public static string Build(
    string plainText,
    string? voiceName,
    string style = "chat",
    double styleDegree = 1.0,
    string? role = null,
    string rate = "-7%",
    string pitch = "0%",
    string volume = "default",
    string? contour = null,
    int ackPauseMs = 200)
  {
    var t = (plainText ?? string.Empty).Trim();
    if (t.Length == 0) t = "Okay.";

    static string Escape(string s) => SecurityElement.Escape(s) ?? string.Empty;

    // Add a short break after common acknowledgements.
    var m = Regex.Match(
      t,
      @"^(?<ack>okay|got it|alright|sure|no problem|on it|all good|cool|makes sense|affirmative|no worries)[\.\!\?]?\s+(?<rest>.+)$",
      RegexOptions.IgnoreCase);

    var inner = m.Success
      ? $"{Escape(m.Groups["ack"].Value)}<break time=\"{ackPauseMs}ms\"/>{Escape(m.Groups["rest"].Value)}"
      : Escape(t);

    // Optional <voice>
    var voiceOpen = !string.IsNullOrWhiteSpace(voiceName) ? $"<voice name=\"{Escape(voiceName!)}\">" : "";
    var voiceClose = !string.IsNullOrWhiteSpace(voiceName) ? "</voice>" : "";

    // Build <prosody ...>
    var prosodyAttrs = new StringBuilder();
    prosodyAttrs.Append($"rate=\"{Escape(rate)}\"");

    if (!string.IsNullOrWhiteSpace(pitch) && !string.Equals(pitch, "default", StringComparison.OrdinalIgnoreCase))
      prosodyAttrs.Append($" pitch=\"{Escape(pitch)}\"");

    if (!string.IsNullOrWhiteSpace(volume) && !string.Equals(volume, "default", StringComparison.OrdinalIgnoreCase))
      prosodyAttrs.Append($" volume=\"{Escape(volume)}\"");

    if (!string.IsNullOrWhiteSpace(contour))
      prosodyAttrs.Append($" contour=\"{Escape(contour)}\"");

    // Build <mstts:express-as ...>
    var expressAttrs = new StringBuilder();
    expressAttrs.Append($"style=\"{Escape(style)}\"");

    // Style degree is optional; keep it only when meaningful.
    if (styleDegree is > 0 and <= 2.0)
      expressAttrs.Append($" styledegree=\"{styleDegree:0.##}\"");

    if (!string.IsNullOrWhiteSpace(role))
      expressAttrs.Append($" role=\"{Escape(role)}\"");

    return $"""
<speak version="1.0"
       xmlns="http://www.w3.org/2001/10/synthesis"
       xmlns:mstts="https://www.w3.org/2001/mstts"
       xml:lang="en-US">
  {voiceOpen}
    <mstts:express-as {expressAttrs}>
      <prosody {prosodyAttrs}>{inner}</prosody>
    </mstts:express-as>
  {voiceClose}
</speak>
""";
  }
}
