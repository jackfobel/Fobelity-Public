using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core.Models
{
  public record Device(
      string? Id, string? Name, string? Location,
      string? Brand, string? Model, IReadOnlyList<string>? Capabilities);
}
