using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Command.Models
{
  public class TuyaCommandPayload
  {
    public Command[] commands { get; set; }

    public class Command
    {
      public string code { get; set; }
      public object value { get; set; }
    }
  }
}
