using Fobelity.Home.Automation.DeviceHub.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core.Abstractions
{
  public interface IDeviceThermostat
  {
    Task<ThermostatStatus> GetStatusAsync(CancellationToken ct = default);
    Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct = default);
  }
}
