using Fobelity.Home.Automation.Edge.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

internal sealed class PirMonitor : BackgroundService, IDisposable
{
  private readonly int _pin;
  private readonly double _debounceS, _holdS, _maxHighS;
  private readonly GpioController _ctrl;

  private int _rawLast = -1;
  private int _rawStable = 0;
  private double _rawSince = 0;      // monotonic seconds when stable set
  private double? _lastRise = null;  // monotonic seconds of last rising edge
  private bool _present = false;
  private string? _lastChangeUtc = null;

  public MotionStatus Status => BuildStatus();

  public PirMonitor(IOptions<GpioOptions> opts)
  {
    var o = opts.Value;
    _pin = o.PirPin;
    _debounceS = o.DebounceSeconds;
    _holdS = o.HoldSeconds;
    _maxHighS = o.MaxHighSeconds;

    _ctrl = new GpioController(); // default = logical (BCM)
    if (!_ctrl.IsPinOpen(_pin))
      _ctrl.OpenPin(_pin, PinMode.InputPullDown); // <- match Bias.PULL_DOWN
    _rawSince = Now();
  }

  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var lastSample = -1;
    var lastSampleT = Now();

    while (!stoppingToken.IsCancellationRequested)
    {
      var raw = _ctrl.Read(_pin) == PinValue.High ? 1 : 0;
      var now = Now();

      if (raw != lastSample)
      {
        lastSample = raw;
        lastSampleT = now;
        _rawLast = raw;
      }

      // Debounce to stable
      if ((now - lastSampleT) >= _debounceS && raw != _rawStable)
      {
        var prevStable = _rawStable;
        _rawStable = raw;
        _rawSince = now;

        if (prevStable == 0 && _rawStable == 1)
          _lastRise = now; // rising edge
      }

      bool presentNow = _lastRise.HasValue && (now - _lastRise.Value) <= _holdS;

      // watchdog if raw sticks high too long
      if (_rawStable == 1 && (now - _rawSince) > _maxHighS)
        presentNow = false;

      if (presentNow != _present)
      {
        _present = presentNow;
        _lastChangeUtc = DateTimeOffset.UtcNow.ToString("o");
      }

      await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
    }
  }

  private MotionStatus BuildStatus()
  {
    var now = Now();
    var lastRiseAge = _lastRise.HasValue ? now - _lastRise.Value : (double?)null;
    var debug = new MotionDebug(
        RawStable: _rawStable,
        RawSinceAgeSeconds: Math.Round(now - _rawSince, 3),
        LastRiseAgeSeconds: lastRiseAge.HasValue ? Math.Round(lastRiseAge.Value, 3) : null
    );

    return new MotionStatus(
        Present: _present,
        LastChangeUtc: _lastChangeUtc,
        HoldSeconds: _holdS,
        Debug: debug
    );
  }

  private static double Now() => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

  public void Dispose()
  {
    try { if (_ctrl.IsPinOpen(_pin)) _ctrl.ClosePin(_pin); } catch { }
    _ctrl.Dispose();
  }
}
