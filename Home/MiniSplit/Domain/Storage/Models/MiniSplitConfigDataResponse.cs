using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Storage.Models
{
  public class MiniSplitConfigDataResponse
  {
    public MiniSplitConfigData result { get; set; }
    public bool success { get; set; }
    public long t { get; set; } // Unix time
    public string tid { get; set; } // Tracking ID
  }

}
