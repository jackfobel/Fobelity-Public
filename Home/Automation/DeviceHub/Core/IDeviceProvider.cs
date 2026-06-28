using Fobelity.Home.Automation.DeviceHub.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core
{
  public interface IDeviceProvider
  {
    Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct = default);
  }

}
