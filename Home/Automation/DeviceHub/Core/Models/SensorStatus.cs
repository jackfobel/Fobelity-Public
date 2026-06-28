using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core.Models
{
  public record SensorStatus(double? TemperatureF, bool? Occupied, DateTimeOffset LastUpdatedUtc);
}
