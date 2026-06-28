using System;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;
using Fobelity.Home.Automation.Edge.Abstractions;
using Microsoft.Extensions.Options;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

internal sealed class PiGpioSwitch : IGpioSwitch //, IDisposable
{
  private readonly int _pin;
  private readonly GpioController _ctrl;
  private bool _disposed;

  public PiGpioSwitch(IOptions<GpioOptions> opts)
  {
    _pin = opts.Value.LedPin;
    _ctrl = new GpioController(); // default = logical (BCM)
    if (!_ctrl.IsPinOpen(_pin))
      _ctrl.OpenPin(_pin, PinMode.Output, PinValue.Low);
  }

  public Task SetOnAsync(CancellationToken ct = default)
  { _ctrl.Write(_pin, PinValue.High); return Task.CompletedTask; }

  public Task SetOffAsync(CancellationToken ct = default)
  { _ctrl.Write(_pin, PinValue.Low); return Task.CompletedTask; }

  public Task<bool> IsOnAsync(CancellationToken ct = default) =>
      Task.FromResult(_ctrl.Read(_pin) == PinValue.High);

  //public void Dispose()
  //{
  //  if (_disposed) return;
  //  try { if (_ctrl.IsPinOpen(_pin)) _ctrl.ClosePin(_pin); } catch { }
  //  _ctrl.Dispose();
  //  _disposed = true;
  //}

}
