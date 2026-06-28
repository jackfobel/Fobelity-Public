using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core.Models
{
  public record ThermostatStatus(
      string? Mode, double? SetpointCoolF, double? SetpointHeatF,
      double? InsideTempF, double? HumidityPct, bool IsOn, DateTimeOffset LastUpdatedUtc);
}
