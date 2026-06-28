namespace Fobelity.Home.Automation.Lights.Service.Core.Models;

public sealed record LightInfo(
  string Id,
  string Name,
  string Location,
  string TopicRoot
);

public sealed record LightSetRequest(
  string? State = null,     // "ON" | "OFF"
  int? Brightness = null,   // 0..254
  int? ColorTemp = null,    // mireds (e.g. 233..370)
  bool DryRun = false
);

public sealed record LightSetResult(
  string Id,
  string Topic,
  object Payload,
  DateTimeOffset SentAtUtc
);
