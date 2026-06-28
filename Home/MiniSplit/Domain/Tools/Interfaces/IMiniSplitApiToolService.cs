using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Tools.Interfaces
{
  public interface IMiniSplitApiToolService
  {
    Task<string> GetStatusAsync();
    Task<string> TurnOnAsync();
    Task<string> TurnOffAsync();
  }

}
