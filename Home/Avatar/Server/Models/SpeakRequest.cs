public sealed record SpeakRequest(
  string? Text,
  string? Ssml,
  string? Voice,
  string? ClientId,
  string? Animation // NEW
);
