namespace Fobelity.Home.Automation.Edge.Abstractions;

public interface IGpioSwitch
{
  Task SetOnAsync(CancellationToken ct = default);
  Task SetOffAsync(CancellationToken ct = default);
  Task<bool> IsOnAsync(CancellationToken ct = default);
}

public sealed record DeviceStatus(bool On, DateTimeOffset ServerTime);

public sealed class GpioOptions
{
  // from Python: LED_LINE=17, PIR_LINE default 23
  public int LedPin { get; init; } = 17;
  public int PirPin { get; init; } = 23;

  // from Python defaults
  public double DebounceSeconds { get; init; } = 0.15;
  public double HoldSeconds { get; init; } = 3.0;
  public double MaxHighSeconds { get; init; } = 30.0;
}

public sealed record MotionDebug(
    int RawStable,
    double RawSinceAgeSeconds,
    double? LastRiseAgeSeconds
);

public sealed record MotionStatus(
    bool Present,
    string? LastChangeUtc,
    double HoldSeconds,
    MotionDebug Debug
);
